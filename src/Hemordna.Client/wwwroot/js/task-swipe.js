// Swipe right = mark done, swipe left = move to tomorrow (Components/TaskListItem.razor). The
// check/move BUTTONS are the real, always-present controls for keyboard and screen readers -
// this is a pointer-only shortcut layered on top, never the only way to do either. A gesture
// that turns out to be a vertical scroll (|dy| > |dx| within the first directionLockDistance
// px of movement) is abandoned outright, not just left under threshold - scrolling the day's
// list must never risk completing or deferring a task by accident.
const threshold = 96;
const maxDrag = 120;
const directionLockDistance = 12;

// A brief, non-essential confirmation (flash + haptic) after marking a task done - shared by
// the swipe gesture below and the ordinary tap-to-complete button (TaskListItem.razor calls
// this directly). Never the only signal that the task is done: the row still leaves the list
// once the day reloads. Skipped entirely - including the haptic - under prefers-reduced-motion,
// same as the drag animation below.
export function confirm(element) {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        return;
    }

    element.classList.add('task-confirm');
    // Matches the task-spring keyframes' own .32s duration (TaskListItem.razor.css) with a
    // little headroom, so the class is never removed mid-animation.
    setTimeout(() => element.classList.remove('task-confirm'), 340);
    // iOS Safari silently ignores navigator.vibrate - see docs/DESIGN.md §4a. Left in for the
    // platforms that do support it (most of Android); never the only confirmation signal.
    navigator.vibrate?.(10);
}

export function attach(element, dotNetRef) {
    // "Lugnare skärm" (Installningar.razor, Support/CalmScreen.cs) skips the same drag visual
    // as an OS-level prefers-reduced-motion would - it is a "rörelse" either way, just an
    // explicit per-device opt-in instead of an OS setting. Snapshotted once per attach(), same
    // as the OS check to its left: toggling either mid-session will not affect a row already
    // attached, only the next one a list reload creates - matches this file's existing
    // reduceMotion behaviour rather than adding new reactivity data-calm alone would not have.
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
        || document.documentElement.hasAttribute('data-calm');

    let startX = null;
    let startY = null;
    let dx = 0;
    let dy = 0;
    let dragging = false;
    // Undecided until the pointer has moved directionLockDistance px in some direction - then
    // fixed for the rest of this gesture, never re-evaluated (a swipe that starts vertical
    // stays abandoned even if it later drifts horizontal).
    let verticalScroll = false;

    function onPointerDown(event) {
        if (event.pointerType === 'mouse' && event.button !== 0) {
            return;
        }

        // A tap that starts on the check/expand button (or anything else focusable) is not the
        // start of a swipe - capturing the pointer here would steal it from the button's own
        // click handling.
        if (event.target.closest('button, a, input, select, textarea')) {
            return;
        }

        startX = event.clientX;
        startY = event.clientY;
        dx = 0;
        dy = 0;
        dragging = true;
        verticalScroll = false;
        element.setPointerCapture(event.pointerId);
    }

    function onPointerMove(event) {
        if (!dragging || startX === null) {
            return;
        }

        dx = event.clientX - startX;
        dy = event.clientY - startY;

        if (!verticalScroll && Math.max(Math.abs(dx), Math.abs(dy)) >= directionLockDistance) {
            if (Math.abs(dy) > Math.abs(dx)) {
                // A vertical scroll, not a swipe - let touch-action: pan-y (TaskListItem.razor.css)
                // handle it natively from here on; release capture so it is not fighting the
                // page's own scrolling for the rest of this gesture.
                verticalScroll = true;
                element.style.transform = '';
                element.releasePointerCapture(event.pointerId);
                return;
            }
        }

        if (verticalScroll) {
            return;
        }

        if (!reduceMotion) {
            const clamped = Math.max(-maxDrag, Math.min(maxDrag, dx));
            element.style.transform = `translateX(${clamped}px)`;
        }
    }

    function onPointerUp() {
        if (!dragging) {
            return;
        }

        dragging = false;
        element.style.transform = '';

        if (!verticalScroll) {
            if (dx > threshold) {
                confirm(element);
                dotNetRef.invokeMethodAsync('OnSwipeCompleteAsync');
            } else if (dx < -threshold) {
                dotNetRef.invokeMethodAsync('OnSwipeDeferAsync');
            }
        }

        startX = null;
        dx = 0;
        dy = 0;
    }

    element.addEventListener('pointerdown', onPointerDown);
    element.addEventListener('pointermove', onPointerMove);
    element.addEventListener('pointerup', onPointerUp);
    element.addEventListener('pointercancel', onPointerUp);

    return {
        dispose() {
            element.removeEventListener('pointerdown', onPointerDown);
            element.removeEventListener('pointermove', onPointerMove);
            element.removeEventListener('pointerup', onPointerUp);
            element.removeEventListener('pointercancel', onPointerUp);
        }
    };
}
