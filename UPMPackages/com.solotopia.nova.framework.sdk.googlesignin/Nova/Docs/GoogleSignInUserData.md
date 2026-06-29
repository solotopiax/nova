# GoogleSignInUserData

`GoogleSignInUserData` is the user payload returned from the Android Google
Sign-In bridge to the Nova layer. It represents the current login result. It
does not persist account state and does not validate ID Tokens.

## Public API

```csharp
public sealed class GoogleSignInUserData
{
    public string UserId { get; }
    public string IdToken { get; }
    public string Email { get; }
    public string DisplayName { get; }
    public string AvatarUrl { get; }
    public AuthResult ToAuthResult(string provider);
}
```

## Token Fields

| Field | Notes |
|---|---|
| `UserId` | Google user ID. Android prefers the `sub` claim from the ID Token when available. |
| `IdToken` | Google ID Token mapped to `AuthResult.Token`. |
| `Email` | Email returned by the Android platform flow; may be empty. |
| `DisplayName` | Display name returned by the Android platform flow; may be empty. |
| `AvatarUrl` | Avatar URL returned by the Android platform flow; may be empty. |

## Usage

```csharp
var data = new GoogleSignInUserData("user", "id-token", "user@test.com", "User", "");
AuthResult result = data.ToAuthResult("Google");
```

`ToAuthResult` requires both `UserId` and `IdToken`. If either value is missing,
it returns a failed `AuthResult` with an `ErrorMessage`.

## Android Credential Manager Flow

The Android bridge reads the ID Token from the Google ID Token credential
returned by Credential Manager. It parses ID Token claims to read `sub` and
`email` when available. Avatar URL comes from
`googleCredential.getProfilePictureUri()`. Parse failures or platform failures
are returned through the async flow to `GoogleSignInPlugin.LoginAsync`.

## Data Handling Expectations

- `IdToken` is sensitive authentication data and should be passed only to the
  server or authentication flow that verifies login.
- `Email`, `DisplayName`, and `AvatarUrl` are not guaranteed and should not be
  the only identity proof.
- Do not make server-trust decisions from client-side token parsing alone.
- Recheck Google platform policy, privacy disclosure, and account data handling
  requirements for each release.

## Related

- [GoogleSignInPlugin.md](./GoogleSignInPlugin.md)
- [OpenSourceCompliance.md](./OpenSourceCompliance.md)
