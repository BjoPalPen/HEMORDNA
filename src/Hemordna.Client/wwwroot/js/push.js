// Bridges the browser's Push API to Hemordna.Client.Services.PushNotificationService and the
// /api/households/{id}/push endpoints. Keys travel as base64url strings, matching what the
// server's WebPush package expects (WebPush.PushSubscription's p256dh/auth constructor args).
window.hemordnaPush = (function () {
    function base64UrlToUint8Array(base64url) {
        const padding = "=".repeat((4 - (base64url.length % 4)) % 4);
        const base64 = (base64url + padding).replace(/-/g, "+").replace(/_/g, "/");
        const raw = atob(base64);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) {
            bytes[i] = raw.charCodeAt(i);
        }
        return bytes;
    }

    function bufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = "";
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function isSupported() {
        return !!(window.navigator.serviceWorker && window.PushManager && window.Notification);
    }

    // iOS requires the app to be added to the home screen before push works at all - Safari
    // in an ordinary browser tab has no push support there, silently. This is what lets
    // Installningar.razor say so once, calmly, instead of a subscribe button that just does
    // nothing when pressed. Not iOS-version-gated: every iOS Safari tab needs the same nudge.
    function isIosSafariTab() {
        const ua = window.navigator.userAgent;
        // iPadOS reports as "Macintosh" with touch support, unlike a real Mac.
        const isIosDevice = /iPad|iPhone|iPod/.test(ua)
            || (ua.indexOf("Macintosh") !== -1 && "ontouchend" in document);
        const isSafari = /^((?!chrome|android|crios|fxios|edgios).)*safari/i.test(ua);
        const isStandalone = window.navigator.standalone === true
            || (window.matchMedia && window.matchMedia("(display-mode: standalone)").matches);

        return isIosDevice && isSafari && !isStandalone;
    }

    function getPermission() {
        return window.Notification ? window.Notification.permission : "unsupported";
    }

    async function getSubscription() {
        if (!isSupported()) {
            return { endpoint: null };
        }

        const registration = await navigator.serviceWorker.getRegistration();
        const subscription = registration ? await registration.pushManager.getSubscription() : null;
        return { endpoint: subscription ? subscription.endpoint : null };
    }

    // Must be called directly from a click handler with no prior await - iOS only honours the
    // permission prompt as a direct result of a real button press (see Installningar.razor).
    async function subscribe(vapidPublicKey) {
        try {
            const permission = await Notification.requestPermission();

            if (permission !== "granted") {
                return { success: false, error: "Behörighet nekades." };
            }

            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: base64UrlToUint8Array(vapidPublicKey)
            });

            const p256dh = subscription.getKey("p256dh");
            const auth = subscription.getKey("auth");

            return {
                success: true,
                endpoint: subscription.endpoint,
                p256dh: bufferToBase64Url(p256dh),
                auth: bufferToBase64Url(auth)
            };
        } catch (error) {
            return { success: false, error: error && error.message ? error.message : "Okänt fel." };
        }
    }

    async function unsubscribe() {
        try {
            const registration = await navigator.serviceWorker.getRegistration();
            const subscription = registration ? await registration.pushManager.getSubscription() : null;

            if (!subscription) {
                return { success: true, endpoint: null };
            }

            const endpoint = subscription.endpoint;
            await subscription.unsubscribe();
            return { success: true, endpoint };
        } catch (error) {
            return { success: false, error: error && error.message ? error.message : "Okänt fel." };
        }
    }

    return { isSupported, isIosSafariTab, getPermission, getSubscription, subscribe, unsubscribe };
})();
