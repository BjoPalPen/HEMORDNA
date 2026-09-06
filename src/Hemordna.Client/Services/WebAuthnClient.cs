using Microsoft.JSInterop;

namespace Hemordna.Client.Services;

/// <summary>
/// Thin wrapper over wwwroot/js/webauthn.js. Options travel to the browser and credentials
/// travel back as opaque JSON strings - this layer does not parse them, it only carries them
/// between <see cref="HemordnaApiClient"/> and the browser's own WebAuthn implementation.
/// </summary>
public sealed class WebAuthnClient
{
    private readonly IJSRuntime _js;

    public WebAuthnClient(IJSRuntime js) => _js = js;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("hemordnaWebAuthn.isAvailable");
        }
        catch (Exception)
        {
            // A capability probe must never take its caller down with it - an unsupported
            // browser, a blocked script, or an interop hiccup should all just read as "no",
            // not crash the page that asked. See Installningar.razor's OnInitializedAsync,
            // which used to have exactly that failure mode.
            return false;
        }
    }

    /// <summary>Prompts for Face ID/Touch ID/Windows Hello to create a new passkey from a
    /// server-issued challenge. Returns <c>null</c> if the person cancels or it fails.</summary>
    public async Task<string?> RegisterAsync(string optionsJson)
    {
        try
        {
            return await _js.InvokeAsync<string>("hemordnaWebAuthn.register", optionsJson);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Prompts for Face ID/Touch ID/Windows Hello to satisfy a server-issued
    /// challenge with an existing passkey. Returns <c>null</c> if the person cancels or no
    /// matching passkey is on this device.</summary>
    public async Task<string?> AuthenticateAsync(string optionsJson)
    {
        try
        {
            return await _js.InvokeAsync<string>("hemordnaWebAuthn.authenticate", optionsJson);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
