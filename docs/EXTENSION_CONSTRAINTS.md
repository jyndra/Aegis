# Aegis Browser Extension — MV3 Constraints and Workarounds

## 1. Purpose

This document catalogues Manifest V3 limitations that directly affect Aegis and defines the workarounds used in the extension design.

## 2. Service worker lifecycle

### Constraint
MV3 service workers are **ephemeral**. Chrome will terminate the service worker after ~30 seconds of inactivity (or 5 minutes of sustained activity). There is no persistent background page.

### Impact on Aegis
- The extension cannot maintain a persistent WebSocket or long-polling connection to the local service.
- Heartbeat/handshake state is lost on termination.
- In-memory caches (policy, keyword lists) are cleared on termination.

### Workaround
- Use the `chrome.alarms` API for periodic heartbeats (minimum interval: 1 minute in MV3).
- On every service worker wake-up, check `chrome.storage.local` for cached session token and policy.
- If the cached token is expired or missing, perform a fresh handshake with the local API.
- Store the last known service health status in `chrome.storage.local` so the popup/UI can display it even when the service worker is asleep.

## 3. Declarative net request limits

### Constraint
`chrome.declarativeNetRequest` has rule count limits:
- **Static rules:** up to 330,000 across all static rulesets (Chrome 120+), but only 50 enabled rulesets.
- **Dynamic rules:** up to 30,000.
- **Session rules:** up to 5,000 (cleared when the browser closes).

### Impact on Aegis
- Large blocklists (100K+ domains) must use static rulesets bundled with the extension, not dynamic rules.
- Policy updates from the service that add block rules at runtime are limited to 30K dynamic rules.
- Cannot exceed 5K session-scoped rules.

### Workaround
- Bundle the primary domain blocklist as a static ruleset in the extension package. Update it via extension updates.
- Use dynamic rules for custom user-added blocks and real-time policy additions from the service.
- If the dynamic rule limit is approached, batch oldest custom rules into a static ruleset update.
- Use the content script + local API fallback for keyword/regex blocking that cannot be expressed as declarativeNetRequest rules (since DNR only matches URL patterns, not page content).

## 4. No synchronous request blocking

### Constraint
MV3 removed the synchronous blocking variant of `chrome.webRequest.onBeforeRequest`. All blocking must go through `declarativeNetRequest` or async handlers.

### Impact on Aegis
- Domain blocking must be pre-registered as declarativeNetRequest rules; it cannot be decided synchronously per-request.
- Keyword/content-based decisions cannot block the initial navigation — they can only redirect or close the tab after the fact.

### Workaround
- Pre-populate declarativeNetRequest rules for all known blocked domains (covers 95%+ of cases).
- For keyword/content-based blocking, use a content script that:
  1. Runs at `document_start` or `document_idle`.
  2. Scans page title, URL, and visible text.
  3. If a match is found, immediately replaces the page body with the block page or navigates to the local block page.
- Accept a brief flash of content before the content script blocks — this is an inherent MV3 limitation. Minimize it by injecting at `document_start` and hiding the body until scanned.

## 5. Content script injection timing

### Constraint
Content scripts injected via `manifest.json` run at `document_start`, `document_idle`, or `document_end`. None of these reliably catch SPA (single-page application) route changes (e.g., React Router, Next.js navigation).

### Impact on Aegis
- A user navigating within Reddit, Twitter, or a search engine via SPA routing may not trigger a new content script injection.
- Keyword scanning that runs only once on page load will miss dynamically loaded content.

### Workaround
- Use `MutationObserver` in the content script to watch for DOM changes continuously.
- Debounce the observer callback to 500ms to avoid jank.
- Also listen to `chrome.webNavigation.onHistoryStateUpdated` in the service worker to detect SPA navigations and re-evaluate the URL.
- Limit DOM scanning scope: scan `document.title`, visible text nodes in the main content area, and `meta` tags. Do not scan the entire DOM tree.

## 6. Cross-origin fetch from service worker

### Constraint
The service worker can make `fetch()` calls to `localhost`, but must declare it in `host_permissions` in `manifest.json`.

### Impact on Aegis
- The extension must declare `"host_permissions": ["https://127.0.0.1:9443/*"]` (or the configured port).
- The port is not known at build time if it is configurable.

### Workaround
- Use a fixed default port (9443) in `host_permissions`.
- If the port must be configurable, use `"optional_host_permissions"` and request at runtime — but this adds UX friction (permission prompt).
- Recommended: use the fixed default port and document it as non-configurable for the extension handshake.

## 7. Storage quotas

### Constraint
`chrome.storage.local` has a 10 MB quota by default (can be increased with `"unlimitedStorage"` permission).

### Impact on Aegis
- Cached policy data, keyword lists, and session state must fit within 10 MB, or the extension must request `unlimitedStorage`.

### Workaround
- Request `"unlimitedStorage"` permission in the manifest.
- In practice, Aegis extension storage should be well under 10 MB (session tokens, policy hash, cached keyword list, health state).

## 8. Extension update path

### Constraint
Chrome auto-updates extensions from the Chrome Web Store. Side-loaded (developer mode) extensions do not auto-update. Policy-installed extensions update per the policy `update_url`.

### Impact on Aegis
- If the extension is force-installed via registry policy, a local `update_url` can point to a manifest served by the Aegis service for local updates.
- If distributed via the Chrome Web Store, updates go through Google's review process.

### Workaround
- For v1, support both distribution methods:
  1. Chrome Web Store (preferred for SmartScreen trust and auto-update).
  2. Local policy install with a service-hosted update manifest (for offline/private use).
- Document both paths in the installer.

## 9. Summary of extension architecture

```
┌─────────────────────────────────────────────────┐
│ Service Worker (ephemeral)                      │
│  - Alarms-based heartbeat (1 min)              │
│  - Handshake with local API on wake            │
│  - declarativeNetRequest rule management       │
│  - webNavigation listener for SPA detection    │
│  - chrome.storage.local for state persistence  │
└──────────────┬──────────────────────────────────┘
               │ messages
┌──────────────▼──────────────────────────────────┐
│ Content Script (per tab)                        │
│  - Runs at document_start                      │
│  - MutationObserver for SPA content changes    │
│  - Keyword/title/URL scanning                  │
│  - Block page injection on match               │
│  - Debounced (500ms) DOM scanning              │
└──────────────┬──────────────────────────────────┘
               │ fetch (https://127.0.0.1:9443)
┌──────────────▼──────────────────────────────────┐
│ Aegis Local Service API                         │
│  - Handshake / token exchange                  │
│  - Policy sync                                 │
│  - Evaluate endpoint for content decisions     │
│  - Health reporting                            │
└─────────────────────────────────────────────────┘
```
