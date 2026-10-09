// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Same update hand-off as service-worker.published.js, so "New version available – Reload" works in development too.
self.addEventListener('message', event => { if (event.data && event.data.type === 'SKIP_WAITING') self.skipWaiting(); });
