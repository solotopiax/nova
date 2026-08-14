using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Cysharp.Threading.Tasks;

using NovaFramework.BestHTTP.Runtime;
using NovaFramework.Runtime;

using NUnit.Framework;

using UnityEngine;

namespace NovaFramework.BestHTTP.Tests
{
    public sealed class NovaBestHttpTelemetrySinkTests
    {
        [TearDown]
        public void TearDown()
        {
            BestHttpTelemetryRegistration.Reset();
        }

        [Test]
        public void Registration_InstallsOwnedSinkAndResetRemovesIt()
        {
            BestHttpTelemetryRegistration.Reset();

            BestHttpTelemetryRegistration.Register();

            if (GetInternalEventHandlerProperty() == null)
            {
                Assert.Pass("当前 BestHTTP 未提供内部遥测委托，注册器应静默跳过。");
            }

            Delegate eventHandler = GetInternalEventHandler();
            Assert.That(eventHandler, Is.Not.Null);
            Assert.That(eventHandler.Target, Is.TypeOf<NovaBestHttpTelemetrySink>());

            BestHttpTelemetryRegistration.Reset();

            Assert.That(GetInternalEventHandler(), Is.Null);
        }

        [Test]
        public void Registration_SeparatesSinkInstallationFromUniTaskReadinessWatch()
        {
            Assert.That(
                GetRuntimeInitializeLoadType(nameof(BestHttpTelemetryRegistration.Register)),
                Is.EqualTo(RuntimeInitializeLoadType.AfterAssembliesLoaded));
            Assert.That(
                GetRuntimeInitializeLoadType("StartReadinessWatch"),
                Is.EqualTo(RuntimeInitializeLoadType.BeforeSceneLoad));
        }

        [Test]
        public void BusinessTelemetryCapability_InternalBestHttpCreatesScope()
        {
            var transport = new BestHttpTransport();

            using IBusinessHttpTelemetryScope scope = transport.BeginBusinessHttpTelemetry("account_login");

            Assert.That(scope, Is.Not.Null, "内部 BestHTTP 3.0.20 应提供业务整链遥测入口。");
        }

        [Test]
        public void Track_WhenDisabled_DoesNotBufferOrDispatch()
        {
            var plugin = new RecordingTrackPlugin();
            var sink = CreateSink(
                isEnabled: () => false,
                isReady: () => false,
                plugins: () => new[] { plugin });

            sink.Track("disabled", null);

            Assert.That(sink.PendingCount, Is.Zero);
            Assert.That(plugin.Events, Is.Empty);
        }

        [Test]
        public void Track_WhenReady_FansOutIndependentPropertyCopies()
        {
            var first = new RecordingTrackPlugin(mutateProperties: true);
            var second = new RecordingTrackPlugin();
            var sink = CreateSink(
                isEnabled: () => true,
                isReady: () => true,
                plugins: () => new ITrackPlugin[] { first, second });

            sink.Track("ready", CreateProperties("value"));

            Assert.That(first.Events.Select(item => item.Name), Is.EqualTo(new[] { "ready" }));
            Assert.That(second.Events.Select(item => item.Name), Is.EqualTo(new[] { "ready" }));
            Assert.That(second.Events[0].Properties["key"], Is.EqualTo("value"));
        }

