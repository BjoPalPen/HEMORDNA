// Toggles document.documentElement.dataset.scrolled so CSS elsewhere (NavMenu.razor.css's
// compact pill, Pages/MinDag.razor.css's/Hushall.razor.css's collapsed header row) can react to
// scroll position without its own per-page listener. CSS's animation-timeline: scroll() would
// remove the need for this entirely, but lacks Firefox support - a data attribute plus plain CSS
// transitions works everywhere.
const threshold = 24;

let rafId = null;

function onScroll() {
    if (rafId !== null) {
        return;
    }

    rafId = requestAnimationFrame(() => {
        rafId = null;

        if (window.scrollY > threshold) {
            document.documentElement.dataset.scrolled = '';
        } else {
            delete document.documentElement.dataset.scrolled;
        }
    });
}

export function attach() {
    window.addEventListener('scroll', onScroll, { passive: true });
}

export function dispose() {
    window.removeEventListener('scroll', onScroll);

    if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
    }

    delete document.documentElement.dataset.scrolled;
}
