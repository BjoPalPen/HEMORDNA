// "Stor text" (docs/DESIGN.md §7) - a per-device presentation preference, never a household
// setting. app.css scales --font-size-base from this attribute; this module is the only place
// that touches it.
export function apply(large) {
    if (large) {
        document.documentElement.setAttribute('data-text-size', 'large');
    } else {
        document.documentElement.removeAttribute('data-text-size');
    }
}
