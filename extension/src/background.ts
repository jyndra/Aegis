import { HandshakePayload } from './types';

console.log('[Aegis Extension] Service worker initialized.');

chrome.runtime.onInstalled.addListener(() => {
  console.log('[Aegis Extension] Extension installed. Setting up alarms...');
  chrome.alarms.create('aegis-heartbeat', { periodInMinutes: 1 });
});

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'aegis-heartbeat') {
    console.log('[Aegis Extension] Executing scheduled heartbeat...');
  }
});
