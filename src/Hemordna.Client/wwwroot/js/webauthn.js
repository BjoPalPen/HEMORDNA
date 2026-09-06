// Bridges the browser's WebAuthn API to the JSON shape Fido2NetLib expects on the server -
// see PasskeyEndpoints.cs and Hemordna.Client.Services.WebAuthnClient. Byte fields travel as
// base64url strings both ways, matching Fido2NetLib's own Base64UrlConverter.
window.hemordnaWebAuthn = (function () {
    function base64UrlToBuffer(base64url) {
        const padding = "=".repeat((4 - (base64url.length % 4)) % 4);
        const base64 = (base64url + padding).replace(/-/g, "+").replace(/_/g, "/");
        const raw = atob(base64);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) {
            bytes[i] = raw.charCodeAt(i);
        }
        return bytes.buffer;
    }

    function bufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = "";
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function isAvailable() {
        return !!window.PublicKeyCredential;
    }

    function withDecodedId(descriptor) {
        return { ...descriptor, id: base64UrlToBuffer(descriptor.id) };
    }

    async function register(optionsJson) {
        const options = JSON.parse(optionsJson);
        options.challenge = base64UrlToBuffer(options.challenge);
        options.user.id = base64UrlToBuffer(options.user.id);
        if (options.excludeCredentials) {
            options.excludeCredentials = options.excludeCredentials.map(withDecodedId);
        }

        const credential = await navigator.credentials.create({ publicKey: options });

        return JSON.stringify({
            id: credential.id,
            rawId: bufferToBase64Url(credential.rawId),
            type: credential.type,
            response: {
                attestationObject: bufferToBase64Url(credential.response.attestationObject),
                clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON)
            }
        });
    }

    async function authenticate(optionsJson) {
        const options = JSON.parse(optionsJson);
        options.challenge = base64UrlToBuffer(options.challenge);
        if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(withDecodedId);
        }

        const credential = await navigator.credentials.get({ publicKey: options });

        return JSON.stringify({
            id: credential.id,
            rawId: bufferToBase64Url(credential.rawId),
            type: credential.type,
            response: {
                clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON),
                authenticatorData: bufferToBase64Url(credential.response.authenticatorData),
                signature: bufferToBase64Url(credential.response.signature),
                userHandle: credential.response.userHandle
                    ? bufferToBase64Url(credential.response.userHandle)
                    : null
            }
        });
    }

    return { isAvailable, register, authenticate };
})();
