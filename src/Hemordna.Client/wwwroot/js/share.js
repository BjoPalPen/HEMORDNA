// "Bjud in" (Hushall.razor): use the platform share sheet where available, otherwise fall back
// to the clipboard - the caller shows its own confirmation either way.
export async function shareOrCopy(title, text) {
    if (navigator.share) {
        try {
            await navigator.share({ title, text });
            return true;
        } catch (error) {
            // AbortError just means the person cancelled the share sheet - not a failure to
            // report, and not a reason to fall through to clipboard either.
            if (error?.name === 'AbortError') {
                return true;
            }
        }
    }

    await navigator.clipboard.writeText(text);
    return false;
}
