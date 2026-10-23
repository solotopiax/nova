/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename: NovaNativeAppInstallerBridge.java
 * descrip: 显式自身 APK 安装；严格验签与身份校验、系统确认、session 清理
 ***************************************************************/
package com.solotopia.nova.nativebridge;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import com.android.apksig.ApkVerifier;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.OutputStream;
import java.lang.ref.WeakReference;
import java.security.cert.X509Certificate;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** 首版只处理单 APK 自身升级；不支持 split、signer 轮换、静默安装或自动重启。 */
public final class NovaNativeAppInstallerBridge {
    public interface Callback { void onComplete(int status, String message); }
    private static final int UNSUPPORTED = 0, PERMISSION_REQUIRED = 2, INSTALLED = 3, CANCELLED = 4, FAILED = 5;
    private static final long MAX_BYTES = 1024L * 1024 * 1024;
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static final ExecutorService WORKER = Executors.newSingleThreadExecutor();
    private static final Map<String, Operation> ACTIVE = new HashMap<>();
    private static final String PREFS = "Nova.Native.AppInstaller", SESSION_KEY = "session", STAGING = "nova-app-installer";

    private NovaNativeAppInstallerBridge() { }

    /** 仅清理本桥接上次进程留下的 session 与 cache；从不打开权限或系统界面。 */
    public static synchronized void initialize(Activity activity) {
        if (activity == null || !ACTIVE.isEmpty()) return;
        Context context = activity.getApplicationContext();
        SharedPreferences prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        int session = prefs.getInt(SESSION_KEY, -1);
        if (session >= 0) {
            try { context.getPackageManager().getPackageInstaller().abandonSession(session); } catch (Exception ignored) { }
            prefs.edit().remove(SESSION_KEY).commit();
        }
        File directory = new File(context.getCacheDir(), STAGING);
        File[] files = directory.listFiles();
        if (files != null) for (File file : files) if (file.isFile()) file.delete();
    }

    /** API28 以下能力不支持；来源许可始终读取操作系统。 */
    public static boolean canRequestPackageInstalls(Activity activity) {
        return activity != null && Build.VERSION.SDK_INT >= 28 && activity.getPackageManager().canRequestPackageInstalls();
    }

