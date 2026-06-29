package com.solotopia.nova.googlesignin;

public interface GoogleSignInBridgeCallback {
    void onSuccess(String userId, String idToken, String email, String displayName, String avatarUrl);
    void onError(String error);
}
