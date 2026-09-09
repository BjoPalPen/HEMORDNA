// "Hur är orken idag?" (MinDag.razor) - which of the three levels was picked today, remembered
// per-device only so the chip reads back correctly after a reload. The server never sees this -
// only the minutes SetAvailabilityAsync already sends it. Mirrors started-task.js exactly.
const STORAGE_KEY = 'hemordna.energy';

export function get() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

export function set(date, level) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ date, level }));
    } catch {
        // Private browsing / blocked storage - the choice just will not survive a reload.
    }
}
