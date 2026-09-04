package com.solotopia.nova.thirdpay;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.Log;

import androidx.annotation.Keep;
import androidx.activity.ComponentActivity;
import androidx.activity.result.ActivityResultCallback;
import androidx.activity.result.ActivityResultLauncher;
import androidx.browser.auth.AuthTabIntent;
import androidx.browser.customtabs.CustomTabsClient;
import androidx.browser.customtabs.CustomTabsIntent;

/**
 * ThirdPay Auth Tab 透明代理 Activity。
 */
@Keep
public final class NovaThirdPayAuthTabActivity extends ComponentActivity {
    private static final String TAG = "NovaThirdPayAuthTab";

    /** 打开失败。 */
    private static final int RESULT_FAILED = 0;

    /** 已通过 Auth Tab 打开。 */
    private static final int RESULT_AUTH_TAB = 1;

    /** 已通过 Custom Tabs 打开。 */
    private static final int RESULT_CUSTOM_TABS = 2;

    /** ThirdPay 支付 URL extra。 */
    private static final String EXTRA_URL = "com.solotopia.nova.thirdpay.extra.URL";

    /** Auth Tab 使用的浏览器 provider 包名 extra。 */
    private static final String EXTRA_PROVIDER_PACKAGE = "com.solotopia.nova.thirdpay.extra.PROVIDER_PACKAGE";

    /** Auth Tab 捕获的支付页回调 scheme；当前 ThirdPay 支付页沿用 UniWebView message scheme。 */
    private static final String REDIRECT_SCHEME = "uniwebview";

    /** Auth Tab 结果接收器，必须在 Activity 创建阶段稳定注册。 */
    private final ActivityResultLauncher<Intent> m_AuthTabLauncher =
            AuthTabIntent.registerActivityResultLauncher(
                    this,
                    new ActivityResultCallback<AuthTabIntent.AuthResult>() {
                        @Override
                        public void onActivityResult(AuthTabIntent.AuthResult result) {
                            handleAuthResult(result);
                        }
                    });

    /**
     * 优先通过 Auth Tab 打开 URL，不支持时回退 Custom Tabs；普通浏览器兜底由 C# Application.OpenURL 处理。
     *
     * @param context Android Context。
     * @param rawUrl  待打开 URL。
     * @return 0 失败，1 Auth Tab，2 Custom Tabs。
     */
    @Keep
    public static int openUrlPreferAuthTab(Context context, String rawUrl) {
        if (context == null || TextUtils.isEmpty(rawUrl)) {
            return RESULT_FAILED;
        }

        Uri uri = Uri.parse(rawUrl);
        String customTabsPackageName = getCustomTabsPackageName(context);
        if (!TextUtils.isEmpty(customTabsPackageName)) {
            if (isAuthTabSupported(context, customTabsPackageName)
                    && openUrlByAuthTab(context, rawUrl, customTabsPackageName)) {
                return RESULT_AUTH_TAB;
            }

            if (openUrlByCustomTabs(context, uri, customTabsPackageName)) {
                return RESULT_CUSTOM_TABS;
            }
        }

        return RESULT_FAILED;
    }

