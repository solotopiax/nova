using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using NovaFramework.SDK.AdMobAdPlugin.Runtime;
using AdFormat = NovaFramework.Runtime.AdFormat;

using System.Threading.Tasks;
using NUnit.Framework;

namespace NovaFramework.Tests.Editor
{
public sealed class AdMobOwnedObjectTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type Plugin = typeof(AdMobAdPlugin);
    private static readonly Type Session = Plugin.GetNestedType("AdSession", BindingFlags.NonPublic);
    /// <summary>检查实际生命周期状态，失败交给 NUnit 报告。</summary>
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
    /// <summary>读取测试涉及的确切自有字段，不调用原厂静态入口。</summary>
    private static object Read(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    /// <summary>向内存自有对象设置测试身份或完成源，不写项目资产。</summary>
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    /// <summary>仅调用本包确切生命周期方法，不接受外部代码或广告网络目标。</summary>
    private static object Call(object target, string name, params object[] args) => Plugin.GetMethod(name, Flags).Invoke(target, args);
    /// <summary>创建一条内存广告记录并赋予本轮身份，不生成原厂资源。</summary>
    private static object Add(AdMobAdPlugin plugin, string id, AdFormat format = AdFormat.Rewarded)
    {
        object lifetime = Read(plugin, "m_Lifetime");
        if (lifetime == null) { lifetime = new object(); Write(plugin, "m_Lifetime", lifetime); }
        object session = Activator.CreateInstance(Session, Flags, null, new object[] { format, id, lifetime, CancellationToken.None }, null);
        ((IDictionary)Read(plugin, "m_Ads")).Add(id, session);
        return session;
    }
    /// <summary>只创建原厂 null-client C# 对象，验证准确回调释放、奖励关闭、晚到资源、重复展示拒绝及初始化阶段异常/取消。</summary>
    [Test]
    public async Task OwnedObjectsAndInitializationCallbacksRespectLifetime()
    {
        // 只构造当前自有渠道和未初始化的官方 C# 数据对象；不调用 MobileAds/UMP 静态入口。
        var report = new Dictionary<string, object>();
        var plugin = new AdMobAdPlugin();
        var gatePlugin = new AdMobAdPlugin();
        try
        {
            object late = Add(plugin, "late"); Write(late, "Active", false);
            int destroyed = 0, accepted = 0;
            Call(plugin, "AcceptNativeAd", late, (Action)(() => destroyed++), (Action)(() => accepted++));
            Check(destroyed == 1 && accepted == 0, "late-loaded object not destroyed exactly once");
            ((IDictionary)Read(plugin, "m_Ads")).Clear();
            report["lateLoadCleanup"] = true;

            object session = Add(plugin, "rewarded");
    #pragma warning disable SYSLIB0050
            var rewarded = (RewardedAd)FormatterServices.GetUninitializedObject(typeof(RewardedAd));
    #pragma warning restore SYSLIB0050
            Call(plugin, "AcceptRewarded", session, rewarded, null);
            Check(((List<Action>)Read(session, "Unsubscribe")).Count == 5, "official delegates not paired");
            Check((bool)Read(session, "Loaded"), "accept did not mark loaded");
            var completion = new UniTaskCompletionSource<AdResult>(); Write(session, "Showing", completion);
            Call(plugin, "OnRequestAsync", AdFormat.Rewarded, "rewarded", CancellationToken.None);
            Check(ReferenceEquals(((IDictionary)Read(plugin, "m_Ads"))["rewarded"], session), "pending native show replaced by request");
            Write(session, "Rewarded", true); Call(plugin, "Closed", session);
            AdResult result = completion.Task.GetAwaiter().GetResult();
            Check(result.Success && result.UserCompleted && result.RewardGranted, "reward not retained at close");
            Check(!(bool)Read(session, "Active") && ((List<Action>)Read(session, "Unsubscribe")).Count == 0, "close did not release precise handlers");
            Check(((IDictionary)Read(plugin, "m_Ads")).Count == 0, "closed object retained");
            report["rewardedCloseAndNativeShowOwnership"] = true;

            object a = Add(plugin, "release-rv"); object b = Add(plugin, "release-inter", AdFormat.Interstitial);
            int destroyAll = 0, unsubscribeAll = 0;
            var pending = new UniTaskCompletionSource<AdResult>(); Write(a, "Showing", pending);
            foreach (object item in new[] { a, b })
            {
                Write(item, "Destroy", (Action)(() => destroyAll++));
                ((List<Action>)Read(item, "Unsubscribe")).Add(() => unsubscribeAll++);
            }
            ((IAdChannelLifecycle)plugin).DisposeAsync(CancellationToken.None).GetAwaiter().GetResult();
            ((IAdChannelLifecycle)plugin).DisposeAsync(new CancellationToken(true)).GetAwaiter().GetResult();
            Check(destroyAll == 2 && unsubscribeAll == 2, "owned release not exactly once");
            Check(((IDictionary)Read(plugin, "m_Ads")).Count == 0 && Read(plugin, "m_Lifetime") == null, "release state retained");
            Check(pending.Task.Status == UniTaskStatus.Canceled, "release pending show not canceled");
            int afterReleaseDestroy = 0;
            Call(plugin, "AcceptNativeAd", a, (Action)(() => afterReleaseDestroy++), (Action)(() => accepted++));
            Check(afterReleaseDestroy == 1 && accepted == 0, "post-dispose late load skipped resource cleanup");
            report["releaseAllAndLateLoadAfterDispose"] = true;
            object gateSession = Add(gatePlugin, "gate");
            var gate = new UniTaskCompletionSource();
            object gateLifetime = Read(gatePlugin, "m_Lifetime");
            Call(gatePlugin, "PostInitialization", gateLifetime, CancellationToken.None, (Action)(() => throw new InvalidOperationException("probe init stage")), gate);
            var canceledGate = new UniTaskCompletionSource();
            int canceledAction = 0;
            Call(gatePlugin, "PostInitialization", gateLifetime, new CancellationToken(true), (Action)(() => canceledAction++), canceledGate);
            // 同一原厂展示仍被持有时，第二次 Show 不会掉入原厂加载或误发布一个失败续杯。
            Write(gateSession, "Showing", new UniTaskCompletionSource<AdResult>());
            var secondShow = (UniTask<AdResult>)Call(gatePlugin, "OnShowAsync", AdFormat.Rewarded, "gate", CancellationToken.None);
            bool showingRejected = false;
            try { secondShow.GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { showingRejected = true; }
            Check(showingRejected, "second native show not rejected");
            report["initExceptionCompletion"] = gate;
            report["canceledInitCompletion"] = canceledGate;
            report["canceledInitAction"] = (Func<int>)(() => canceledAction);
            report["gatePlugin"] = gatePlugin;
            report["nativeShowingSecondCallRejected"] = true;
            report["networkCalls"] = 0;
            await UniTask.Yield();
            await UniTask.Yield();
            Check(gate.Task.Status == UniTaskStatus.Faulted, "init stage exception left pending");
            Assert.Throws<InvalidOperationException>(() => gate.Task.GetAwaiter().GetResult());
            Check(canceledGate.Task.Status == UniTaskStatus.Pending && canceledAction == 0, "canceled init action executed");
        }
        finally
        {
            ((IAdChannelLifecycle)plugin).DisposeAsync(CancellationToken.None).GetAwaiter().GetResult();
            ((IAdChannelLifecycle)gatePlugin).DisposeAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>渠道初始化前置取消或配置校验失败均结束内部隐私等待，原始异常继续向调用方传播。</summary>
    /// <param name="canceled">为 true 时验证进入渠道时已取消，为 false 时验证配置类型不匹配。</param>
    [TestCase(false)]
    [TestCase(true)]
    public void InitializationValidationFailureCompletesPrivacy(bool canceled)
    {
        var plugin = new AdMobAdPlugin();
        try
        {
            var initialization = (UniTask)Call(plugin, "InitChannelSDKAsync", null, new CancellationToken(canceled));
            if (canceled) Assert.Throws<OperationCanceledException>(() => initialization.GetAwaiter().GetResult());
            else Assert.Throws<ArgumentException>(() => initialization.GetAwaiter().GetResult());
            Assert.That(plugin.WaitForPrivacyFlowAsync().Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }
        finally { ((IAdChannelLifecycle)plugin).DisposeAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    }
}

}
