// "Lugnare skärm" (Installningar.razor) - a per-device choice, so localStorage rather than the
// server - mirrors wwwroot/js/theme.js exactly, which see for the same reasoning. index.html
// runs the same STORAGE_KEY/attribute logic inline, synchronously, before app.css loads, so
// there is no flash of motion/blur on first paint - keep the two in sync if this ever changes.
const STORAGE_KEY = 'hemordna.calm';

export function get() {
    try {
        return localStorage.getItem(STORAGE_KEY) === '1';
    } catch {
        return false;
    }
}

export function set(enabled) {
    try {
        if (enabled) {
            localStorage.setItem(STORAGE_KEY, '1');
        } else {
            localStorage.removeItem(STORAGE_KEY);
        }
    } catch {
        // Private browsing / blocked storage - the choice just will not survive a reload.
    }

    apply(enabled);
}

function apply(enabled) {
    if (enabled) {
        document.documentElement.setAttribute('data-calm', '');
    } else {
        document.documentElement.removeAttribute('data-calm');
    }
}
