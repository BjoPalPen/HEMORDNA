// Development service worker. It is registered so the install path is exercised while
// developing, but it deliberately caches nothing - a stale shell during development costs
// more than an offline start is worth. The published build uses service-worker.published.js.
self.addEventListener('fetch', () => { });
