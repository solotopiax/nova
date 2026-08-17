package com.solotopia.nova.nativebridge;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;

import com.google.android.gms.tasks.OnCompleteListener;
import com.google.android.gms.tasks.Task;
import com.google.android.play.core.review.ReviewInfo;
import com.google.android.play.core.review.ReviewManager;
import com.google.android.play.core.review.ReviewManagerFactory;

/**
 * Android 应用内评价原生桥接；仅将平台请求链状态回传给 Unity，不推断系统展示或用户评价结果。
 */
public final class NovaNativeReviewBridge {
    /** 当前平台或版本不支持该能力。 */
    public static final int STATUS_UNSUPPORTED = 0;
    /** 当前没有可用的前台 Activity。 */
    public static final int STATUS_UNAVAILABLE = 1;
    /** 已将请求交给系统原生流程。 */
    public static final int STATUS_REQUEST_DISPATCHED = 2;
    /** 原生桥接或平台请求链发生技术失败。 */
    public static final int STATUS_FAILED = 3;

    /** Android 主线程派发器；不依赖可能已销毁的 Activity。 */
    private static final Handler MAIN_HANDLER = new Handler(Looper.getMainLooper());

    /**
     * Unity 侧回调接口。回调由桥接派发到 Android 主界面线程，托管层会再切回 Unity PlayerLoop。
     */
    public interface Callback {
        /**
         * 接收平台原生请求状态。
         *
         * @param status 平台无关的请求状态。
         * @param errorMessage 技术失败时的框架错误描述。
         */
        void onComplete(int status, String errorMessage);
    }

    /**
     * 禁止实例化纯静态桥接类型。
     */
    private NovaNativeReviewBridge() {
    }

    /**
     * 依次请求评价令牌并启动系统评价流程；完成只表示平台请求链结束，不能说明用户是否评价。
     *
     * @param activity 当前 Unity Activity。
     * @param callback Unity 侧状态回调。
     */
    public static void requestReview(final Activity activity, final Callback callback) {
        if (callback == null) {
            return;
        }
        if (isActivityUnavailable(activity)) {
            dispatchResult(callback, STATUS_UNAVAILABLE, null);
            return;
        }

        try {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (isActivityUnavailable(activity)) {
                        dispatchResult(callback, STATUS_UNAVAILABLE, null);
                        return;
                    }

                    try {
                        final ReviewManager reviewManager = ReviewManagerFactory.create(activity);
                        reviewManager.requestReviewFlow().addOnCompleteListener(
                            new OnCompleteListener<ReviewInfo>() {
                                @Override
                                public void onComplete(Task<ReviewInfo> requestTask) {
                                    try {
                                        if (!requestTask.isSuccessful() || requestTask.getResult() == null) {
                                            dispatchResult(callback, STATUS_FAILED, "应用内评价请求失败。");
                                            return;
                                        }
                                        if (isActivityUnavailable(activity)) {
                                            dispatchResult(callback, STATUS_UNAVAILABLE, null);
                                            return;
                                        }

                                        ReviewInfo reviewInfo = requestTask.getResult();
                                        reviewManager.launchReviewFlow(activity, reviewInfo).addOnCompleteListener(
                                            new OnCompleteListener<Void>() {
                                                @Override
                                                public void onComplete(Task<Void> launchTask) {
                                                    try {
                                                        dispatchResult(
                                                            callback,
                                                            launchTask.isSuccessful()
                                                                ? STATUS_REQUEST_DISPATCHED
                                                                : STATUS_FAILED,
                                                            launchTask.isSuccessful()
                                                                ? null
                                                                : "应用内评价请求失败。");
                                                    } catch (Exception exception) {
                                                        dispatchResult(callback, STATUS_FAILED, "应用内评价请求失败。");
                                                    }
                                                }
                                            });
                                    } catch (Exception exception) {
                                        dispatchResult(callback, STATUS_FAILED, "应用内评价请求失败。");
                                    }
                                }
                            });
                    } catch (Exception exception) {
                        dispatchResult(callback, STATUS_FAILED, "应用内评价请求失败。");
                    }
                }
            });
        } catch (Exception exception) {
            dispatchResult(callback, STATUS_FAILED, "应用内评价请求失败。");
        }
    }

    /**
     * 判断当前 Activity 是否已经不适合作为系统评价流程宿主。
     *
     * @param activity 当前 Unity Activity。
     * @return 当 Activity 缺失或已进入销毁流程时返回 true。
     */
    private static boolean isActivityUnavailable(Activity activity) {
        return activity == null || activity.isFinishing() || activity.isDestroyed();
    }

    /**
     * 将异步结果统一派发到 Android 主界面线程，再进入 Unity 的 Java 代理回调。
     *
     * @param callback Unity 侧状态回调。
     * @param status 平台无关的请求状态。
     * @param errorMessage 技术失败时的框架错误描述。
     */
    private static void dispatchResult(
        final Callback callback,
        final int status,
        final String errorMessage) {
        if (callback == null) {
            return;
        }

        final Runnable callbackRunnable = new Runnable() {
            @Override
            public void run() {
                callback.onComplete(status, errorMessage);
            }
        };
        if (Looper.myLooper() == Looper.getMainLooper()) {
            callbackRunnable.run();
        } else if (!MAIN_HANDLER.post(callbackRunnable)) {
            callbackRunnable.run();
        }
    }
}
