// "Utseende" (Installningar.razor) - a per-device choice, so localStorage rather than the
// server. index.html runs the same STORAGE_KEY/attribute logic inline, synchronously, before
// app.css loads, to avoid a flash of the wrong theme on first paint - keep the two in sync if
// this ever changes.
const STORAGE_KEY = 'hemordna.theme';

export function get() {
    try {
        return localStorage.getItem(STORAGE_KEY) ?? 'system';
    } catch {
        return 'system';
    }
}

export function set(choice) {
    try {
        if (choice === 'light' || choice === 'dark') {
            localStorage.setItem(STORAGE_KEY, choice);
        } else {
            localStorage.removeItem(STORAGE_KEY);
        }
    } catch {
        // Private browsing / blocked storage - the choice just will not survive a reload.
    }

    apply(choice);
}

function apply(choice) {
    if (choice === 'light' || choice === 'dark') {
        document.documentElement.setAttribute('data-theme', choice);
    } else {
        document.documentElement.removeAttribute('data-theme');
    }
}
