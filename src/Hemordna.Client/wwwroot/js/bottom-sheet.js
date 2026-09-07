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
