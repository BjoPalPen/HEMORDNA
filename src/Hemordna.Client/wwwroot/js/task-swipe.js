// Swipe right = mark done, swipe left = move to tomorrow (Components/TaskListItem.razor). The
// check/move BUTTONS are the real, always-present controls for keyboard and screen readers -
// this is a pointer-only shortcut layered on top, never the only way to do either.
const threshold = 72;
const maxDrag = 120;

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
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    let startX = null;
    let dx = 0;
    let dragging = false;

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
        dx = 0;
        dragging = true;
        element.setPointerCapture(event.pointerId);
    }

    function onPointerMove(event) {
        if (!dragging || startX === null) {
            return;
        }

        dx = event.clientX - startX;

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

        if (dx > threshold) {
            confirm(element);
            dotNetRef.invokeMethodAsync('OnSwipeCompleteAsync');
        } else if (dx < -threshold) {
            dotNetRef.invokeMethodAsync('OnSwipeDeferAsync');
        }

        startX = null;
        dx = 0;
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