        [Test]
        public void FlushPendingIfReady_PreservesFifoOrder()
        {
            bool ready = false;
            var plugin = new RecordingTrackPlugin();
            var sink = CreateSink(
                isEnabled: () => true,
                isReady: () => ready,
                plugins: () => new[] { plugin });

            sink.Track("first", null);
            sink.Track("second", null);
            ready = true;
            sink.FlushPendingIfReady();

            Assert.That(sink.PendingCount, Is.Zero);
            Assert.That(plugin.Events.Select(item => item.Name), Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void Track_WhenOnePluginThrows_ContinuesOtherPlugins()
        {
            var throwing = new RecordingTrackPlugin(throwOnTrack: true);
            var recording = new RecordingTrackPlugin();
            var sink = CreateSink(
                isEnabled: () => true,
                isReady: () => true,
                plugins: () => new ITrackPlugin[] { throwing, recording });

            Assert.DoesNotThrow(() => sink.Track("isolated", null));

            Assert.That(recording.Events.Select(item => item.Name), Is.EqualTo(new[] { "isolated" }));
        }

        [Test]
        public void Track_WhenBufferOverflows_KeepsNewest128Events()
        {
            bool ready = false;
            var plugin = new RecordingTrackPlugin();
            var sink = CreateSink(
                isEnabled: () => true,
                isReady: () => ready,
                plugins: () => new[] { plugin });

            for (int i = 0; i < 130; i++)
                sink.Track("event-" + i, null);

            Assert.That(sink.PendingCount, Is.EqualTo(128));

            ready = true;
            sink.FlushPendingIfReady();

            Assert.That(plugin.Events.Count, Is.EqualTo(128));
            Assert.That(plugin.Events[0].Name, Is.EqualTo("event-2"));
            Assert.That(plugin.Events[127].Name, Is.EqualTo("event-129"));
        }

        private static NovaBestHttpTelemetrySink CreateSink(
            Func<bool> isEnabled,
            Func<bool> isReady,
            Func<IReadOnlyList<ITrackPlugin>> plugins)
        {
            return new NovaBestHttpTelemetrySink(isEnabled, isReady, plugins);
        }

        /// <summary>
        /// 读取指定自动初始化方法声明的 Unity 生命周期阶段。
        /// </summary>
        /// <param name="methodName">BestHTTP 遥测注册器中的静态方法名。</param>
        /// <returns>该方法声明的运行时初始化阶段。</returns>
        private static RuntimeInitializeLoadType GetRuntimeInitializeLoadType(string methodName)
        {
            MethodInfo method = typeof(BestHttpTelemetryRegistration).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到自动初始化方法：{methodName}");

            CustomAttributeData attribute = method.GetCustomAttributesData()
                .Single(item => item.AttributeType == typeof(RuntimeInitializeOnLoadMethodAttribute));
            return (RuntimeInitializeLoadType)attribute.ConstructorArguments[0].Value;
        }

        private static Dictionary<string, object> CreateProperties(string value)
        {
            return value == null
                ? null
                : new Dictionary<string, object> { { "key", value } };
        }

        /// <summary>
        /// 通过反射读取内部 BestHTTP 的标准遥测委托，避免测试程序集依赖其专属类型。
        /// </summary>
        private static Delegate GetInternalEventHandler()
        {
            return GetInternalEventHandlerProperty()?.GetValue(null) as Delegate;
        }

        /// <summary>
        /// 获取内部 BestHTTP 的标准遥测委托属性；官方原版返回 null。
        /// </summary>
        private static PropertyInfo GetInternalEventHandlerProperty()
        {
            Type telemetryType = typeof(Best.HTTP.HTTPRequest).Assembly.GetType(
                "Best.HTTP.Telemetry.BestHttpTelemetry",
                false);
            return telemetryType?.GetProperty("EventHandler", BindingFlags.Public | BindingFlags.Static);
        }

        private sealed class RecordingTrackPlugin : ITrackPlugin
        {
            private readonly bool m_ThrowOnTrack;
            private readonly bool m_MutateProperties;

            public RecordingTrackPlugin(bool throwOnTrack = false, bool mutateProperties = false)
            {
                m_ThrowOnTrack = throwOnTrack;
                m_MutateProperties = mutateProperties;
            }

            public string Name => "Recording";
            public int Priority => 0;
            public bool IsAvailable => true;
            public List<RecordedEvent> Events { get; } = new List<RecordedEvent>();

            public void SetUserId(string userId) { }
            public void SetUserProperty(string key, string value) { }
            public void TrackEvent(TrackEvent evt) => TrackEvent(evt.Name, evt.Parameters);

            public void TrackEvent(string eventName, Dictionary<string, object> parameters)
            {
                if (m_ThrowOnTrack)
                    throw new InvalidOperationException("expected test failure");

                Events.Add(new RecordedEvent(eventName, new Dictionary<string, object>(parameters)));
                if (m_MutateProperties)
                    parameters["key"] = "mutated";
            }

            public UniTask InitializeAsync(ISDKPluginConfig config, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisposeAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<object> FetchDataAsync(string key, CancellationToken ct = default) => UniTask.FromResult<object>(null);
        }

        private sealed class RecordedEvent
        {
            public RecordedEvent(string name, Dictionary<string, object> properties)
            {
                Name = name;
                Properties = properties;
            }

            public string Name { get; }
            public Dictionary<string, object> Properties { get; }
        }
    }
}
