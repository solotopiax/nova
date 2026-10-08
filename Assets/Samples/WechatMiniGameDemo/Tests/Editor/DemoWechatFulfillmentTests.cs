/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatFulfillmentTests.cs
 * author:    Codex
 * created:   2026/10/08
 * descrip:   Demo 幂等发货、存储失败与释放行为验证
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using NovaFramework.Runtime;
using NovaFramework.SDK.WeChatMiniGame.Runtime;
using NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Tests.Editor
{
    /// <summary>仅使用内存存储，不触碰真实 PlayerPrefs、平台 SDK 或业务服务器。</summary>
    public sealed class DemoWechatFulfillmentTests
    {
        /// <summary>同一订单重复发货只增加一次模拟资产，并可返回成功。</summary>
        [Test]
        public void DuplicateOrder_DoesNotIncreaseAssetTwice()
        {
            var storage = new MemoryStorage();
            using var service = CreateService(storage);
            WeChatMiniGamePaymentDelivery delivery = CreateDelivery("order-1");
            Assert.IsTrue(service.DeliverAsync(delivery).GetAwaiter().GetResult());
            Assert.IsTrue(service.DeliverAsync(delivery).GetAwaiter().GetResult());
            Assert.AreEqual(1, storage.SaveCalls);
            Assert.AreEqual(7, ReadState(storage).Assets[0].Quantity);
            Assert.AreEqual(1, ReadState(storage).DeliveredOrders.Count);
        }

        /// <summary>存档保存失败时回滚内存，恢复保存后重试不会重复增加。</summary>
        [Test]
        public void SaveFailure_RollsBackAndRetryDeliversExactlyOnce()
        {
            var storage = new MemoryStorage { SaveSucceeds = false };
            using var service = CreateService(storage);
            WeChatMiniGamePaymentDelivery delivery = CreateDelivery("order-2");
            Assert.Throws<InvalidOperationException>(() => service.DeliverAsync(delivery).GetAwaiter().GetResult());
            Assert.AreEqual(string.Empty, storage.Value);
            storage.SaveSucceeds = true;
            Assert.IsTrue(service.DeliverAsync(delivery).GetAwaiter().GetResult());
            Assert.IsTrue(service.DeliverAsync(delivery).GetAwaiter().GetResult());
            Assert.AreEqual(7, ReadState(storage).Assets[0].Quantity);
            Assert.AreEqual(1, ReadState(storage).DeliveredOrders.Count);
        }

        /// <summary>预先取消的请求不能改写存档或确认发货。</summary>
        [Test]
        public void CancelledDelivery_DoesNotWriteStorage()
        {
            var storage = new MemoryStorage();
            using var service = CreateService(storage);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => service.DeliverAsync(CreateDelivery("cancelled"), cancellation.Token).GetAwaiter().GetResult());
            Assert.AreEqual(0, storage.SetCalls);
            Assert.AreEqual(0, storage.SaveCalls);
        }

        /// <summary>释放发货器后不再触碰存储，重复释放没有副作用。</summary>
        [Test]
        public void DisposedService_RejectsDeliveryAndClearsFeedback()
        {
            var storage = new MemoryStorage();
            var service = CreateService(storage);
            service.Feedback += _ => Assert.Fail("释放后不应回调页面。");
            service.Dispose();
            service.Dispose();
            Assert.Throws<ObjectDisposedException>(() => service.DeliverAsync(CreateDelivery("closed")).GetAwaiter().GetResult());
            Assert.AreEqual(0, storage.SetCalls);
            Assert.IsNull(GetField(service, "Feedback"));
        }

        /// <summary>注册句柄释放时解除仍匹配的发货器，并清理页面反馈及 Plugin 资源。</summary>
        [UnityTest]
        public IEnumerator RegistrationDispose_UnbindsAndDisposesService()
        {
            var storage = new MemoryStorage();
            var service = CreateService(storage);
            var commerce = (WeChatMiniGameCommerce)FormatterServices.GetUninitializedObject(typeof(WeChatMiniGameCommerce));
            SetField(commerce, "m_StateLock", new object());
            SetField(commerce, "m_Gate", new SemaphoreSlim(1, 1));
            SetField(commerce, "m_FulfillmentHandler", service);
            var plugin = new WeChatMiniGamePlugin();
            Type registrationType = typeof(WeChatMiniGamePlugin).Assembly.GetType("NovaFramework.SDK.WeChatMiniGame.Runtime.WeChatMiniGameFulfillmentRegistration", true);
            var registration = (IDisposable)Activator.CreateInstance(registrationType,
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { plugin, commerce, service }, null);
            registration.Dispose();
            registration.Dispose();
            yield return null;
            Assert.IsNull(GetField(commerce, "m_FulfillmentHandler"));
            Assert.Throws<ObjectDisposedException>(() => service.DeliverAsync(CreateDelivery("after-release")).GetAwaiter().GetResult());
        }

        /// <summary>旧句柄不能解绑随后替换的新发货器。</summary>
        [UnityTest]
        public IEnumerator OldRegistrationDispose_PreservesReplacementHandler()
        {
            var first = CreateService(new MemoryStorage());
            using var replacement = CreateService(new MemoryStorage());
            var commerce = (WeChatMiniGameCommerce)FormatterServices.GetUninitializedObject(typeof(WeChatMiniGameCommerce));
            SetField(commerce, "m_StateLock", new object());
            SetField(commerce, "m_Gate", new SemaphoreSlim(1, 1));
            SetField(commerce, "m_FulfillmentHandler", replacement);
            Type registrationType = typeof(WeChatMiniGamePlugin).Assembly.GetType("NovaFramework.SDK.WeChatMiniGame.Runtime.WeChatMiniGameFulfillmentRegistration", true);
            var registration = (IDisposable)Activator.CreateInstance(registrationType,
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new WeChatMiniGamePlugin(), commerce, first }, null);
            registration.Dispose();
            yield return null;
            Assert.AreSame(replacement, GetField(commerce, "m_FulfillmentHandler"));
        }

        /// <summary>同实例重复托管注册必须拒绝，避免一个旧句柄释放另一个注册的对象。</summary>
        [UnityTest]
        public IEnumerator RegisterSameHandlerTwice_IsRejected()
        {
            var service = CreateService(new MemoryStorage());
            var commerce = (WeChatMiniGameCommerce)FormatterServices.GetUninitializedObject(typeof(WeChatMiniGameCommerce));
            SetField(commerce, "m_StateLock", new object());
            SetField(commerce, "m_Gate", new SemaphoreSlim(1, 1));
            var plugin = new WeChatMiniGamePlugin();
            SetField(plugin, "<Commerce>k__BackingField", commerce);
            IDisposable registration = plugin.RegisterPaymentFulfillmentHandler(service);
            Assert.Throws<InvalidOperationException>(() => plugin.RegisterPaymentFulfillmentHandler(service));
            registration.Dispose();
            yield return null;
            Assert.IsNull(GetField(commerce, "m_FulfillmentHandler"));
        }

        /// <summary>通过反射注入内存存储，不增设生产公开测试接口。</summary>
        private static DemoWechatFulfillmentService CreateService(MemoryStorage storage)
        {
            var service = (DemoWechatFulfillmentService)FormatterServices.GetUninitializedObject(typeof(DemoWechatFulfillmentService));
            SetField(service, "m_Storage", storage);
            SetField(service, "m_Plugin", new WeChatMiniGamePlugin());
            return service;
        }

        /// <summary>构造框架内部发货快照，模拟已完成验单后的业务接入点。</summary>
        private static WeChatMiniGamePaymentDelivery CreateDelivery(string orderId)
        {
            var order = new WeChatMiniGamePaymentOrderRecord
            { OrderId = orderId, UserId = "demo-user", ProductId = "demo-item", Quantity = 7, Kind = WeChatMiniGamePaymentKind.GameItem };
            return (WeChatMiniGamePaymentDelivery)Activator.CreateInstance(typeof(WeChatMiniGamePaymentDelivery),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { order }, null);
        }

        /// <summary>解析唯一存档快照，以用户可见资产结果验证幂等行为。</summary>
        private static DemoWechatFulfillmentService.DemoAccountState ReadState(MemoryStorage storage) =>
            JsonUtility.FromJson<DemoWechatFulfillmentService.DemoAccountState>(storage.Value);

        /// <summary>定位并写入私有实例字段，名称漂移时明确失败。</summary>
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        /// <summary>读取私有状态用于注册生命周期验证。</summary>
        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        /// <summary>仅模拟本次发货使用的字符串存储和显式 Save 失败。</summary>
        private sealed class MemoryStorage : IPlayerPrefsManager
        {
            public string Value = string.Empty;
            public bool SaveSucceeds = true;
            public int SetCalls;
            public int SaveCalls;
            /// <summary>读取该测试账号的当前快照。</summary>
            public string GetString(string classify, string item, string defaultValue = "") => Value;
            /// <summary>更新内存快照，供保存失败回滚检查。</summary>
            public void SetString(string classify, string item, string value) { Value = value; SetCalls++; }
            /// <summary>按测试控制返回落盘成功或失败。</summary>
            public bool Save(string classify) { SaveCalls++; return SaveSucceeds; }
            /// <summary>测试不使用全量保存。</summary>
            public bool Save() => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public UniTask<bool> Load() => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public bool HasItem(string classify, string item) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public bool RemoveItem(string classify, string item) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void RemoveAll(string classify) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public string[] GetAllItemNames(string classify) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void GetAllItemNames(string classify, List<string> results) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public string[] GetAllClassifyNames() => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public int Count(string classify) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public bool GetBool(string classify, string item, bool defaultValue = default) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public int GetInt(string classify, string item, int defaultValue = default) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public float GetFloat(string classify, string item, float defaultValue = default) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public long GetLong(string classify, string item, long defaultValue = default) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public double GetDouble(string classify, string item, double defaultValue = default) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public byte[] GetBytes(string classify, string item, byte[] defaultValue = null) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public T GetObject<T>(string classify, string item, T defaultValue = default) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetBool(string classify, string item, bool value) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetInt(string classify, string item, int value) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetFloat(string classify, string item, float value) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetLong(string classify, string item, long value) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetDouble(string classify, string item, double value) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetBytes(string classify, string item, byte[] value) => throw new NotSupportedException();
            /// <summary>本测试不使用该持久化操作；误调用立即失败。</summary>
            public void SetObject<T>(string classify, string item, T value) => throw new NotSupportedException();
        }
    }
}
