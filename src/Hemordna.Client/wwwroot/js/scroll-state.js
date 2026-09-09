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

// A Blazor route change never reloads the page, so the browser never runs its own "new page"
// scroll reset - a scrollY left over from whatever page the member was on before (even a few px,
// enough to cross the threshold above without looking "scrolled" at all) would otherwise survive
// straight into the newly rendered page. Called from MainLayout on every navigation
// (Support/ScrollState.cs) - moves the actual scroll position, not just the attribute, so the
// underlying state is genuinely correct rather than a flag masking a stale scrollY.
export function reset() {
    window.scrollTo(0, 0);
    delete document.documentElement.dataset.scrolled;
}

export function dispose() {
    window.removeEventListener('scroll', onScroll);

    if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
    }

    delete document.documentElement.dataset.scrolled;
}
