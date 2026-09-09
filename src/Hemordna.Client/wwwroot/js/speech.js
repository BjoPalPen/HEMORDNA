// "Läs upp" (MinDag.razor, fokusläget) - a thin wrapper over the Web Speech API. No state kept
// here beyond the one in-flight utterance; a new speak() always cancels whatever was playing.

export function isAvailable() {
    return 'speechSynthesis' in window;
}

// Voices can load asynchronously in some browsers (an empty list right after startup does not
// mean "no voices ever") - an empty list here is read as "cannot tell yet", not "no Swedish
// voice", so a device that just has not finished loading its voice list does not get shown a
// caveat that may turn out to be wrong a moment later.
export function hasSwedishVoice() {
    if (!isAvailable()) {
        return false;
    }

    const voices = window.speechSynthesis.getVoices();
    if (voices.length === 0) {
        return true;
    }

    return voices.some(voice => voice.lang && voice.lang.toLowerCase().startsWith('sv'));
}

export function speak(dotNetRef, text) {
    window.speechSynthesis.cancel();

    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'sv-SE';
    utterance.rate = 0.95;
    utterance.onend = () => dotNetRef.invokeMethodAsync('OnSpeechEnded');
    utterance.onerror = () => dotNetRef.invokeMethodAsync('OnSpeechEnded');

    window.speechSynthesis.speak(utterance);
}

export function stop() {
    window.speechSynthesis.cancel();
}
