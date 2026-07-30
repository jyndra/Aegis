console.log('[Aegis Extension] Content script injected at document_start.');

function scanPage(): void {
  const title = document.title;
  const url = window.location.href;
  // Stub for page evaluation
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', scanPage);
} else {
  scanPage();
}
