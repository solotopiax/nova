/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseDefaultTopicBuilderTests.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase 默认推送 Topic 构建规则测试
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NovaFramework.SDK.FirebasePlugin.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Tests
{
    /// <summary>
    /// Firebase 默认推送 Topic 构建器测试。
    /// 覆盖业务要求的 top_ 前缀、语言、平台、时区、国家和差异计算规则。
    /// </summary>
    public sealed class FirebaseDefaultTopicBuilderTests
    {
        private const string c_FirebasePluginSourcePath = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/FirebasePlugin.cs";

        private const string c_FirebaseDefaultTopicsSourcePath = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/Topics/FirebasePlugin.DefaultTopics.cs";

        private const string c_FirebasePluginMethodsSourcePath = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/FirebasePlugin.Methods.cs";

        private const string c_FirebasePluginConfigSourcePath = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/FirebasePluginConfig.cs";

        private const string c_FirebaseReportNetServiceSourcePath = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/Services/FirebaseReportNetService.cs";

        /// <summary>
        /// Firebase 推送主题公开入口应只保留 SetTopicSubscribed，避免订阅和退订暴露三套同义 API。
        /// </summary>
        [Test]
        public void FirebasePluginOnlyExposesUnifiedTopicSubscriptionApi()
        {
            string source = File.ReadAllText(c_FirebasePluginSourcePath);

            StringAssert.Contains("public void SetTopicSubscribed(string topic, bool subscribed)", source);
            StringAssert.DoesNotContain("public void SubscribeAsync(string topic)", source);
            StringAssert.DoesNotContain("public void UnsubscribeAsync(string topic)", source);
        }

        /// <summary>
        /// Firebase 配置不再承载国家码等待超时，国家码等待与缓存兜底统一收口到 AdPlugin。
        /// </summary>
        [Test]
        public void FirebasePluginConfig_DoesNotExposeCountryTopicAdWaitTimeout()
        {
            string source = File.ReadAllText(c_FirebasePluginConfigSourcePath);

            StringAssert.DoesNotContain("DefaultCountryTopicAdWaitTimeoutSeconds", source);
            StringAssert.DoesNotContain("m_DefaultCountryTopicAdWaitTimeoutSeconds", source);
            StringAssert.DoesNotContain("RegionInfo.CurrentRegion", source);
        }

        /// <summary>
        /// 国家 Topic 等待链路应只调用 IAdPlugin.GetCountryCodeAsync，不再在 Firebase 内部做 RegionInfo 或数据槽等待兜底。
        /// </summary>
        [Test]
        public void CountryTopicSync_UsesAdPluginAsyncCountryCodeOnly()
        {
            string source = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath);

            StringAssert.Contains("GetCountryCodeAsync(ct)", source);
            StringAssert.DoesNotContain("RegionInfo.CurrentRegion.TwoLetterISORegionName", source);
            StringAssert.DoesNotContain("WaitForValidAdCountryCodeAsync", source);
            StringAssert.DoesNotContain("WaitForAdPluginAsync", source);
            StringAssert.DoesNotContain("FetchDataAsync(SDKDataKeys.AdCountryCode", source);
            StringAssert.DoesNotContain("DefaultCountryTopicAdWaitTimeoutSeconds", source);
        }

        /// <summary>
        /// Language topic sync must wait for Localization to publish a real current language.
        /// </summary>
        [Test]
        public void BaseTopicSync_WaitsForLocalizationRefreshBeforeChangingLanguageTopic()
        {
            string source = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath);

            StringAssert.Contains("Subscribe<LocalizationRefreshEventData>", source);
            StringAssert.Contains("Unsubscribe<LocalizationRefreshEventData>", source);
            StringAssert.Contains("Language.Unspecified", source);
            StringAssert.Contains("oldState?.Language", source);
            StringAssert.Contains("SyncDefaultBaseTopicsAsync(ct, Language.Unspecified)", source);
        }

        /// <summary>
        /// Older startup syncs could persist the Unspecified language flag as unknown; do not preserve it.
        /// </summary>
        [Test]
        public void BaseTopicSync_DoesNotPreserveUnspecifiedLanguageFlag()
        {
            string source = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath);

            StringAssert.Contains("LanguageMetadata.GetFlag(Language.Unspecified)", source);
            StringAssert.Contains("string.Equals(persistedLanguage, s_UnspecifiedLanguageTopicFlag, StringComparison.Ordinal)", source);
        }

        /// <summary>
        /// Localization refresh is synchronous and pooled, so the handler should only copy the language value.
        /// </summary>
        [Test]
        public void LocalizationRefreshHandler_CopiesLanguageAndSchedulesAsyncTopicSync()
        {
            string source = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath);

            StringAssert.Contains("Language newLanguage = localizationRefresh.NewLanguage", source);
            StringAssert.Contains("SyncDefaultBaseTopicsAsync(ct, newLanguage).Forget()", source);
        }

        /// <summary>
        /// Startup sync and Localization refresh sync may overlap, so base topic persistence must be serialized.
        /// </summary>
        [Test]
        public void BaseTopicSync_SerializesBaseStateWrites()
        {
            string source = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath);

            StringAssert.Contains("m_DefaultBaseTopicSyncLock.WaitAsync(ct)", source);
            StringAssert.Contains("m_DefaultBaseTopicSyncLock.Release()", source);
        }

        /// <summary>
        /// Topic 操作必须等待 FCM Token，避免 iOS 首次安装时在 APNs Token 就绪前调用 Firebase。
        /// </summary>
        [Test]
        public void TopicSubscription_WaitsForFcmTokenBeforeCallingFirebase()
        {
            string topicsSource = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath);
            string methodsSource = File.ReadAllText(c_FirebasePluginMethodsSourcePath);

            int waitIndex = topicsSource.IndexOf("await WaitForFcmTokenAsync(ct);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(waitIndex, 0);

            int subscribeIndex = topicsSource.IndexOf("await FirebaseMessaging.SubscribeAsync(topic);", waitIndex, StringComparison.Ordinal);
            int unsubscribeIndex = topicsSource.IndexOf("await FirebaseMessaging.UnsubscribeAsync(topic);", waitIndex, StringComparison.Ordinal);

            Assert.Greater(subscribeIndex, waitIndex);
            Assert.Greater(unsubscribeIndex, waitIndex);
            StringAssert.Contains("m_FcmTokenReadySource", methodsSource);
            StringAssert.Contains("m_FcmTokenReadySource.TrySetResult(m_TokenReceived)", methodsSource);
        }

        /// <summary>
        /// 构建基础订阅状态时应包含 all、语言、平台和小写 utc 时区 Topic。
        /// </summary>
        [Test]
        public void BuildBaseState_UsesTopPrefixAndRequestedSegments()
        {
            object state = InvokeBuildBaseState("zh-CN", "Android", TimeSpan.FromHours(8));

            Assert.AreEqual("zh-CN", GetStringProperty(state, "Language"));
            Assert.AreEqual("Android", GetStringProperty(state, "Platform"));
            Assert.AreEqual("utc_plus_08", GetStringProperty(state, "Timezone"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "top_all",
                    "top_lang_zh-CN",
                    "top_platform_Android",
                    "top_timezone_utc_plus_08",
                },
                GetStringListProperty(state, "Topics"));
        }

        /// <summary>
        /// 时区格式化应使用小写 utc，并在存在分钟偏移时保留分钟字段。
        /// </summary>
        [Test]
        public void FormatUtcOffset_KeepsLowercaseUtcAndMinutesWhenNeeded()
        {
            Assert.AreEqual("utc_plus_08", InvokeFormatUtcOffset(TimeSpan.FromHours(8)));
            Assert.AreEqual("utc_plus_05_30", InvokeFormatUtcOffset(new TimeSpan(5, 30, 0)));
            Assert.AreEqual("utc_minus_03_30", InvokeFormatUtcOffset(new TimeSpan(-3, -30, 0)));
        }

        /// <summary>
        /// Firebase 上报协议使用服务端可读时区格式，不能复用 Topic 的 utc_plus_08 安全字符格式。
        /// </summary>
        [Test]
        public void FormatReportTimezoneOffset_UsesSignAndColonProtocolFormat()
        {
            Assert.AreEqual("+08:00", InvokeFormatReportTimezoneOffset(TimeSpan.FromHours(8)));
            Assert.AreEqual("+05:30", InvokeFormatReportTimezoneOffset(new TimeSpan(5, 30, 0)));
            Assert.AreEqual("-03:30", InvokeFormatReportTimezoneOffset(new TimeSpan(-3, -30, 0)));
            Assert.AreEqual("+00:00", InvokeFormatReportTimezoneOffset(TimeSpan.Zero));
        }

        /// <summary>
        /// Firebase 标识上报请求应把国家和服务端可读时区写入新增协议字段。
        /// </summary>
        [Test]
        public void FirebaseReportNetService_PopulatesCountryAndTimezoneOffset()
        {
            string source = File.ReadAllText(c_FirebaseReportNetServiceSourcePath);

            StringAssert.Contains("Async(string cmdName, string firebasePushToken, string firebaseAnalyticsInstanceId, string country, string timezoneOffset)", source);
            StringAssert.Contains("Country = country ?? string.Empty", source);
            StringAssert.Contains("TimezoneOffset = timezoneOffset ?? string.Empty", source);
        }

        /// <summary>
        /// Firebase 上报国家码应在发协议前归一化，空值和 IV 占位值统一发空字符串。
        /// </summary>
        [Test]
        public void NormalizeReportCountryCode_ReturnsEmptyForMissingOrIv()
        {
            Assert.AreEqual(string.Empty, InvokeNormalizeReportCountryCode(null));
            Assert.AreEqual(string.Empty, InvokeNormalizeReportCountryCode(""));
            Assert.AreEqual(string.Empty, InvokeNormalizeReportCountryCode(" IV "));
            Assert.AreEqual(string.Empty, InvokeNormalizeReportCountryCode("iv"));
            Assert.AreEqual("US", InvokeNormalizeReportCountryCode(" us "));
        }

        /// <summary>
        /// 登录后 Firebase 上报应解析国家和时区，并传入新版上报 Service。
        /// </summary>
        [Test]
        public void ReportOnLoginAsync_PassesCountryAndTimezoneOffsetToReportService()
        {
            string source = File.ReadAllText(c_FirebaseDefaultTopicsSourcePath)
                            + File.ReadAllText(c_FirebasePluginSourcePath)
                            + File.ReadAllText("UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/FirebasePlugin.Methods.cs");

            StringAssert.Contains("ResolveFirebaseCountryCodeAsync", source);
            StringAssert.Contains("FirebaseDefaultTopicBuilder.NormalizeReportCountryCode", source);
            StringAssert.Contains("FirebaseDefaultTopicBuilder.FormatReportTimezoneOffset", source);
            StringAssert.Contains("TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)", source);
            StringAssert.Contains("m_ReportNetService.Async(m_RuntimeConfig.ReportCmdName, pushToken, instanceId, country, timezoneOffset)", source);
        }

        /// <summary>
        /// 国家 Topic 应跳过空值和广告 SDK 返回的 IV 占位值。
        /// </summary>
        [Test]
        public void TryBuildCountryState_SkipsEmptyAndIvCountry()
        {
            Assert.IsFalse(InvokeTryBuildCountryState(null, out _));
            Assert.IsFalse(InvokeTryBuildCountryState("", out _));
            Assert.IsFalse(InvokeTryBuildCountryState(" IV ", out _));
            Assert.IsFalse(InvokeTryBuildCountryState("iv", out _));
        }

        /// <summary>
        /// 国家 Topic 应统一保存大写国家码，并使用 top_country_ 前缀。
        /// </summary>
        [Test]
        public void TryBuildCountryState_NormalizesCountryAndTopic()
        {
            bool built = InvokeTryBuildCountryState(" us ", out object state);

            Assert.IsTrue(built);
            Assert.AreEqual("US", GetStringProperty(state, "Country"));
            Assert.AreEqual("top_country_US", GetStringProperty(state, "Topic"));
        }

        /// <summary>
        /// Topic 差异计算应只退订旧集合独有项，只订阅新集合独有项。
        /// </summary>
        [Test]
        public void BuildTopicDiff_OnlyMovesChangedTopics()
        {
            object diff = InvokeBuildTopicDiff(
                new[]
                {
                    "top_all",
                    "top_lang_en-US",
                    "top_platform_Android",
                    "top_timezone_utc_plus_08",
                },
                new[]
                {
                    "top_all",
                    "top_lang_zh-CN",
                    "top_platform_Android",
                    "top_timezone_utc_plus_08",
                });

            CollectionAssert.AreEqual(new[] { "top_lang_en-US" }, GetStringListProperty(diff, "UnsubscribeTopics"));
            CollectionAssert.AreEqual(new[] { "top_lang_zh-CN" }, GetStringListProperty(diff, "SubscribeTopics"));
            Assert.IsFalse(GetBoolProperty(diff, "IsEmpty"));
        }

        /// <summary>
        /// Topic 差异为空时表示无需进行订阅或退订操作。
        /// </summary>
        [Test]
        public void BuildTopicDiff_ReturnsEmptyWhenTopicsAreEqual()
        {
            object diff = InvokeBuildTopicDiff(
                new[] { "top_all", "top_lang_zh-CN" },
                new[] { "top_lang_zh-CN", "top_all" });

            Assert.IsTrue(GetBoolProperty(diff, "IsEmpty"));
            CollectionAssert.IsEmpty(GetStringListProperty(diff, "UnsubscribeTopics"));
            CollectionAssert.IsEmpty(GetStringListProperty(diff, "SubscribeTopics"));
        }

        private static object InvokeBuildBaseState(string language, string platform, TimeSpan utcOffset)
        {
            return InvokeBuilderMethod("BuildBaseState", language, platform, utcOffset);
        }

        private static string InvokeFormatUtcOffset(TimeSpan utcOffset)
        {
            return (string)InvokeBuilderMethod("FormatUtcOffset", utcOffset);
        }

        private static string InvokeFormatReportTimezoneOffset(TimeSpan utcOffset)
        {
            return (string)InvokeBuilderMethod("FormatReportTimezoneOffset", utcOffset);
        }

        private static string InvokeNormalizeReportCountryCode(string countryCode)
        {
            return (string)InvokeBuilderMethod("NormalizeReportCountryCode", countryCode);
        }

        private static bool InvokeTryBuildCountryState(string countryCode, out object state)
        {
            MethodInfo method = GetBuilderMethod("TryBuildCountryState");
            object[] args = { countryCode, null };
            bool result = (bool)method.Invoke(null, args);
            state = args[1];
            return result;
        }

        private static object InvokeBuildTopicDiff(string[] oldTopics, string[] currentTopics)
        {
            return InvokeBuilderMethod("BuildTopicDiff", oldTopics, currentTopics);
        }

        private static object InvokeBuilderMethod(string methodName, params object[] args)
        {
            return GetBuilderMethod(methodName).Invoke(null, args);
        }

        private static MethodInfo GetBuilderMethod(string methodName)
        {
            MethodInfo method = GetBuilderType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, methodName + " should exist.");
            return method;
        }

        private static Type GetBuilderType()
        {
            Type type = typeof(FirebasePlugin).Assembly.GetType("NovaFramework.SDK.FirebasePlugin.Runtime.FirebaseDefaultTopicBuilder", false);
            Assert.IsNotNull(type, "FirebaseDefaultTopicBuilder should exist.");
            return type;
        }

        private static string GetStringProperty(object instance, string propertyName)
        {
            return (string)GetPropertyValue(instance, propertyName);
        }

        private static bool GetBoolProperty(object instance, string propertyName)
        {
            return (bool)GetPropertyValue(instance, propertyName);
        }

        private static List<string> GetStringListProperty(object instance, string propertyName)
        {
            List<string> result = new List<string>();
            foreach (object item in (IEnumerable)GetPropertyValue(instance, propertyName))
            {
                result.Add((string)item);
            }

            return result;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            Assert.IsNotNull(instance, propertyName + " owner should not be null.");
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, propertyName + " should exist.");
            return property.GetValue(instance);
        }
    }
}
