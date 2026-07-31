import { AegisStorageState } from './types';

const API_BASE_URL = 'http://127.0.0.1:9443';

document.addEventListener('DOMContentLoaded', async () => {
  const dashboardBtn = document.getElementById('open-dashboard-btn') as HTMLButtonElement;
  const syncBtn = document.getElementById('sync-btn') as HTMLButtonElement;
  const apiStatusEl = document.getElementById('api-status') as HTMLElement;
  const tokenStatusEl = document.getElementById('token-status') as HTMLElement;
  const protectionBadge = document.getElementById('protection-badge') as HTMLElement;
  const protectionText = document.getElementById('protection-text') as HTMLElement;

  if (dashboardBtn) {
    dashboardBtn.addEventListener('click', () => {
      chrome.tabs.create({ url: `${API_BASE_URL}/` });
    });
  }

  if (syncBtn) {
    syncBtn.addEventListener('click', async () => {
      syncBtn.disabled = true;
      syncBtn.innerText = '⏳ Checking connection...';
      await checkStatus();
      syncBtn.disabled = false;
      syncBtn.innerHTML = '<span>🔄 Refresh & Verify Sync</span>';
    });
  }

  async function checkStatus() {
    try {
      const storage = await chrome.storage.local.get(['token', 'serviceStatus']) as AegisStorageState;
      
      // Ping background health endpoint
      const res = await fetch(`${API_BASE_URL}/health`, { method: 'GET' });
      if (res.ok) {
        apiStatusEl.innerText = '● Connected';
        apiStatusEl.className = 'val ok';
        protectionBadge.style.background = 'rgba(16, 185, 129, 0.15)';
        protectionBadge.style.borderColor = 'rgba(16, 185, 129, 0.3)';
        protectionBadge.style.color = '#10B981';
        protectionText.innerText = 'Protected';

        if (storage.token) {
          tokenStatusEl.innerText = 'Valid HMAC Session';
        } else {
          tokenStatusEl.innerText = 'Connecting...';
        }
      } else {
        throw new Error('Health check non-200');
      }
    } catch (err) {
      apiStatusEl.innerText = '○ Offline (Check Service)';
      apiStatusEl.className = 'val err';
      tokenStatusEl.innerText = 'Fallback Mode';
      protectionBadge.style.background = 'rgba(248, 113, 113, 0.15)';
      protectionBadge.style.borderColor = 'rgba(248, 113, 113, 0.3)';
      protectionBadge.style.color = '#F87171';
      protectionText.innerText = 'Service Offline';
    }
  }

  await checkStatus();
});
