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
  syncDynamicRules();
});

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'aegis-heartbeat') {
    ensureValidToken();
    syncDynamicRules();
  }
});

async function syncDynamicRules() {
  try {
    const res = await fetch(`${API_BASE_URL}/policy/custom-rules`);
    if (!res.ok) return;
    const data = await res.json();
    const websites: string[] = data.websites || [];

    const rulesToAdd = websites.map((domain, index) => ({
      id: 10000 + index,
      priority: 2,
      action: { type: 'block' as any },
      condition: {
        requestDomains: [domain],
        resourceTypes: ['main_frame' as any, 'sub_frame' as any, 'xmlhttprequest' as any, 'other' as any]
      }
    }));

    const existingRules = await chrome.declarativeNetRequest.getDynamicRules();
    const existingIds = existingRules.map(r => r.id);

    await chrome.declarativeNetRequest.updateDynamicRules({
      removeRuleIds: existingIds,
      addRules: rulesToAdd
    });
    console.log('[Aegis Extension] Synchronized', rulesToAdd.length, 'dynamic DNR block rules from service.');
  } catch (err) {
    console.warn('[Aegis Extension] Failed to sync dynamic DNR rules:', err);
  }
}

// === BEFORE-NAVIGATE INTERCEPTION (fires before the page loads) ===
if (chrome.webNavigation) {
  chrome.webNavigation.onBeforeNavigate.addListener(async (details) => {
    if (details.frameId !== 0) return; // main frame only
    const url = details.url;
    if (!url.startsWith('http://') && !url.startsWith('https://')) return;
    if (url.startsWith('chrome-extension://') || url.includes('block.html')) return;

    // Trigger proactive sync if needed
    syncDynamicRules();

    const token = await ensureValidToken();
    if (!token) return;

    try {
      const res = await fetch(`${API_BASE_URL}/evaluate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
        body: JSON.stringify({ url, component: COMPONENT_ID, timestamp: new Date().toISOString() })
      });

      if (!res.ok) return;
      const evalRes: EvaluationResponse = await res.json();
      console.log('[Aegis Extension] onBeforeNavigate eval:', url, '->', evalRes.decision);

      if (evalRes.decision === 'Block' || (evalRes.decision as any) === 1 || evalRes.action === 'Block') {
        const blockPageUrl = chrome.runtime.getURL(`block.html?url=${encodeURIComponent(url)}&reason=${encodeURIComponent(evalRes.reason)}`);
        chrome.tabs.update(details.tabId, { url: blockPageUrl }).catch(e => console.error('[Aegis] Tab redirect failed:', e));
      }
    } catch (e) {
      console.warn('[Aegis Extension] onBeforeNavigate eval failed:', e);
    }
  });

  // Also intercept SPA navigations
  chrome.webNavigation.onHistoryStateUpdated.addListener(async (details) => {
    if (details.frameId !== 0) return;
    const url = details.url;
    if (!url.startsWith('http://') && !url.startsWith('https://')) return;
    if (url.includes('block.html')) return;

    const token = await ensureValidToken();
    if (!token) return;

    try {
      const res = await fetch(`${API_BASE_URL}/evaluate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
        body: JSON.stringify({ url, component: COMPONENT_ID, timestamp: new Date().toISOString() })
      });

      if (!res.ok) return;
      const evalRes: EvaluationResponse = await res.json();

      if (evalRes.decision === 'Block' || (evalRes.decision as any) === 1 || evalRes.action === 'Block') {
        const blockPageUrl = chrome.runtime.getURL(`block.html?url=${encodeURIComponent(url)}&reason=${encodeURIComponent(evalRes.reason)}`);
        chrome.tabs.update(details.tabId, { url: blockPageUrl }).catch(e => console.error('[Aegis] SPA redirect failed:', e));
      }
    } catch (e) {
      console.warn('[Aegis Extension] onHistoryStateUpdated eval failed:', e);
    }
  });
}

// Runtime message listener from content scripts (fallback evaluation)
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'EVALUATE_PAGE') {
    const tabId = sender.tab?.id || 0;
    evaluateUrl(tabId, message.url, message.title)
      .then((res) => sendResponse(res))
      .catch((err) => sendResponse({ decision: 'Allow', reason: err.message }));
    return true; // Keep channel open for async response
  }
});

async function performHandshake(): Promise<string | null> {
  try {
    const timestamp = Math.floor(Date.now() / 1000);
    const payloadStr = `${COMPONENT_ID}:${timestamp}`;
    const hmacSignature = await computeHmacHex(PRE_SHARED_SECRET, payloadStr);

    const reqPayload: HandshakePayload = { componentId: COMPONENT_ID, timestamp, hmacSignature };

    const res = await fetch(`${API_BASE_URL}/handshake`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(reqPayload)
    });

    if (!res.ok) throw new Error(`Handshake failed with status ${res.status}`);

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
    console.warn('[Aegis Extension] Handshake failed:', err);
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
  if (url.startsWith('chrome-extension://') || url.startsWith('edge-extension://') || url.includes('block.html')) {
    return { decision: 'Allow', reason: 'Extension page internal', severity: 'Info', action: 'Allow', componentState: 'Protected' };
  }

  const token = await ensureValidToken();
  if (!token) {
    return { decision: 'Allow', reason: 'Offline fallback', severity: 'Info', action: 'Allow', componentState: 'Degraded' };
  }

  try {
    const evalPayload: EvaluationPayload = { url, title, component: COMPONENT_ID, timestamp: new Date().toISOString() };

    const res = await fetch(`${API_BASE_URL}/evaluate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
      body: JSON.stringify(evalPayload)
    });

    if (!res.ok) throw new Error(`Evaluate API returned status ${res.status}`);

    const evalRes: EvaluationResponse = await res.json();

    if (evalRes.decision === 'Block' || (evalRes.decision as any) === 1 || evalRes.action === 'Block') {
      console.warn('[Aegis Extension] BLOCK DECISION FOR TAB:', tabId, url);
      const blockPageUrl = chrome.runtime.getURL(`block.html?url=${encodeURIComponent(url)}&reason=${encodeURIComponent(evalRes.reason)}`);
      if (tabId > 0) {
        chrome.tabs.update(tabId, { url: blockPageUrl }).catch(e => console.error('Tab redirect failed:', e));
      }
    }

    return evalRes;
  } catch (err: any) {
    console.warn('[Aegis Extension] Evaluation API failed:', err);
    return { decision: 'Allow', reason: 'API error fallback', severity: 'Warning', action: 'Allow', componentState: 'Degraded' };
  }
}

async function computeHmacHex(secret: string, text: string): Promise<string> {
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey('raw', enc.encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const signature = await crypto.subtle.sign('HMAC', key, enc.encode(text));
  return Array.from(new Uint8Array(signature))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();
}