    /**
     * Activity 创建后立即启动 Auth Tab。
     *
     * @param savedInstanceState 系统保存的 Activity 状态。
     */
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState == null) {
            launchAuthTab();
        }
    }

    /**
     * 使用 AuthTabIntent 打开支付页；启动失败时退回 Custom Tabs，普通浏览器兜底由 C# Application.OpenURL 处理。
     */
    private void launchAuthTab() {
        String rawUrl = getIntent().getStringExtra(EXTRA_URL);
        String providerPackage = getIntent().getStringExtra(EXTRA_PROVIDER_PACKAGE);
        if (TextUtils.isEmpty(rawUrl)) {
            finish();
            return;
        }

        try {
            AuthTabIntent authTabIntent = new AuthTabIntent.Builder().build();
            if (!TextUtils.isEmpty(providerPackage)) {
                authTabIntent.intent.setPackage(providerPackage);
            }

            authTabIntent.launch(m_AuthTabLauncher, Uri.parse(rawUrl), REDIRECT_SCHEME);
        } catch (RuntimeException exception) {
            Log.w(TAG, "Launch Auth Tab failed, fallback to Custom Tabs.", exception);
            Uri uri = Uri.parse(rawUrl);
            if (!TextUtils.isEmpty(providerPackage)) {
                openUrlByCustomTabs(this, uri, providerPackage);
            }
            finish();
        }
    }

    /**
     * 处理 Auth Tab 关闭或 redirect 结果；实际验单仍由 Unity 前后台返回链路兜底触发。
     *
     * @param result Auth Tab 结果。
     */
    private void handleAuthResult(AuthTabIntent.AuthResult result) {
        int resultCode = result == null ? AuthTabIntent.RESULT_UNKNOWN_CODE : result.resultCode;
        Uri resultUri = result == null ? null : result.resultUri;
        Log.i(TAG, "Auth Tab finished. resultCode=" + resultCode + ", uri=" + resultUri);
        finish();
    }

    /**
     * 查询当前系统可用于 Custom Tabs 的浏览器包名。
     *
     * @param context Android Context。
     * @return 支持 Custom Tabs 的包名；不存在或查询失败时返回 null。
     */
    private static String getCustomTabsPackageName(Context context) {
        if (context == null) {
            return null;
        }

        try {
            return CustomTabsClient.getPackageName(context, null);
        } catch (RuntimeException exception) {
            Log.w(TAG, "Query Custom Tabs package failed.", exception);
            return null;
        }
    }

    /**
     * 判断指定 provider 是否支持 Auth Tab。
     *
     * @param context     Android Context。
     * @param packageName Custom Tabs provider 包名。
     * @return provider 支持 Auth Tab 时返回 true。
     */
    private static boolean isAuthTabSupported(Context context, String packageName) {
        if (context == null || TextUtils.isEmpty(packageName)) {
            return false;
        }

        try {
            return CustomTabsClient.isAuthTabSupported(context, packageName);
        } catch (RuntimeException exception) {
            Log.w(TAG, "Query Auth Tab support failed.", exception);
            return false;
        }
    }

    /**
     * 通过包内透明 Activity 启动 Auth Tab。
     *
     * @param context     Android Context。
     * @param rawUrl      待打开 URL。
     * @param packageName Custom Tabs provider 包名。
     * @return Auth Tab 代理 Activity 启动成功时返回 true。
     */
    private static boolean openUrlByAuthTab(Context context, String rawUrl, String packageName) {
        try {
            Intent intent = new Intent();
            intent.setClass(context, NovaThirdPayAuthTabActivity.class);
            intent.putExtra(EXTRA_URL, rawUrl);
            intent.putExtra(EXTRA_PROVIDER_PACKAGE, packageName);
            if (!(context instanceof Activity)) {
                intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            }

            context.startActivity(intent);
            return true;
        } catch (RuntimeException exception) {
            Log.w(TAG, "Open url by Auth Tab failed.", exception);
            return false;
        }
    }

    /**
     * 通过 Custom Tabs 打开 URL。
     *
     * @param context     Android Context。
     * @param uri         待打开 URI
     * @param packageName Custom Tabs provider 包名
     * @return Custom Tabs 启动请求提交成功时返回 true。
     */
    private static boolean openUrlByCustomTabs(Context context, Uri uri, String packageName) {
        try {
            CustomTabsIntent customTabsIntent = new CustomTabsIntent.Builder()
                    .setShowTitle(false)
                    .setUrlBarHidingEnabled(true)
                    .setShareState(CustomTabsIntent.SHARE_STATE_OFF)
                    .setDefaultShareMenuItemEnabled(false)
                    .setBookmarksButtonEnabled(false)
                    .setDownloadButtonEnabled(false)
                    .setCloseButtonEnabled(false)
                    .setOpenInBrowserButtonState(CustomTabsIntent.OPEN_IN_BROWSER_STATE_OFF)
                    .build();
            customTabsIntent.intent.setPackage(packageName);
            if (!(context instanceof Activity)) {
                customTabsIntent.intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            }

            customTabsIntent.launchUrl(context, uri);
            return true;
        } catch (RuntimeException exception) {
            Log.w(TAG, "Open url by Custom Tabs failed.", exception);
            return false;
        }
    }
}
