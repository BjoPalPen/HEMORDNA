// "Skriv ut" (MinDag.razor) - the layout itself switches via @media print in app.css; this just
// triggers the browser's own print dialog.
export function printPage() {
    window.print();
}

// The print-only view is built by MinDag.razor's own Razor markup, but only rendered while
// _isPrinting is true - matchMedia('print'), not just @media print in CSS, because Blazor's
// print-only block must not exist in the DOM at all outside of printing. It carries the same
// task names/room headings the live list already does, and Playwright's locators (GetByText,
// "h1 with text X", ...) match hidden elements just as readily as visible ones - a
// display:none-only block would silently turn "the only element with this text" false for
// every other test that happens to visit Idag, not just print-specific ones.
export function watchPrintMedia(dotNetRef) {
    const mediaQueryList = window.matchMedia('print');
    const handler = (e) => dotNetRef.invokeMethodAsync('OnPrintMediaChanged', e.matches);
    mediaQueryList.addEventListener('change', handler);

    return {
        dispose: () => mediaQueryList.removeEventListener('change', handler),
    };
}
