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
      if (response && response.decision === 'Block') {
        console.warn('[Aegis Extension] Block decision received for page:', currentUrl);
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
