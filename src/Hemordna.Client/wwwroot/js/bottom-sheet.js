// Focus handling for BottomSheet.razor: move focus into the sheet when it opens, trap Tab
// inside it while open, and restore focus to whatever triggered it when it closes - see
// docs/DESIGN.md §10, focus is never left stranded or silently dropped.

let lastFocused = null;

export function focusIn(sheetElement) {
    lastFocused = document.activeElement;

    const focusable = sheetElement.querySelector(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');

    (focusable ?? sheetElement).focus();
}

export function focusBack() {
    if (lastFocused instanceof HTMLElement) {
        lastFocused.focus();
    }

    lastFocused = null;
}

// Drag-to-resize/dismiss shortcut on the handle and header - a genuine shortcut, never the only
// way: the "Stäng" button and Esc (BottomSheet.razor's own OnKeyDownAsync) still work regardless
// of whether this attaches. Mirrors task-swipe.js's own pointer-capture pattern.
const expandThreshold = 60;
const dismissThreshold = 80;
const maxDrag = 120;

export function attachDrag(sheetElement, handleElement, dotNetRef) {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const header = sheetElement.querySelector('.sheet-header');
    const targets = [handleElement, header].filter(Boolean);

    let startY = null;
    let dy = 0;
    let dragging = false;

    function onPointerDown(event) {
        if (event.pointerType === 'mouse' && event.button !== 0) {
            return;
        }

        // A tap on the close button (inside .sheet-header) is not the start of a drag.
        if (event.target.closest('button, a, input, select, textarea')) {
            return;
        }

        startY = event.clientY;
        dy = 0;
        dragging = true;
        event.currentTarget.setPointerCapture(event.pointerId);
    }

    function onPointerMove(event) {
        if (!dragging || startY === null) {
            return;
        }

        dy = event.clientY - startY;

        if (!reduceMotion) {
            // Only ever drags the sheet DOWN visually (0..maxDrag) - dragging up past the
            // expand threshold is a gesture, not a visual move, since "half" already shows the
            // sheet at its resting position.
            const clamped = Math.max(0, Math.min(maxDrag, dy));
            sheetElement.style.transform = `translateY(${clamped}px)`;
        }
    }

    function onPointerUp() {
        if (!dragging) {
            return;
        }

        dragging = false;
        sheetElement.style.transform = '';

        if (dy < -expandThreshold) {
            dotNetRef.invokeMethodAsync('ExpandAsync');
        } else if (dy > dismissThreshold) {
            dotNetRef.invokeMethodAsync('DismissAsync');
        }

        startY = null;
        dy = 0;
    }

    for (const target of targets) {
        target.addEventListener('pointerdown', onPointerDown);
        target.addEventListener('pointermove', onPointerMove);
        target.addEventListener('pointerup', onPointerUp);
        target.addEventListener('pointercancel', onPointerUp);
    }

    return {
        dispose() {
            for (const target of targets) {
                target.removeEventListener('pointerdown', onPointerDown);
                target.removeEventListener('pointermove', onPointerMove);
                target.removeEventListener('pointerup', onPointerUp);
                target.removeEventListener('pointercancel', onPointerUp);
            }
        }
    };
}
