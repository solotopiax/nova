# GoogleSignInPlugin

`GoogleSignInPlugin` is Nova's Android Google Sign-In authentication plugin.
It implements `IAuthPlugin`, returns Nova `AuthResult` values, and stores the
most recent native user payload in `CurrentUserData`.

## Public API

```csharp
public sealed partial class GoogleSignInPlugin : SDKPluginBase, IAuthPlugin
{
    public override string Name { get; }
    public override int Priority { get; }
    public bool IsLoggedIn { get; }
    public GoogleSignInUserData CurrentUserData { get; }
    public UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);
    public UniTask LogoutAsync(CancellationToken ct = default);
}
```

| Member | Notes |
|---|---|
| `Name => "Google"` | SDK plugin identifier. |
| `Priority => 30` | Initialization priority. |
| `IsLoggedIn` | `true` only when both `UserId` and `IdToken` are present. |
| `CurrentUserData` | Latest login payload as `GoogleSignInUserData`. |
| `LoginAsync` | Starts the Android login flow and returns `AuthResult`. |
| `LogoutAsync` | Clears Android credential state and current user data. |

## Usage

Register `GoogleSignInPluginConfig` before Nova SDK initialization, wait for SDK
initialization to finish, and then get the strongly typed plugin.

```csharp
GoogleSignInPlugin plugin = Nova.SDK.Get<GoogleSignInPlugin>();
AuthResult result = await plugin.LoginAsync("Google");
if (result.Success)
{
    string userId = plugin.CurrentUserData?.UserId;
    string idToken = plugin.CurrentUserData?.IdToken;
}
```

After a successful login, `LoginAsync` updates `CurrentUserData`.
`AuthResult.Token` is the Google ID Token for this login result. Callers should
not cache stale ID Tokens for long periods. When server verification is needed,
send the current login result token to the server-side authentication flow.

## Config

Runtime configuration is documented in
[GoogleSignInPluginConfig.md](./GoogleSignInPluginConfig.md). Key fields:

- `ClientId`
- `RequestEmail`
- `FilterByAuthorizedAccounts`
- `AutoSelectEnabled`
- `AutoRestoreOnInitialize`

When `AutoRestoreOnInitialize` is `true`, initialization attempts to restore a
previous Google sign-in. Restore failures are logged and do not prevent callers
from explicitly calling `LoginAsync`.

## Token Fields

Google Sign-In maps only the Google ID Token to Nova `AuthResult.Token`.
`GoogleSignInUserData` also keeps `UserId`, `Email`, `DisplayName`, and
`AvatarUrl`. These fields can be empty or unavailable depending on Android
platform behavior, account state, consent, and profile data.

## Android Credential Manager Flow

The Android bridge uses Credential Manager and Google ID Token credentials:

- `LoginAsync` requests a Google ID Token through Credential Manager.
- `FilterByAuthorizedAccounts == true` first requests authorized accounts; if
  that request fails, the bridge retries without the authorized-account filter.
- `AutoSelectEnabled` is passed to Credential Manager auto-select behavior.
- `LogoutAsync` clears platform credential state.

Android builds need a valid `ClientId`. Before release, recheck the current
Google Play, Google Identity Services, Credential Manager, privacy disclosure,
and login text or button requirements.

## Data Handling Expectations

- Treat `CurrentUserData` as the package's current login-state payload.
- Treat `IdToken`, `Email`, `DisplayName`, and `AvatarUrl` as nullable or
  revocable data.
- Do not make server-trust decisions from client-side token parsing alone.
- Handle cancellation, platform login failure, the user closing platform UI,
  network failure, and empty returned fields.
- Do not publish OAuth client secrets, private signing material, provisioning
  profiles, or environment-specific service files with sample or package docs.

## Related

- [GoogleSignInPluginConfig.md](./GoogleSignInPluginConfig.md)
- [GoogleSignInUserData.md](./GoogleSignInUserData.md)
- [OpenSourceCompliance.md](./OpenSourceCompliance.md)
