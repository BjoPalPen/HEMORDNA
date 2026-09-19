// "Utseende" (Installningar.razor) - a per-device backdrop photo, same family as
// hemordna.theme/hemordna.calm (theme.js, calm-screen.js), but a photo does not fit
// localStorage's ~5 MB quota - mobile photos alone are routinely 3-10 MB - so this one lives in
// IndexedDB instead. index.html runs this module's apply() as early as possible after first
// paint (it cannot be synchronous like theme/calm, since reading a Blob out of IndexedDB is
// inherently async) - keep DB_NAME/STORE_NAME/KEY in sync with index.html if any of them change.
const DB_NAME = 'hemordna';
const STORE_NAME = 'backdrop';
const KEY = 'image';
const MAX_DIMENSION = 1600;
const JPEG_QUALITY = 0.82;
const STORAGE_ERROR = 'Bilden gick inte att sparas på den här enheten.';

// The object URL currently applied to --backdrop-image, so it can be revoked when replaced or
// cleared - object URLs are never garbage collected on their own.
let currentObjectUrl = null;

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, 1);
        request.onupgradeneeded = () => {
            if (!request.result.objectStoreNames.contains(STORE_NAME)) {
                request.result.createObjectStore(STORE_NAME);
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function readBlob() {
    const db = await openDatabase();
    try {
        return await new Promise((resolve, reject) => {
            const transaction = db.transaction(STORE_NAME, 'readonly');
            const request = transaction.objectStore(STORE_NAME).get(KEY);
            request.onsuccess = () => resolve(request.result ?? null);
            request.onerror = () => reject(request.error);
        });
    } finally {
        db.close();
    }
}

async function writeBlob(blob) {
    const db = await openDatabase();
    try {
        await new Promise((resolve, reject) => {
            const transaction = db.transaction(STORE_NAME, 'readwrite');
            transaction.objectStore(STORE_NAME).put(blob, KEY);
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });
    } finally {
        db.close();
    }
}

async function deleteBlob() {
    const db = await openDatabase();
    try {
        await new Promise((resolve, reject) => {
            const transaction = db.transaction(STORE_NAME, 'readwrite');
            transaction.objectStore(STORE_NAME).delete(KEY);
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });
    } finally {
        db.close();
    }
}

// Decodes, downscales to at most MAX_DIMENSION px on the longest side and re-encodes as JPEG.
// createImageBitmap with imageOrientation: 'from-image' bakes in EXIF rotation from phone
// cameras correctly; browsers without it fall back to <img>, which already applies EXIF
// rotation on its own everywhere createImageBitmap is missing, so no manual matrix is needed.
async function resizeToJpeg(file) {
    let bitmap = null;
    let source;
    let width;
    let height;
    let revoke = () => {};

    if (typeof createImageBitmap === 'function') {
        bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
        source = bitmap;
        width = bitmap.width;
        height = bitmap.height;
    } else {
        const objectUrl = URL.createObjectURL(file);
        revoke = () => URL.revokeObjectURL(objectUrl);
        try {
            const image = await new Promise((resolve, reject) => {
                const element = new Image();
                element.onload = () => resolve(element);
                element.onerror = () => reject(new Error('image-decode-failed'));
                element.src = objectUrl;
            });
            source = image;
            width = image.naturalWidth;
            height = image.naturalHeight;
        } catch (error) {
            revoke();
            throw error;
        }
    }

    try {
        const scale = Math.min(1, MAX_DIMENSION / Math.max(width, height));
        const targetWidth = Math.max(1, Math.round(width * scale));
        const targetHeight = Math.max(1, Math.round(height * scale));

        const canvas = document.createElement('canvas');
        canvas.width = targetWidth;
        canvas.height = targetHeight;
        const context = canvas.getContext('2d');
        if (!context) {
            throw new Error('canvas-2d-unavailable');
        }
        context.drawImage(source, 0, 0, targetWidth, targetHeight);

        return await new Promise((resolve, reject) => {
            canvas.toBlob((result) => {
                if (result) {
                    resolve(result);
                } else {
                    reject(new Error('canvas-encode-failed'));
                }
            }, 'image/jpeg', JPEG_QUALITY);
        });
    } finally {
        if (bitmap) {
            bitmap.close();
        }
        revoke();
    }
}

// Called from Installningar with the <input type="file"> element itself (Blazor marshals an
// ElementReference to the real DOM node), or directly with a File - never a byte array, so a
// multi-megabyte photo never has to cross the JS interop boundary as a .NET array.
export async function set(inputOrFile) {
    const file = inputOrFile instanceof File ? inputOrFile : inputOrFile?.files?.[0];
    if (!file) {
        return;
    }

    let blob;
    try {
        blob = await resizeToJpeg(file);
    } catch {
        throw new Error(STORAGE_ERROR);
    }

    try {
        await writeBlob(blob);
    } catch {
        // Quota exceeded, storage blocked (private browsing) or any other IndexedDB failure -
        // the app must not break, but Installningar needs a message it can actually show.
        throw new Error(STORAGE_ERROR);
    }

    await apply();
}

// Whether a backdrop image exists - never the image itself, which stays out of .NET entirely.
export async function get() {
    try {
        const blob = await readBlob();
        return blob != null;
    } catch {
        return false;
    }
}

export async function clear() {
    try {
        await deleteBlob();
    } catch {
        // Storage already gone or blocked - nothing left to remove; still un-apply below.
    }

    unapply();
}

// Applies whatever is currently stored, if anything - used both by Installningar right after
// set()/clear() and by index.html's early best-effort boot read.
export async function apply() {
    let blob;
    try {
        blob = await readBlob();
    } catch {
        return;
    }

    if (!blob) {
        unapply();
        return;
    }

    if (currentObjectUrl) {
        URL.revokeObjectURL(currentObjectUrl);
    }

    currentObjectUrl = URL.createObjectURL(blob);
    document.documentElement.style.setProperty('--backdrop-image', `url("${currentObjectUrl}")`);
    document.documentElement.setAttribute('data-backdrop', '');
}

function unapply() {
    if (currentObjectUrl) {
        URL.revokeObjectURL(currentObjectUrl);
        currentObjectUrl = null;
    }
    document.documentElement.style.removeProperty('--backdrop-image');
    document.documentElement.removeAttribute('data-backdrop');
}
