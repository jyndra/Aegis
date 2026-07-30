function parseQueryParams(): { url: string; reason: string } {
  const params = new URLSearchParams(window.location.search);
  return {
    url: params.get('url') || 'Unknown URL',
    reason: params.get('reason') || 'Blocked by Aegis Protection Policy'
  };
}

function renderBlockInfo(): void {
  const info = parseQueryParams();

  const urlElem = document.getElementById('blocked-url');
  const reasonElem = document.getElementById('blocked-reason');

  if (urlElem) urlElem.textContent = info.url;
  if (reasonElem) reasonElem.textContent = info.reason;
}

document.addEventListener('DOMContentLoaded', renderBlockInfo);
