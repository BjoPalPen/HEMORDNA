// "Jag börjar nu" (MinDag.razor) - a per-device, per-day marker for which single task someone is
// currently working on. Mirrors wwwroot/js/calm-screen.js/theme.js: localStorage rather than the
// server, since this is a property of the device/moment, not something other members should see.
const STORAGE_KEY = 'hemordna.started';

export function get() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

export function set(date, occurrenceId) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ date, occurrenceId }));
    } catch {
        // Private browsing / blocked storage - the choice just will not survive a reload.
    }
}

export function clear() {
    try {
        localStorage.removeItem(STORAGE_KEY);
    } catch {
        // Nothing to clean up if storage was never reachable in the first place.
    }
}
