import { HandshakePayload, HandshakeResponse, EvaluationPayload, EvaluationResponse, AegisStorageState } from './types';

const COMPONENT_ID = 'aegis-extension-chrome';
const PRE_SHARED_SECRET = 'aegis-extension-secret-dev';
const API_BASE_URL = 'http://127.0.0.1:9443';

console.log('[Aegis Extension] Background Service Worker initializing...');

// Setup 1-minute alarm heartbeat for MV3 lifecycle survival
chrome.runtime.onInstalled.addListener(() => {
  console.log('[Aegis Extension] Extension installed. Creating alarm heartbeat...');
  chrome.alarms.create('aegis-heartbeat', { periodInMinutes: 1 });
  performHandshake();
});

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'aegis-heartbeat') {
    console.log('[Aegis Extension] Alarm heartbeat pulse triggered.');
    ensureValidToken();
  }
});

// SPA Navigation listener
if (chrome.webNavigation) {
  chrome.webNavigation.onHistoryStateUpdated.addListener((details) => {
    if (details.frameId === 0) {
      console.log('[Aegis Extension] SPA Navigation detected:', details.url);
      evaluateUrl(details.tabId, details.url);
    }
  });
}

// Runtime message listener from content scripts
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'EVALUATE_PAGE') {
    const tabId = sender.tab?.id;
    if (tabId) {
      evaluateUrl(tabId, message.url, message.title)
        .then((res) => sendResponse(res))
        .catch((err) => sendResponse({ decision: 'Allow', reason: err.message }));
      return true; // Keep channel open for async response
    }
  }
});

async function performHandshake(): Promise<string | null> {
  try {
    const timestamp = Math.floor(Date.now() / 1000);
    const payloadStr = `${COMPONENT_ID}:${timestamp}`;
    const hmacSignature = await computeHmacHex(PRE_SHARED_SECRET, payloadStr);

    const reqPayload: HandshakePayload = {
      componentId: COMPONENT_ID,
      timestamp,
      hmacSignature
    };

    const res = await fetch(`${API_BASE_URL}/handshake`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(reqPayload)
    });

    if (!res.ok) {
      throw new Error(`Handshake failed with status ${res.status}`);
    }

    const data: HandshakeResponse = await res.json();
    const expiresAt = Date.now() + (data.expiresInSeconds * 1000);

    const state: AegisStorageState = {
      token: data.token,
      tokenExpiresAt: expiresAt,
      serviceStatus: 'Healthy',
      lastCheckedAt: Date.now()
    };

    await chrome.storage.local.set(state);
    console.log('[Aegis Extension] Handshake successful. Token cached.');
    return data.token;
  } catch (err) {
    console.warn('[Aegis Extension] Handshake failed, fallback to degraded offline state:', err);
    await chrome.storage.local.set({ serviceStatus: 'Degraded', lastCheckedAt: Date.now() });
    return null;
  }
}

async function ensureValidToken(): Promise<string | null> {
  const data = await chrome.storage.local.get(['token', 'tokenExpiresAt']);
  const now = Date.now();

  if (data.token && data.tokenExpiresAt && data.tokenExpiresAt - now > 60000) {
    return data.token;
  }

  return await performHandshake();
}

async function evaluateUrl(tabId: number, url: string, title?: string): Promise<EvaluationResponse> {
  // Prevent infinite redirect loops on extension pages or block.html
  if (url.startsWith('chrome-extension://') || url.startsWith('edge-extension://') || url.includes('block.html')) {
    return { decision: 'Allow', reason: 'Extension page internal', severity: 'Info', action: 'Allow', componentState: 'Protected' };
  }

  const token = await ensureValidToken();

  if (!token) {
    return { decision: 'Allow', reason: 'Offline fallback', severity: 'Info', action: 'Allow', componentState: 'Degraded' };
  }

  try {
    const evalPayload: EvaluationPayload = {
      url,
      title,
      component: COMPONENT_ID,
      timestamp: new Date().toISOString()
    };

    const res = await fetch(`${API_BASE_URL}/evaluate`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      body: JSON.stringify(evalPayload)
    });

    if (!res.ok) {
      throw new Error(`Evaluate API returned status ${res.status}`);
    }

    const evalRes: EvaluationResponse = await res.json();

    if (evalRes.decision === 'Block' || (evalRes.decision as any) === 1 || evalRes.action === 'Block') {
      console.warn('[Aegis Extension] BLOCK DECISION FOR TAB:', tabId, url);
      const blockPageUrl = chrome.runtime.getURL(`block.html?url=${encodeURIComponent(url)}&reason=${encodeURIComponent(evalRes.reason)}`);
      chrome.tabs.update(tabId, { url: blockPageUrl });
    }

    return evalRes;
  } catch (err: any) {
    console.warn('[Aegis Extension] Evaluation API failed:', err);
    return { decision: 'Allow', reason: 'API error fallback', severity: 'Warning', action: 'Allow', componentState: 'Degraded' };
  }
}

async function computeHmacHex(secret: string, text: string): Promise<string> {
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey(
    'raw',
    enc.encode(secret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign']
  );
  const signature = await crypto.subtle.sign('HMAC', key, enc.encode(text));
  return Array.from(new Uint8Array(signature))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();
}
