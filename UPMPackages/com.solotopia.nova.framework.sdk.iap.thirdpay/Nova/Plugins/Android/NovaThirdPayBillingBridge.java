package com.solotopia.nova.thirdpay;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;

import androidx.annotation.Keep;
import androidx.annotation.NonNull;

import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingConfig;
import com.android.billingclient.api.BillingConfigResponseListener;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.GetBillingConfigParams;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.PurchasesUpdatedListener;

import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Google Play Billing 商店地区代码桥接。
 *
 * 该 bridge 只读取 BillingConfig，不承载 ThirdPay 支付流程；支付政策流程仍由
 * Unity Purchasing ExternalBillingProgramClient 负责。
 */
@Keep
public final class NovaThirdPayBillingBridge {
    private static final Handler MAIN_HANDLER = new Handler(Looper.getMainLooper());

    /** Unity 侧接收商店地区代码的回调。 */
    @Keep
    public interface Callback {
        /**
         * 返回商店地区代码；读取失败时传空字符串。
         *
         * @param countryCode Google Play Billing 国家或地区代码
         */
        @Keep
        void onCountryCodeReceived(String countryCode);
    }

    private NovaThirdPayBillingBridge() {
    }

    /**
     * 连接 BillingClient 并异步读取 BillingConfig.countryCode。
     *
     * @param activity 当前 Unity Activity
     * @param callback Unity 侧回调
     */
    @Keep
    public static void getBillingCountryCode(final Activity activity, final Callback callback) {
        if (callback == null) {
            return;
        }
        if (isActivityUnavailable(activity)) {
            dispatch(callback, "");
            return;
        }

        try {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (isActivityUnavailable(activity)) {
                        dispatch(callback, "");
                        return;
                    }

                    final AtomicBoolean completed = new AtomicBoolean(false);
                    final BillingClient[] clientHolder = new BillingClient[1];
                    try {
                        BillingClient client = BillingClient.newBuilder(activity)
                                .setListener(new PurchasesUpdatedListener() {
                                    @Override
                                    public void onPurchasesUpdated(
                                            @NonNull BillingResult billingResult,
                                            java.util.List<com.android.billingclient.api.Purchase> purchases) {
                                        // 读取 BillingConfig 不需要处理购买回调。
                                    }
                                })
                                .enablePendingPurchases(
                                        PendingPurchasesParams.newBuilder()
                                                .enableOneTimeProducts()
                                                .build())
                                .build();
                        clientHolder[0] = client;
                        client.startConnection(new BillingClientStateListener() {
                            @Override
                            public void onBillingSetupFinished(@NonNull BillingResult billingResult) {
                                if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                                    finish(clientHolder[0], callback, completed, "");
                                    return;
                                }

                                try {
                                    clientHolder[0].getBillingConfigAsync(
                                            GetBillingConfigParams.newBuilder().build(),
                                            new BillingConfigResponseListener() {
                                                @Override
                                                public void onBillingConfigResponse(
                                                        @NonNull BillingResult response,
                                                        @NonNull BillingConfig billingConfig) {
                                                    if (response.getResponseCode() != BillingClient.BillingResponseCode.OK
                                                            || billingConfig == null) {
                                                        finish(clientHolder[0], callback, completed, "");
                                                        return;
                                                    }

                                                    String countryCode = billingConfig.getCountryCode();
                                                    finish(clientHolder[0], callback, completed,
                                                            countryCode == null ? "" : countryCode);
                                                }
                                            });
                                } catch (RuntimeException exception) {
                                    finish(clientHolder[0], callback, completed, "");
                                }
                            }

                            @Override
                            public void onBillingServiceDisconnected() {
                                finish(clientHolder[0], callback, completed, "");
                            }
                        });
                    } catch (RuntimeException exception) {
                        finish(clientHolder[0], callback, completed, "");
                    }
                }
            });
        } catch (RuntimeException exception) {
            dispatch(callback, "");
        }
    }

    private static void finish(
            BillingClient client,
            Callback callback,
            AtomicBoolean completed,
            String countryCode) {
        if (!completed.compareAndSet(false, true)) {
            return;
        }

        if (client != null) {
            client.endConnection();
        }
        dispatch(callback, countryCode == null ? "" : countryCode);
    }

    private static void dispatch(final Callback callback, final String countryCode) {
        if (callback == null) {
            return;
        }

        Runnable callbackRunnable = new Runnable() {
            @Override
            public void run() {
                callback.onCountryCodeReceived(countryCode);
            }
        };
        if (Looper.myLooper() == Looper.getMainLooper()) {
            callbackRunnable.run();
        } else if (!MAIN_HANDLER.post(callbackRunnable)) {
            callbackRunnable.run();
        }
    }

    private static boolean isActivityUnavailable(Activity activity) {
        return activity == null || activity.isFinishing() || activity.isDestroyed();
    }
}
