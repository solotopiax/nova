package com.solotopia.nova.googlesignin;

import android.app.Activity;
import android.net.Uri;
import android.os.CancellationSignal;
import android.util.Base64;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialException;
import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import org.json.JSONException;
import org.json.JSONObject;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

public final class GoogleSignInBridge {
    private static final Executor EXECUTOR = Executors.newSingleThreadExecutor();

    private GoogleSignInBridge() {
    }

    public static void signIn(
            Activity activity,
            String webClientId,
            boolean requestEmail,
            boolean filterByAuthorizedAccounts,
            boolean autoSelectEnabled,
            GoogleSignInBridgeCallback callback) {
        requestCredential(activity, webClientId, filterByAuthorizedAccounts, autoSelectEnabled, callback, true);
    }

    public static void restore(
            Activity activity,
            String webClientId,
            boolean requestEmail,
            boolean autoSelectEnabled,
            GoogleSignInBridgeCallback callback) {
        requestCredential(activity, webClientId, true, autoSelectEnabled, callback, false);
    }

    public static void signOut(Activity activity) {
        if (activity == null) {
            return;
        }

        activity.runOnUiThread(() -> {
            CredentialManager credentialManager = CredentialManager.create(activity);
            credentialManager.clearCredentialStateAsync(
                    new ClearCredentialStateRequest(),
                    new CancellationSignal(),
                    EXECUTOR,
                    new CredentialManagerCallback<Void, ClearCredentialException>() {
                        @Override
                        public void onResult(Void result) {
                        }

                        @Override
                        public void onError(ClearCredentialException e) {
                        }
                    });
        });
    }

    private static void requestCredential(
            Activity activity,
            String webClientId,
            boolean filterByAuthorizedAccounts,
            boolean autoSelectEnabled,
            GoogleSignInBridgeCallback callback,
            boolean retryWithoutAuthorizedFilter) {
        if (callback == null) {
            return;
        }

        if (activity == null) {
            callback.onError("Unity Activity 为空。");
            return;
        }

        if (webClientId == null || webClientId.length() == 0) {
            callback.onError("Google Web Client ID 为空。");
            return;
        }

        activity.runOnUiThread(() -> {
            GetGoogleIdOption googleIdOption = new GetGoogleIdOption.Builder()
                    .setServerClientId(webClientId)
                    .setFilterByAuthorizedAccounts(filterByAuthorizedAccounts)
                    .setAutoSelectEnabled(autoSelectEnabled)
                    .build();

            GetCredentialRequest request = new GetCredentialRequest.Builder()
                    .addCredentialOption(googleIdOption)
                    .build();

            CredentialManager credentialManager = CredentialManager.create(activity);
            credentialManager.getCredentialAsync(
                    activity,
                    request,
                    new CancellationSignal(),
                    EXECUTOR,
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override
                        public void onResult(GetCredentialResponse result) {
                            handleCredential(result.getCredential(), callback);
                        }

                        @Override
                        public void onError(GetCredentialException e) {
                            if (retryWithoutAuthorizedFilter && filterByAuthorizedAccounts) {
                                requestCredential(activity, webClientId, false, autoSelectEnabled, callback, false);
                                return;
                            }

                            callback.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
                        }
                    });
        });
    }

    private static void handleCredential(Credential credential, GoogleSignInBridgeCallback callback) {
        if (!(credential instanceof CustomCredential)) {
            callback.onError("Google 登录返回了不支持的凭据类型。");
            return;
        }

        CustomCredential customCredential = (CustomCredential) credential;
        if (!GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(customCredential.getType())) {
            callback.onError("Google 登录返回了非 Google ID Token 凭据。");
            return;
        }

        try {
            GoogleIdTokenCredential googleCredential = GoogleIdTokenCredential.createFrom(customCredential.getData());
            JSONObject claims = parseIdTokenClaims(googleCredential.getIdToken());
            String credentialId = googleCredential.getId();
            String userId = claims.optString("sub", credentialId);
            String email = claims.optString("email", credentialId);
            Uri pictureUri = googleCredential.getProfilePictureUri();
            callback.onSuccess(
                    userId,
                    googleCredential.getIdToken(),
                    email,
                    googleCredential.getDisplayName(),
                    pictureUri == null ? null : pictureUri.toString());
        } catch (RuntimeException e) {
            callback.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
        }
    }

    private static JSONObject parseIdTokenClaims(String idToken) {
        if (idToken == null) {
            return new JSONObject();
        }

        String[] parts = idToken.split("\\.");
        if (parts.length < 2) {
            return new JSONObject();
        }

        try {
            byte[] payload = Base64.decode(parts[1], Base64.URL_SAFE | Base64.NO_PADDING | Base64.NO_WRAP);
            return new JSONObject(new String(payload, StandardCharsets.UTF_8));
        } catch (IllegalArgumentException | JSONException e) {
            return new JSONObject();
        }
    }
}
