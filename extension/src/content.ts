console.log('[Aegis Extension] Content script injected at document_start.');

let lastEvaluatedUrl = '';
let lastEvaluatedTitle = '';

function inspectAndRequestEvaluation(): void {
  const currentUrl = window.location.href;
  const pageTitle = document.title;

  // Ignore extension internal pages (e.g. block.html)
  if (currentUrl.startsWith('chrome-extension://') || currentUrl.startsWith('edge-extension://') || currentUrl.includes('block.html')) {
    return;
  }

  // Avoid duplicate evaluation calls if URL and Title are unchanged
  if (currentUrl === lastEvaluatedUrl && pageTitle === lastEvaluatedTitle) {
    return;
  }

  lastEvaluatedUrl = currentUrl;
  lastEvaluatedTitle = pageTitle;

  chrome.runtime.sendMessage(
    {
      type: 'EVALUATE_PAGE',
      url: currentUrl,
      title: pageTitle
    },
    (response) => {
      if (chrome.runtime.lastError) {
        // Channel closed or background worker sleeping
        return;
      }
      if (response && (response.decision === 'Block' || (response.decision as any) === 1 || response.action === 'Block')) {
        console.warn('[Aegis Extension] Block decision received for page:', currentUrl);
        
        try {
          if (document && document.documentElement) {
            document.documentElement.innerHTML = `
              <div style="background: #0f172a; color: #f8fafc; height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; font-family: system-ui, sans-serif; z-index: 99999999; position: fixed; top:0; left:0; width: 100vw; margin: 0; padding: 0;">
                <h1 style="font-size: 3.5rem; margin-bottom: 1rem; color: #ef4444; font-weight: 800;">🛡️ Blocked by Aegis</h1>
                <p style="font-size: 1.35rem; color: #cbd5e1; max-width: 600px; text-align: center;">${response.reason || 'Content protection threshold exceeded'}</p>
                <p style="font-size: 0.95rem; color: #64748b; margin-top: 2.5rem;">Redirecting to safe zone...</p>
              </div>
            `;
          }
        } catch (e) {
          console.warn('[Aegis Extension] Overlay injection error:', e);
        }

        const blockUrl = chrome.runtime.getURL(`block.html?url=${encodeURIComponent(currentUrl)}&reason=${encodeURIComponent(response.reason || 'Content threshold exceeded')}`);
        if (window.location.href !== blockUrl) {
          window.location.replace(blockUrl);
          window.location.href = blockUrl;
        }
      }
    }
  );
}

// SPA Routing wrapper (history.pushState & history.replaceState) & MutationObserver on <title>
function wrapHistoryAndTitleObserver(): void {
  const origPushState = history.pushState;
  const origReplaceState = history.replaceState;

  history.pushState = function (...args) {
    origPushState.apply(this, args);
    setTimeout(inspectAndRequestEvaluation, 100);
  };

  history.replaceState = function (...args) {
    origReplaceState.apply(this, args);
    setTimeout(inspectAndRequestEvaluation, 100);
  };

  window.addEventListener('popstate', () => {
    setTimeout(inspectAndRequestEvaluation, 100);
  });

  // Watch for asynchronous <title> DOM mutations (common in React/Next.js/Vue SPAs)
  const titleEl = document.querySelector('title');
  if (titleEl) {
    const observer = new MutationObserver(() => {
      inspectAndRequestEvaluation();
    });
    observer.observe(titleEl, { childList: true, characterData: true, subtree: true });
  }
}

wrapHistoryAndTitleObserver();

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', inspectAndRequestEvaluation);
} else {
  inspectAndRequestEvaluation();
}
