// Development service worker. It is registered so the install path is exercised while
// developing, but it deliberately caches nothing - a stale shell during development costs
// more than an offline start is worth. The published build uses service-worker.published.js.
self.addEventListener('fetch', () => { });

// Push notifications need a real, activated service worker regardless of environment - a
// developer testing the push chain locally needs this here too, not just in production.
// Deliberately minimal: title/body/url only, no notification types, no badge, no actions -
// see CLAUDE.md's scope note for this task and Hemordna.Infrastructure/Services/WebPushSender.cs,
// which is the only thing that ever writes this payload.
self.addEventListener('push', event => {
    event.waitUntil((async () => {
        let data = {};

        if (event.data) {
            try {
                data = event.data.json() || {};
            } catch {
                data = {};
            }
        }

        const title = data.title || 'Hemordna';
        const url = typeof data.url === 'string' && data.url.length > 0 ? data.url : '/';

        await self.registration.showNotification(title, {
            body: data.body || '',
            icon: '/icon-192.png',
            badge: '/icon-192.png',
            data: { url },
            tag: url
        });
    })());
});

// Opens (or focuses) the app when the notification is clicked.
self.addEventListener('notificationclick', event => {
    event.notification.close();

    const rawUrl = (event.notification.data && event.notification.data.url) || '/';
    const targetUrl = new URL(rawUrl, self.location.origin).href;

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(async clientList => {
            for (const client of clientList) {
                if (!client.url.startsWith(self.location.origin) || !('focus' in client)) {
                    continue;
                }

                // iOS can ignore navigate() on an already-open app - see BowlingPlatform's
                // service-worker.js, which this mirrors. Route via postMessage too, so a future
                // client-side listener can get there even when navigate() is a no-op - nothing
                // listens for it yet (every push today links to "/", which navigate() already
                // reaches), but wiring the message now costs nothing and saves a second pass
                // once push is linked to a real destination (a reminder, a task).
                client.postMessage({ type: 'OPEN_FROM_NOTIFICATION', url: targetUrl });
                await client.focus();

                if ('navigate' in client) {
                    try {
                        await client.navigate(targetUrl);
                    } catch {
                        // iOS/Safari can throw on navigate(); the postMessage above still lands.
                    }
                }

                return;
            }

            if (clients.openWindow) {
                return clients.openWindow(targetUrl);
            }
        })
    );
});