    /** 显式来源设置跳转；不会在安装或初始化内隐式调用。 */
    public static boolean openPackageInstallSettings(Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < 28 || activity.isFinishing() || activity.isDestroyed()) return false;
        try {
            activity.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + activity.getPackageName())));
            return true;
        } catch (Exception error) { return false; }
    }

    /** 每个进程只允许一个独占操作；复制、验签和 session 写入在后台执行。 */
    public static synchronized void install(Activity activity, String requestId, String controlledRoot, String apkPath, Callback callback) {
        if (callback == null) return;
        if (activity == null || Build.VERSION.SDK_INT < 28) { dispatch(callback, UNSUPPORTED, "Android API 28 or later is required."); return; }
        if (!canRequestPackageInstalls(activity)) { dispatch(callback, PERMISSION_REQUIRED, "Package installation source permission is required."); return; }
        if (!ACTIVE.isEmpty() || requestId == null || !requestId.matches("[a-f0-9]{32}")) { dispatch(callback, FAILED, "An APK install is already active or request identity is invalid."); return; }
        Operation operation = new Operation(activity, requestId, callback);
        ACTIVE.put(requestId, operation);
        operation.timeout = () -> finish(operation, FAILED, "Package installation timed out.", true);
        MAIN.postDelayed(operation.timeout, 15 * 60 * 1000);
        WORKER.execute(() -> prepare(operation, controlledRoot, apkPath));
    }

    /** 只取消当前 request；已成功安装不可回滚，取消后迟到回调无效。 */
    public static void cancel(String requestId) {
        Operation operation;
        synchronized (NovaNativeAppInstallerBridge.class) { operation = ACTIVE.get(requestId); }
        if (operation != null) finish(operation, CANCELLED, "", true);
    }

    /** 对私有暂存的同一份字节验签和写入，不把归档元数据当密码学证明。 */
    private static void prepare(Operation operation, String controlledRoot, String apkPath) {
        try {
            File root = new File(controlledRoot).getCanonicalFile();
            File source = new File(apkPath);
            if (!source.isAbsolute() || !source.getAbsoluteFile().equals(source.getCanonicalFile()) ||
                !source.getCanonicalPath().startsWith(root.getPath() + File.separator) || !source.isFile())
                throw new IllegalArgumentException("APK path is outside the controlled directory or is symbolic.");
            long expected = source.length();
            if (expected <= 0 || expected > MAX_BYTES) throw new IllegalArgumentException("APK size is invalid.");
            File directory = new File(operation.context.getCacheDir(), STAGING);
            if (!directory.isDirectory() && !directory.mkdir()) throw new IllegalStateException("Cannot create APK staging directory.");
            operation.staged = new File(directory, operation.id + ".apk");
            if (!operation.staged.createNewFile()) throw new IllegalStateException("APK staging file already exists.");
            long observed = 0;
            byte[] buffer = new byte[64 * 1024];
            try (FileInputStream input = new FileInputStream(source); FileOutputStream output = new FileOutputStream(operation.staged)) {
                int count;
                while ((count = input.read(buffer)) != -1) {
                    ensureActive(operation);
                    observed += count;
                    if (observed > MAX_BYTES || observed > expected) throw new IllegalArgumentException("APK changed while staging.");
                    output.write(buffer, 0, count);
                }
                output.getFD().sync();
            }
            if (observed != expected) throw new IllegalArgumentException("APK staging length changed.");
            ensureActive(operation);
            ApkVerifier.Result verified = new ApkVerifier.Builder(operation.staged)
                .setMinCheckedPlatformVersion(Build.VERSION.SDK_INT).setMaxCheckedPlatformVersion(Build.VERSION.SDK_INT).build().verify();
            if (!verified.isVerified() || verified.containsErrors()) throw new SecurityException("APK signature verification failed.");
            PackageManager manager = operation.context.getPackageManager();
            PackageInfo candidate = manager.getPackageArchiveInfo(operation.staged.getPath(), 0);
            PackageInfo installed = manager.getPackageInfo(operation.context.getPackageName(), PackageManager.GET_SIGNING_CERTIFICATES);
            if (candidate == null || !operation.context.getPackageName().equals(candidate.packageName) ||
                candidate.getLongVersionCode() <= installed.getLongVersionCode() || candidate.splitNames != null && candidate.splitNames.length != 0)
                throw new SecurityException("APK must be a single full package for this application with a higher versionCode.");
            Set<String> existing = new HashSet<>(), proposed = new HashSet<>();
            if (installed.signingInfo == null) throw new SecurityException("Installed signing identity is unavailable.");
            for (Signature signature : installed.signingInfo.getApkContentsSigners()) existing.add(hex(signature.toByteArray()));
            for (X509Certificate certificate : verified.getSignerCertificates()) proposed.add(hex(certificate.getEncoded()));
            if (existing.isEmpty() || !existing.equals(proposed)) throw new SecurityException("APK current signer set does not exactly match this application.");
            ensureActive(operation);
            PackageInstaller installer = manager.getPackageInstaller();
            PackageInstaller.SessionParams params = new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
            params.setAppPackageName(candidate.packageName);
            params.setSize(operation.staged.length());
            if (Build.VERSION.SDK_INT >= 31) params.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_REQUIRED);
            synchronized (operation) {
                ensureActive(operation);
                operation.sessionId = installer.createSession(params);
                if (!operation.context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putInt(SESSION_KEY, operation.sessionId).commit())
                    throw new IllegalStateException("Cannot persist install session cleanup marker.");
            }
            try (PackageInstaller.Session session = installer.openSession(operation.sessionId)) {
                try (FileInputStream input = new FileInputStream(operation.staged);
                     OutputStream output = session.openWrite("base.apk", 0, operation.staged.length())) {
                    int count;
                    while ((count = input.read(buffer)) != -1) { ensureActive(operation); output.write(buffer, 0, count); }
                    session.fsync(output);
                }
                ensureActive(operation);
                Intent callback = new Intent(operation.context, ResultReceiver.class);
                callback.setAction(operation.context.getPackageName() + ".nova.APP_INSTALL." + operation.id);
                callback.putExtra("novaRequestId", operation.id);
                int flags = PendingIntent.FLAG_UPDATE_CURRENT;
                if (Build.VERSION.SDK_INT >= 31) flags |= PendingIntent.FLAG_MUTABLE;
                operation.pendingIntent = PendingIntent.getBroadcast(operation.context, operation.sessionId, callback, flags);
                synchronized (operation) { ensureActive(operation); session.commit(operation.pendingIntent.getIntentSender()); }
            }
            // 提交后 session 已持有完整 fsync 字节，不再保留本桥接 APK 暂存副本。
            operation.staged.delete();
        } catch (Exception error) {
            finish(operation, FAILED, error.getClass().getSimpleName() + ": " + error.getMessage(), true);
        } finally {
            if (operation.staged != null) operation.staged.delete();
        }
    }

    /** 显式不可导出 receiver，只接受当前 session 与请求标识；pending user action 不是终态。 */
    public static final class ResultReceiver extends BroadcastReceiver {
        @Override public void onReceive(Context context, Intent intent) {
            String id = intent.getStringExtra("novaRequestId");
            Operation operation;
            synchronized (NovaNativeAppInstallerBridge.class) { operation = ACTIVE.get(id); }
            if (operation == null || intent.getIntExtra(PackageInstaller.EXTRA_SESSION_ID, -1) != operation.sessionId) return;
            int status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
            if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
                Intent confirmation = intent.getParcelableExtra(Intent.EXTRA_INTENT);
                Activity activity = operation.activity.get();
                if (confirmation == null || activity == null || activity.isFinishing() || activity.isDestroyed()) {
                    finish(operation, FAILED, "No active Activity for installation confirmation.", true); return;
                }
                try { activity.startActivity(confirmation); }
                catch (Exception error) { finish(operation, FAILED, "Cannot start system installation confirmation.", true); }
                return;
            }
            if (status == PackageInstaller.STATUS_SUCCESS) finish(operation, INSTALLED, "", false);
            else if (status == PackageInstaller.STATUS_FAILURE_ABORTED) finish(operation, CANCELLED, "", true);
            else finish(operation, FAILED, "PackageInstaller status " + status + ": " + intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE), true);
        }
    }

    /** 并发取消可在验证期间发生，创建和提交 session 前后都再次检查。 */
    private static void ensureActive(Operation operation) {
        if (operation.cancelled) throw new IllegalStateException("Install operation was cancelled.");
        synchronized (NovaNativeAppInstallerBridge.class) {
            if (ACTIVE.get(operation.id) != operation) throw new IllegalStateException("Install operation is no longer active.");
        }
    }

    /** 一次终态清理；取消 PendingIntent、放弃自有 session、删暂存、清 callback 与持久 marker。 */
    private static void finish(Operation operation, int status, String message, boolean abandon) {
        Callback callback;
        synchronized (NovaNativeAppInstallerBridge.class) {
            if (ACTIVE.get(operation.id) != operation) return;
            if (operation.cancelled) return;
            operation.cancelled = true;
            callback = operation.callback; operation.callback = null;
        }
        synchronized (operation) {
            MAIN.removeCallbacks(operation.timeout);
            if (operation.pendingIntent != null) operation.pendingIntent.cancel();
            if (operation.sessionId >= 0 && abandon) {
                try { operation.context.getPackageManager().getPackageInstaller().abandonSession(operation.sessionId); } catch (Exception ignored) { }
            }
            operation.context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().remove(SESSION_KEY).commit();
            if (operation.staged != null) operation.staged.delete();
        }
        synchronized (NovaNativeAppInstallerBridge.class) { ACTIVE.remove(operation.id); }
        dispatch(callback, status, message);
    }

    /** 比较证书 DER 全字节，不把签名证书元数据读取当文件验签。 */
    private static String hex(byte[] bytes) {
        StringBuilder result = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) result.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
        return result.toString();
    }

    /** JNI proxy 回调统一派发 Android 主线程，回调引用只保留到本次终态。 */
    private static void dispatch(Callback callback, int status, String message) {
        if (callback != null) MAIN.post(() -> callback.onComplete(status, message == null ? "" : message));
    }

    private static final class Operation {
        final Context context; final WeakReference<Activity> activity; final String id;
        volatile boolean cancelled; volatile File staged; int sessionId = -1; PendingIntent pendingIntent; Callback callback; Runnable timeout;
        Operation(Activity activity, String id, Callback callback) {
            this.context = activity.getApplicationContext(); this.activity = new WeakReference<>(activity); this.id = id; this.callback = callback;
        }
    }
}
