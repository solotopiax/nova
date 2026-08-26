/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePushTaskTests.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase push task cache and flush contract tests
 ***************************************************************/

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NovaFramework.SDK.FirebasePlugin.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Tests
{
    /// <summary>
    /// Firebase push task 缓存和批量发送契约测试。
    /// 覆盖业务入口、缓存版本、初始化与用户身份门槛、阈值配置和协议 Service 接线。
    /// </summary>
    public sealed class FirebasePushTaskTests
    {
        private const string c_RuntimeFolder = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/";
        private const string c_EditorFolder = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Editor/";
        private const string c_PushTasksFolder = c_RuntimeFolder + "PushTasks/";
        private const string c_ServicesFolder = c_RuntimeFolder + "Services/";

        private const string c_FirebasePluginSourcePath = c_RuntimeFolder + "FirebasePlugin.cs";
        private const string c_FirebasePluginMethodsSourcePath = c_RuntimeFolder + "FirebasePlugin.Methods.cs";
        private const string c_FirebasePluginVisitorsSourcePath = c_RuntimeFolder + "FirebasePlugin.Visitors.cs";
        private const string c_FirebasePluginConfigSourcePath = c_RuntimeFolder + "FirebasePluginConfig.cs";
        private const string c_FirebasePushTaskInterfaceSourcePath = c_PushTasksFolder + "IFirebasePushTaskPlugin.cs";
        private const string c_FirebasePushTaskSourcePath = c_PushTasksFolder + "FirebasePushTask.cs";
        private const string c_FirebasePushTaskCacheEntrySourcePath = c_PushTasksFolder + "FirebasePushTaskCacheEntry.cs";
        private const string c_FirebasePushTaskDispatcherSourcePath = c_PushTasksFolder + "FirebasePushTaskDispatcher.cs";
        private const string c_FirebasePushTaskRepositorySourcePath = c_PushTasksFolder + "FirebasePushTaskRepository.cs";
        private const string c_FirebasePushTaskNetServiceSourcePath = c_ServicesFolder + "FirebasePushTaskNetService.cs";
        private const string c_FirebasePluginPushTasksSourcePath = c_PushTasksFolder + "FirebasePlugin.PushTasks.cs";
        private const string c_FirebaseEditorDestroyFixSourcePath = c_EditorFolder + "FirebaseEditorDestroyFix.cs";

        /// <summary>
        /// FCM notification click tracking should reuse the generic track plugin and keep the template id payload.
        /// </summary>
        [Test]
        public void FirebaseNotificationClick_TracksNovaFcmClickThroughTrackPlugin()
        {
            string methodsSource = File.ReadAllText(c_FirebasePluginMethodsSourcePath);

            StringAssert.Contains("TryGetValue(\"push_task_id\"", methodsSource);
            StringAssert.Contains("TryGetValue(\"task_key\"", methodsSource);
            StringAssert.Contains("TryGetValue(\"template_id\"", methodsSource);
            StringAssert.Contains("TryGet<ITrackPlugin>", methodsSource);
            StringAssert.Contains("TrackEvent(\"nova_firebase_fcm_click\"", methodsSource);
            StringAssert.Contains("nova_firebase_fcm_message_id", methodsSource);
            StringAssert.Contains("nova_firebase_push_task_id", methodsSource);
            StringAssert.Contains("nova_firebase_push_task_key", methodsSource);
            StringAssert.Contains("nova_firebase_template_id", methodsSource);
            StringAssert.DoesNotContain("TryGetValue(\"TemplateID\"", methodsSource);
            StringAssert.DoesNotContain("nova_firebase_fcm_order_id", methodsSource);
            StringAssert.DoesNotContain("TryGetValue(\"OrderID\"", methodsSource);
            StringAssert.DoesNotContain("nova_firebase_fcm_messengerid", methodsSource);
            StringAssert.DoesNotContain("GetAll<ITrackPlugin>", methodsSource);
            StringAssert.DoesNotContain("TGAHelper", methodsSource);
        }

        /// <summary>
        /// 配置应暴露 push task 协议名、时间阈值和数量阈值，并提供默认值。
        /// </summary>
        [Test]
        public void FirebasePluginConfig_ExposesPushTaskFlushSettings()
        {
            FirebasePluginConfig config = new FirebasePluginConfig();

            Assert.AreEqual(100f, config.PushFlushIntervalSeconds);
            Assert.AreEqual(5, config.PushFlushBatchSize);
            Assert.IsTrue(config.AutoRequestNotificationPermission);

            string source = File.ReadAllText(c_FirebasePluginConfigSourcePath);
            StringAssert.Contains("m_PushCmdName", source);
            StringAssert.Contains("m_PushFlushIntervalSeconds", source);
            StringAssert.Contains("m_PushFlushBatchSize", source);
            StringAssert.Contains("m_AutoRequestNotificationPermission = true", source);
            StringAssert.Contains("Tooltip", source);
        }

        /// <summary>
        /// 验证 Firebase 通知权限请求由配置开关控制，默认会在初始化成功后走 Native 门面请求。
        /// </summary>
        [Test]
        public void FirebasePlugin_RequestsNotificationPermissionWhenConfigEnabled()
        {
            string methodsSource = File.ReadAllText(c_FirebasePluginMethodsSourcePath);

            int initOverIndex = methodsSource.IndexOf("m_InitOver = true;", StringComparison.Ordinal);
            int requestIndex = methodsSource.IndexOf("RequestDefaultNotificationPermissionIfEnabled().Forget();", initOverIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(initOverIndex, 0);
            Assert.Greater(requestIndex, initOverIndex);
            StringAssert.Contains("!m_RuntimeConfig.AutoRequestNotificationPermission", methodsSource);
            StringAssert.Contains("AutoRequestNotificationPermission", methodsSource);
            StringAssert.Contains("Nova.Native.RequestNotificationPermissionAsync()", methodsSource);
        }

        /// <summary>
        /// Editor 退出播放模式清理必须在域重载后自动注册。
        /// </summary>
        [Test]
        public void FirebaseEditorDestroyFix_RegistersOnEditorLoad()
        {
            string source = File.ReadAllText(c_FirebaseEditorDestroyFixSourcePath);

            StringAssert.Contains("[InitializeOnLoad]", source);
            StringAssert.Contains("EditorApplication.playModeStateChanged", source);
            StringAssert.Contains("DestroyImmediate(handler)", source);
            StringAssert.DoesNotContain("GetMethod(\"Destroy\"", source);
        }

        /// <summary>
        /// Firebase push task 应通过 Firebase 专用插件接口暴露，避免污染通用 IPushPlugin。
        /// </summary>
        [Test]
        public void FirebasePlugin_ExposesFirebasePushTaskInterface()
        {
            Assert.IsTrue(File.Exists(c_FirebasePushTaskInterfaceSourcePath));
            Assert.IsTrue(File.Exists(c_FirebasePushTaskSourcePath));

            string pluginSource = File.ReadAllText(c_FirebasePluginSourcePath);
            string interfaceSource = File.ReadAllText(c_FirebasePushTaskInterfaceSourcePath);
            string taskSource = File.ReadAllText(c_FirebasePushTaskSourcePath);

            StringAssert.Contains("IFirebasePushTaskPlugin", pluginSource);
            StringAssert.Contains("QueuePushTaskAsync", pluginSource);
            StringAssert.Contains("QueuePushTaskAsync(FirebasePushTask task", interfaceSource);
            StringAssert.Contains("public string TaskKey", taskSource);
            StringAssert.Contains("public long TriggerTime", taskSource);
            StringAssert.Contains("public bool Cancel", taskSource);
            StringAssert.Contains("public long TemplateId", taskSource);
        }

        /// <summary>
        /// FirebasePlugin 应复用 SDK 生命周期接口，在从后台恢复前台时主动请求发送当前 push task 缓存。
        /// </summary>
        [Test]
        public void FirebasePluginPushTasks_FlushesCachedTasksWhenReturningToForeground()
        {
            Assert.IsTrue(File.Exists(c_FirebasePluginSourcePath));
            Assert.IsTrue(File.Exists(c_FirebasePluginPushTasksSourcePath));
            Assert.IsTrue(File.Exists(c_FirebasePushTaskDispatcherSourcePath));

            string pluginSource = File.ReadAllText(c_FirebasePluginSourcePath);
            string visitorsSource = File.ReadAllText(c_FirebasePluginVisitorsSourcePath);
            string pushTasksSource = File.ReadAllText(c_FirebasePluginPushTasksSourcePath);
            string dispatcherSource = File.ReadAllText(c_FirebasePushTaskDispatcherSourcePath);

            StringAssert.Contains("ISDKPauseListener", pluginSource);
            StringAssert.Contains("m_WasApplicationPaused", visitorsSource);
            StringAssert.Contains("public void OnPause(bool isPaused)", pluginSource);
            StringAssert.Contains("if (isPaused)", pluginSource);
            StringAssert.Contains("if (!m_WasApplicationPaused)", pluginSource);
            StringAssert.Contains("RequestPushTaskFlushOnForeground()", pluginSource);
            StringAssert.Contains("RequestPushTaskFlushOnForeground", pushTasksSource);
            StringAssert.Contains("FlushAllCachedTasks", dispatcherSource);
        }

        /// <summary>
        /// 本地缓存应使用 task_key 作为主键，并使用 int CacheVersion 保护发送中覆盖的消息。
        /// </summary>
        [Test]
        public void FirebasePushTaskCache_UsesTaskKeyAndIntCacheVersion()
        {
            Assert.IsTrue(File.Exists(c_FirebasePushTaskCacheEntrySourcePath));
            Assert.IsTrue(File.Exists(c_FirebasePushTaskRepositorySourcePath));

            string entrySource = File.ReadAllText(c_FirebasePushTaskCacheEntrySourcePath);
            string repositorySource = File.ReadAllText(c_FirebasePushTaskRepositorySourcePath);

            StringAssert.Contains("public int CacheVersion", entrySource);
            StringAssert.DoesNotContain("long CacheVersion", entrySource);
            StringAssert.Contains("FirebasePushTasks", repositorySource);
            StringAssert.Contains("task.TaskKey", repositorySource);
            StringAssert.Contains("RemoveSucceededSnapshotItems", repositorySource);
            StringAssert.Contains("currentEntry.CacheVersion == snapshotItem.CacheVersion", repositorySource);
        }

        /// <summary>
        /// Firebase 初始化完成后不能直接发送 push task；必须等 SetUserId 成功同步用户身份后才触发 flush。
        /// </summary>
        [Test]
        public void FirebasePluginPushTasks_WaitsForSetUserIdBeforeFlush()
        {
            Assert.IsTrue(File.Exists(c_FirebasePluginPushTasksSourcePath));

            string methodsSource = File.ReadAllText(c_FirebasePluginMethodsSourcePath);
            string pluginSource = File.ReadAllText(c_FirebasePluginSourcePath);
            string visitorsSource = File.ReadAllText(c_FirebasePluginVisitorsSourcePath);
            string pushTasksSource = File.ReadAllText(c_FirebasePluginPushTasksSourcePath);
            string dispatcherSource = File.ReadAllText(c_FirebasePushTaskDispatcherSourcePath);

            StringAssert.Contains("InitializePushTaskServices()", methodsSource);
            StringAssert.DoesNotContain("RequestPushTaskFlush();\r\n\r\n                        Firebase.Analytics", methodsSource);
            StringAssert.DoesNotContain("RequestPushTaskFlush();\n\n                        Firebase.Analytics", methodsSource);
            int initOverIndex = methodsSource.IndexOf("m_InitOver = true;", StringComparison.Ordinal);
            int firebaseReadyIndex = methodsSource.IndexOf("m_PushTaskDispatcher?.SetFirebaseReady(true);", initOverIndex, StringComparison.Ordinal);
            int applyPendingUserIdIndex = methodsSource.IndexOf("ApplyPendingUserIdIfReady();", initOverIndex, StringComparison.Ordinal);
            int startDefaultTopicSyncIndex = methodsSource.IndexOf("StartDefaultTopicSync();", initOverIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(initOverIndex, 0);
            Assert.Greater(firebaseReadyIndex, initOverIndex);
            Assert.Greater(applyPendingUserIdIndex, firebaseReadyIndex);
            Assert.Greater(startDefaultTopicSyncIndex, applyPendingUserIdIndex);
            StringAssert.Contains("m_PendingUserId", visitorsSource);
            StringAssert.Contains("ApplyPendingUserIdIfReady()", methodsSource);
            int setUserIdIndex = pluginSource.IndexOf("public void SetUserId(string userId)", StringComparison.Ordinal);
            int emptyGuardIndex = pluginSource.IndexOf("string.IsNullOrWhiteSpace(userId)", setUserIdIndex, StringComparison.Ordinal);
            int assignUserIdIndex = pluginSource.IndexOf("m_PendingUserId = userId", setUserIdIndex, StringComparison.Ordinal);
            int applyUserIdIndex = pluginSource.IndexOf("ApplyPendingUserIdIfReady();", setUserIdIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(setUserIdIndex, 0);
            Assert.Greater(emptyGuardIndex, setUserIdIndex);
            Assert.Greater(assignUserIdIndex, emptyGuardIndex);
            Assert.Greater(applyUserIdIndex, assignUserIdIndex);

            StringAssert.DoesNotContain("m_PushTaskDispatcher?.SetUserReady(false)", methodsSource);
            StringAssert.Contains("m_PushTaskDispatcher?.SetUserReady();", methodsSource);
            StringAssert.Contains("public void SetUserReady()", dispatcherSource);
            StringAssert.DoesNotContain("SetUserReady(bool", dispatcherSource);
            StringAssert.Contains("m_FlushRunning", dispatcherSource);
            StringAssert.Contains("m_FlushRequested", dispatcherSource);
            StringAssert.Contains("m_CacheVersion", dispatcherSource);
            StringAssert.Contains("m_CacheVersion == int.MaxValue", dispatcherSource);
            StringAssert.Contains("GetFlushInterval()", dispatcherSource);
            StringAssert.Contains("GetFlushBatchSize()", dispatcherSource);
            StringAssert.Contains("RunFlushLoopAsync", dispatcherSource);
            StringAssert.Contains("!m_FirebaseReady || !m_UserReady || m_FlushRunning", dispatcherSource);
        }

        /// <summary>
        /// push task 的缓存、锁、计时器和 flush 状态应集中在内部 dispatcher，避免插件字段区继续膨胀。
        /// </summary>
        [Test]
        public void FirebasePluginPushTasks_UsesDispatcherForRuntimeState()
        {
            Assert.IsTrue(File.Exists(c_FirebasePushTaskDispatcherSourcePath));

            string visitorsSource = File.ReadAllText(c_FirebasePluginVisitorsSourcePath);
            string pushTasksSource = File.ReadAllText(c_FirebasePluginPushTasksSourcePath);
            string methodsSource = File.ReadAllText(c_FirebasePluginMethodsSourcePath);
            string dispatcherSource = File.ReadAllText(c_FirebasePushTaskDispatcherSourcePath);

            StringAssert.Contains("FirebasePushTaskDispatcher", dispatcherSource);
            StringAssert.Contains("m_PushTaskDispatcher", visitorsSource);
            StringAssert.Contains("m_PushTaskDispatcher.Initialize", pushTasksSource);
            StringAssert.Contains("m_PushTaskDispatcher?.SetFirebaseReady", methodsSource);
            StringAssert.Contains("m_PushTaskDispatcher?.SetUserReady", methodsSource);
            StringAssert.DoesNotContain("m_PushTaskRepository", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskNetService", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskCts", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskCacheLock", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskStateLock", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskFlushRunning", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskFlushRequested", visitorsSource);
            StringAssert.DoesNotContain("m_PushTaskTimerRunning", visitorsSource);
        }

        /// <summary>
        /// push task 网络 Service 应发送 PbNetCreatePushTasksReq，并按 NetResponse.IsSuccess 判断整批成功。
        /// </summary>
        [Test]
        public void FirebasePushTaskNetService_SendsCreatePushTasksProtocol()
        {
            Assert.IsTrue(File.Exists(c_FirebasePushTaskNetServiceSourcePath));

            string source = File.ReadAllText(c_FirebasePushTaskNetServiceSourcePath);

            StringAssert.Contains("PbNetCreatePushTasksReq", source);
            StringAssert.Contains("PbPushTask", source);
            StringAssert.Contains("BuildPushTaskMessage", source);
            StringAssert.Contains("PbNetCreatePushTasksResp.Parser", source);
            StringAssert.Contains("NetService.SendAsync", source);
            StringAssert.DoesNotContain("PbPushTaskResult", source);
        }

        /// <summary>
        /// 取消任务时协议层只允许携带 task_key 和 cancel，不应把触发时间或模板 ID 写入 PbPushTask。
        /// </summary>
        [Test]
        public void FirebasePushTaskNetService_OmitsScheduleFieldsWhenCanceling()
        {
            MethodInfo buildMethod = typeof(FirebasePushTaskNetService).GetMethod("BuildPushTaskMessage", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(buildMethod, "FirebasePushTaskNetService 应提供底层 PbPushTask 构造方法。");

            var task = new FirebasePushTask
            {
                TaskKey = "daily_reward",
                TriggerTime = 1910000000,
                Cancel = true,
                TemplateId = 7,
            };

            var message = (PbPushTask)buildMethod.Invoke(null, new object[] { task });

            Assert.AreEqual("daily_reward", message.TaskKey);
            Assert.IsTrue(message.Cancel);
            Assert.AreEqual(0L, message.TriggerTime, "Cancel=true 时不允许传 trigger_time。");
            Assert.AreEqual(0L, message.TemplateId, "Cancel=true 时不允许传 template_id。");
            Assert.AreEqual(16, message.CalculateSize(), "Cancel=true 的 PbPushTask 序列化内容应只有 task_key 和 cancel。");
        }

        /// <summary>
        /// push task 协议发送和成功响应后的缓存删除都应有日志，便于启动日志中确认协议是否发出。
        /// </summary>
        [Test]
        public void FirebasePushTaskFlush_LogsProtocolSendAndCacheRemoval()
        {
            Assert.IsTrue(File.Exists(c_FirebasePushTaskNetServiceSourcePath));
            Assert.IsTrue(File.Exists(c_FirebasePushTaskDispatcherSourcePath));
            Assert.IsTrue(File.Exists(c_FirebasePushTaskRepositorySourcePath));

            string netServiceSource = File.ReadAllText(c_FirebasePushTaskNetServiceSourcePath);
            string dispatcherSource = File.ReadAllText(c_FirebasePushTaskDispatcherSourcePath);
            string repositorySource = File.ReadAllText(c_FirebasePushTaskRepositorySourcePath);

            StringAssert.Contains("Log.Info(LogTag.Firebase", netServiceSource);
            StringAssert.Contains("Firebase push task 准备发送协议", netServiceSource);
            StringAssert.Contains("PushCmdName={cmdName}", netServiceSource);
            StringAssert.Contains("TaskCount={body.Tasks.Count}", netServiceSource);

            StringAssert.Contains("Log.Info(LogTag.Firebase", dispatcherSource);
            StringAssert.Contains("Firebase push task 协议响应成功", dispatcherSource);
            StringAssert.Contains("RemoveSucceededSnapshotAsync(snapshot, ct)", dispatcherSource);
            StringAssert.Contains("Firebase push task 已删除发送成功缓存", dispatcherSource);

            StringAssert.Contains("public int RemoveSucceededSnapshotItems", repositorySource);
            StringAssert.Contains("removedCount++", repositorySource);
        }

        /// <summary>
        /// push task 只缓存但尚未发送时，也应打印等待原因，避免误判按钮点击后已经发协议。
        /// </summary>
        [Test]
        public void FirebasePushTaskFlush_LogsCacheAndGateState()
        {
            Assert.IsTrue(File.Exists(c_FirebasePushTaskDispatcherSourcePath));

            string dispatcherSource = File.ReadAllText(c_FirebasePushTaskDispatcherSourcePath);

            StringAssert.Contains("Firebase push task 已缓存", dispatcherSource);
            StringAssert.Contains("TaskKey={normalizedTask.TaskKey}", dispatcherSource);
            StringAssert.Contains("CacheCount={cacheCount}", dispatcherSource);
            StringAssert.Contains("FlushBatchSize={GetFlushBatchSize()}", dispatcherSource);
            StringAssert.Contains("FlushIntervalSeconds={GetFlushInterval().TotalSeconds}", dispatcherSource);

            StringAssert.Contains("Firebase push task 未达到数量阈值", dispatcherSource);
            StringAssert.Contains("Firebase push task 等待发送条件", dispatcherSource);
            StringAssert.Contains("FirebaseReady={m_FirebaseReady}", dispatcherSource);
            StringAssert.Contains("UserReady={m_UserReady}", dispatcherSource);
            StringAssert.Contains("FlushRunning={m_FlushRunning}", dispatcherSource);
        }
    }
}
