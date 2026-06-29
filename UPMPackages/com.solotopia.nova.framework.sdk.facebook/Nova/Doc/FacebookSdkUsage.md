# Facebook Unity SDK Usage Notes

These notes were extracted from the removed `Assets/FacebookSDK/Examples` folder after importing Facebook Unity SDK `18.0.0`.

## Basic Lifecycle

Use `FB.Init(onInitComplete, onHideUnity)` before calling most SDK APIs.

Important APIs:

- `FB.IsInitialized`
- `FB.Init(...)`
- `FB.ActivateApp()`
- `FB.IsLoggedIn`
- `FB.FacebookImpl.SDKUserAgent`

Example flow:

```csharp
FB.Init(
    () =>
    {
        if (FB.IsInitialized)
            FB.ActivateApp();
    },
    isGameShown =>
    {
        Time.timeScale = isGameShown ? 1 : 0;
    });
```

## Login

Classic login:

```csharp
FB.LogInWithReadPermissions(
    new List<string> { "public_profile" },
    result => { /* handle ILoginResult */ });
```

Limited login on mobile:

```csharp
FB.Mobile.LoginWithTrackingPreference(
    LoginTracking.LIMITED,
    new List<string> { "public_profile", "user_friends" },
    "nonce",
    result => { /* handle result */ });
```

Publish permission login:

```csharp
FB.LogInWithPublishPermissions(
    new List<string> { "publish_actions" },
    result => { /* handle ILoginResult */ });
```

Logout and token:

```csharp
FB.LogOut();
var token = AccessToken.CurrentAccessToken;
```

Useful token fields include `UserId` and `Permissions`.

## Profile

```csharp
FB.CurrentProfile(result =>
{
    var profile = result.CurrentProfile;
});

FB.GetUserLocale(result =>
{
    var locale = result.Locale;
});
```

Mobile limited-login examples also read:

```csharp
var profile = FB.Mobile.CurrentProfile();
```

Profile fields used by the examples:

- `UserID`
- `Name`
- `FirstName`
- `MiddleName`
- `LastName`
- `Email`
- `ImageURL`
- `Birthday`
- `AgeRange`
- `FriendIDs`
- `Hometown`
- `Location`
- `Gender`

## App Events

```csharp
FB.LogAppEvent(
    AppEventName.UnlockedAchievement,
    null,
    new Dictionary<string, object>
    {
        { AppEventParameterName.Description, "Clicked Log AppEvent button" }
    });
```

## Graph API

Common calls from the examples:

```csharp
FB.API("/me", HttpMethod.GET, callback);
FB.API("/me/picture", HttpMethod.GET, callback);
FB.API("me/photos", HttpMethod.POST, callback, form);
```

For profile pictures, `IGraphResult.Texture` can contain the downloaded texture.

`FB.GraphApiVersion` is readable and writable in the Windows example.

## Sharing

Share link:

```csharp
FB.ShareLink(
    new Uri("https://developers.facebook.com/"),
    "Link Share",
    "Look I'm sharing a link",
    new Uri("https://example.com/image.jpg"),
    callback);
```

Feed share:

```csharp
FB.FeedShare(
    toId,
    link,
    linkName,
    linkCaption,
    linkDescription,
    picture,
    mediaSource,
    callback);
```

On mobile, examples also switch:

```csharp
FB.Mobile.ShareDialogMode = ShareDialogMode.Automatic;
```

## App Requests

Simple request:

```csharp
FB.AppRequest("Test Message", callback: callback);
```

Filtered request:

```csharp
FB.AppRequest(
    "Test Message",
    null,
    new List<object> { "app_users" },
    null,
    0,
    string.Empty,
    string.Empty,
    callback);
```

Open Graph action request:

```csharp
FB.AppRequest(
    message,
    OGActionType.SEND,
    objectId,
    toIds,
    data,
    title,
    callback);
```

## App Links

```csharp
FB.GetAppLink(callback);
FB.Mobile.FetchDeferredAppLinkData(callback);
```

## Tournaments

Mobile examples use:

```csharp
FB.Mobile.GetTournaments(callback);
FB.Mobile.UpdateTournament(tournamentId, score, callback);
FB.Mobile.UpdateAndShareTournament(tournamentId, score, callback);
FB.Mobile.CreateAndShareTournament(
    score,
    "Unity Tournament",
    TournamentSortOrder.HigherIsBetter,
    TournamentScoreFormat.Numeric,
    DateTime.UtcNow.AddHours(2),
    "Unity SDK Tournament",
    callback);
```

Windows examples use:

```csharp
FB.CreateTournament(score, title, imageBase64, sortOrder, scoreFormat, endTime, data, callback);
FB.PostSessionScore(score, callback);
FB.PostTournamentScore(score, callback);
FB.ShareTournament(score, data, callback);
FB.GetTournament(callback);
```

## Gaming Services

Cloud game initialization:

```csharp
FBGamingServices.InitCloudGame(callback);
FBGamingServices.GetPayload(callback);
```

Ads:

```csharp
FBGamingServices.LoadInterstitialAd(placementId, callback);
FBGamingServices.ShowInterstitialAd(placementId, callback);
FBGamingServices.LoadRewardedVideo(placementId, callback);
FBGamingServices.ShowRewardedVideo(placementId, callback);
```

Cloud IAP:

```csharp
FBGamingServices.OnIAPReady(callback);
FBGamingServices.GetCatalog(callback);
FBGamingServices.Purchase(productId, callback);
FBGamingServices.GetPurchases(callback);
FBGamingServices.ConsumePurchase(purchaseToken, callback);
```

Media upload:

```csharp
FBGamingServices.UploadImageToMediaLibrary(caption, imageUri, shouldLaunchDialog, callback);
FBGamingServices.UploadVideoToMediaLibrary(caption, videoUri, shouldLaunchDialog, callback);
```

## Windows-Specific APIs

Windows examples use these APIs:

- `FB.Windows.SetVirtualGamepadLayout(...)`
- `FB.Windows.SetSoftKeyboardOpen(...)`
- `FB.Windows.CreateReferral(...)`
- `FB.Windows.GetDataReferral(...)`
- `FB.OpenFriendFinderDialog(...)`
- `FB.GetFriendFinderInvitations(...)`
- `FB.DeleteFriendFinderInvitation(...)`
- `FB.ScheduleAppToUserNotification(...)`
- `FB.LoadInterstitialAd(...)`
- `FB.ShowInterstitialAd(...)`
- `FB.LoadRewardedVideo(...)`
- `FB.ShowRewardedVideo(...)`
- `FB.GetCatalog(...)`
- `FB.Purchase(...)`
- `FB.GetPurchases(...)`
- `FB.ConsumePurchase(...)`

## Suggested Nova Plugin Surface

Start with the stable cross-platform basics:

- `InitAsync`
- `ActivateApp`
- `LoginReadAsync`
- `LoginLimitedAsync`
- `LoginPublishAsync`
- `LogoutAsync`
- `GetAccessToken`
- `GetProfileAsync`
- `LogEvent`
- `ShareLinkAsync`
- `AppRequestAsync`
- `GraphGetAsync`
- `FetchDeferredAppLinkAsync`

Keep advanced capabilities in separate services:

- `FacebookGamingService`
- `FacebookTournamentService`
- `FacebookWindowsService`

The removed official `IAP.cs` example used `IAPWrapper`, but that class was not present in the imported SDK package. Do not use that example as a basis for the Nova plugin.
