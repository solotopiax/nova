/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatFulfillmentService.cs
 * author:    Codex
 * created:   2026/10/08
 * descrip:   Demo 专属幂等模拟资产与发货后统计
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.WeChatMiniGame.Runtime;
using UnityEngine;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    /// <summary>由微信插件注册句柄托管的 Sample 发货器，不保存页面引用。</summary>
    public sealed class DemoWechatFulfillmentService : IWeChatMiniGameFulfillmentHandler, IDisposable
    {
        // 独立 Demo 分类避免污染项目正式资产。UID 作为存储项，去重和资产写在同一 JSON 快照。
        private const string c_Classify = "nova_demo_wechat_fulfillment_v1";
        private static DemoWechatFulfillmentService s_Current;
        private readonly WeChatMiniGamePlugin m_Plugin;
        private readonly IPlayerPrefsManager m_Storage;
        private bool m_Disposed;
        private IDisposable m_Registration;
        public event Action<string> Feedback;

        /// <summary>模拟资产、订单去重与待上报队列是同一业务存档。</summary>
        [Serializable]
        public sealed class DemoAccountState
        {
            public int SchemaVersion = 1;
            public List<DemoAsset> Assets = new();
            public List<WeChatMiniGamePaymentOrderRecord> DeliveredOrders = new();
            public List<string> ReportedOrders = new();
            public List<DemoPaymentEvent> PendingEvents = new();
        }

        /// <summary>持久化阶段事件，只保留允许展示的业务键，不保存签名、用户身份或原始透传。</summary>
        [Serializable]
        public sealed class DemoPaymentEvent
        {
            public string EventId, OrderId, ProductId, Kind, Source, Stage, Result, AttemptId, RecoveryReason;
            public int VerificationAttempts;
            public long Timestamp;
        }

        /// <summary>只供演示使用的商品数量余额，不对应游戏正式资产。</summary>
        [Serializable]
        public sealed class DemoAsset
        {
            public string ProductId;
            public int Quantity;
        }

        /// <summary>绑定微信插件与 Nova Persist，不能在 Persist 尚未初始化前创建。</summary>
        private DemoWechatFulfillmentService(WeChatMiniGamePlugin plugin)
        {
            m_Plugin = plugin;
            m_Storage = Nova.Persist.PlayerPrefs ?? throw new InvalidOperationException("Demo Persist 尚未初始化。");
            m_Plugin.PaymentProgress += OnPaymentProgress;
            m_Plugin.GravityInitialized += OnGravityInitialized;
        }

        /// <summary>每个运行中的微信插件仅绑定一次长期发货器，插件销毁时统一释放。</summary>
        public static DemoWechatFulfillmentService Attach(WeChatMiniGamePlugin plugin)
        {
            if (s_Current != null && !s_Current.m_Disposed && ReferenceEquals(s_Current.m_Plugin, plugin)) return s_Current;
            s_Current?.m_Registration?.Dispose();
            var service = new DemoWechatFulfillmentService(plugin);
            try { service.m_Registration = plugin.RegisterPaymentFulfillmentHandler(service); }
            catch { service.Dispose(); throw; }
            s_Current = service;
            return service;
        }

        /// <summary>以订单号幂等更新模拟资产。取消或落盘失败时不确认发货，订单保留以便重试。</summary>
        public UniTask<bool> DeliverAsync(WeChatMiniGamePaymentDelivery delivery, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (m_Disposed) throw new ObjectDisposedException(nameof(DemoWechatFulfillmentService));
            WeChatMiniGamePaymentOrderRecord order = delivery?.Order ?? throw new ArgumentNullException(nameof(delivery));
            if (string.IsNullOrWhiteSpace(order.UserId) || string.IsNullOrWhiteSpace(order.OrderId) || order.Quantity <= 0)
                throw new ArgumentException("演示发货需要有效 UID、订单号及数量。");
            string previous = m_Storage.GetString(c_Classify, order.UserId, string.Empty);
            DemoAccountState state = string.IsNullOrWhiteSpace(previous)
                ? new DemoAccountState() : JsonUtility.FromJson<DemoAccountState>(previous);
            if (state == null || state.SchemaVersion != 1 || state.Assets == null || state.DeliveredOrders == null || state.ReportedOrders == null)
                throw new InvalidOperationException("演示业务存档无效，拒绝覆盖或重复发货。");
            bool duplicate = state.DeliveredOrders.Any(item => item.OrderId == order.OrderId);
            if (!duplicate)
            {
                DemoAsset asset = state.Assets.FirstOrDefault(item => item.ProductId == order.ProductId);
                if (asset == null) { asset = new DemoAsset { ProductId = order.ProductId }; state.Assets.Add(asset); }
                asset.Quantity = checked(asset.Quantity + order.Quantity);
                state.DeliveredOrders.Add(order.Clone());
                ct.ThrowIfCancellationRequested();
                SaveState(order.UserId, state, previous);
            }
            Emit($"【Demo 模拟资产】订单 {MaskOrder(order.OrderId)}，{(duplicate ? "重复请求，未再次增加" : "幂等发货已落盘")}，来源 {ReadPurchaseSource(order)}。不修改正式资产。");
            // 发货和统计故障分离：统计失败不能撤销已落盘资产，也不能导致再次发货。
            TryReportDelivered(order.UserId, state);
            return UniTask.FromResult(true);
        }

        /// <summary>引力异步初始化完成后主动恢复当前账号待提交统计，不依赖页面或下一次购买。</summary>
        private void OnGravityInitialized() => RetryReports(NetService.UID);

        /// <summary>登录、引力就绪或手动补单后重试该 UID 已发货但尚未提交引力的事件。</summary>
        public void RetryReports(string userId)
        {
            if (m_Disposed || string.IsNullOrWhiteSpace(userId) || !string.Equals(userId, NetService.UID, StringComparison.Ordinal)) return;
            try
            {
                string data = m_Storage.GetString(c_Classify, userId, string.Empty);
                if (string.IsNullOrWhiteSpace(data)) return;
                DemoAccountState state = JsonUtility.FromJson<DemoAccountState>(data);
                if (!IsValidState(state, userId)) { Emit("演示统计存档无效，跳过上报且不覆盖存档。"); return; }
                TryReportDelivered(userId, state);
                TryReportProgress(userId, state);
            }
            catch (Exception) { Emit("演示统计重试未完成，不影响已成功的账号登录或发货。"); }
        }

        /// <summary>仅允许完整且全部订单属于目标 UID 的当前版本存档进入统计队列。</summary>
        private static bool IsValidState(DemoAccountState state, string userId) =>
            state != null && state.SchemaVersion == 1 && state.Assets != null && state.DeliveredOrders != null &&
            state.ReportedOrders != null && state.DeliveredOrders.All(order => order != null &&
                !string.IsNullOrWhiteSpace(order.OrderId) && string.Equals(order.UserId, userId, StringComparison.Ordinal));

        /// <summary>独立来源优先，旧订单仍识别 Demo 历史来源，其余回退 unknown。</summary>
        private static string ReadPurchaseSource(WeChatMiniGamePaymentOrderRecord order) =>
            string.IsNullOrEmpty(order.PurchaseSource) ? ReadDemoSource(order.Payload) : order.PurchaseSource;

        /// <summary>仅提取 Demo 固定来源键，拒绝把任意原始业务透传放入日志和统计。</summary>
        private static string ReadDemoSource(string payload)
        {
            return payload != null && payload.Split(';').Any(part => part == "source=nova-demo") ? "nova-demo" : "unknown";
        }

        /// <summary>普通日志缩略订单号，不输出完整用户身份或支付透传。</summary>
        private static string MaskOrder(string orderId) =>
            string.IsNullOrEmpty(orderId) || orderId.Length < 7 ? "***" : orderId.Substring(0, 3) + "***" + orderId.Substring(orderId.Length - 3);

        /// <summary>提交发货阶段业务事件并持久标记；SDK 无服务端确认，因此不能保证网络端恰好一次。</summary>
        private void TryReportDelivered(string userId, DemoAccountState state)
        {
            if (!IsValidState(state, userId) || !string.Equals(userId, NetService.UID, StringComparison.Ordinal) || !m_Plugin.IsGravityInitialized) return;
            foreach (WeChatMiniGamePaymentOrderRecord order in state.DeliveredOrders)
            {
                if (state.ReportedOrders.Contains(order.OrderId)) continue;
                try
                {
                    m_Plugin.TrackGravityEvent("nova_demo_wx_fulfilled", new Dictionary<string, object>
                    {
                        ["order_id"] = order.OrderId, ["product_id"] = order.ProductId,
                        ["payment_kind"] = order.Kind.ToString(), ["purchase_source"] = ReadPurchaseSource(order),
                        ["stage"] = "fulfilled", ["verification_attempts"] = order.VerificationAttempts,
                        ["attempt_id"] = order.AttemptId ?? string.Empty, ["recovery_reason"] = order.RecoveryReason ?? "none"
                    });
                    string previous = m_Storage.GetString(c_Classify, userId, string.Empty);
                    state.ReportedOrders.Add(order.OrderId);
                    SaveState(userId, state, previous);
                    Emit($"已提交订单 {MaskOrder(order.OrderId)} 发货阶段引力事件（包含购买来源与验单次数）。当前协议无可信实付金额，不伪造收入。" );
                }
                catch (Exception)
                {
                    state.ReportedOrders.Remove(order.OrderId);
                    Emit("发货后引力上报未完成，将在后续补单/登录重试。");
                    break;
                }
            }
        }

        /// <summary>长期服务接收所有购买/补单阶段，页面关闭也继续保存事件；统计失败不反向影响订单。</summary>
        private void OnPaymentProgress(WeChatMiniGamePaymentProgress progress)
        {
            string uid = NetService.UID;
            if (m_Disposed || string.IsNullOrEmpty(uid)) return;
            try
            {
                string previous = m_Storage.GetString(c_Classify, uid, string.Empty);
                var state = string.IsNullOrEmpty(previous) ? new DemoAccountState() : JsonUtility.FromJson<DemoAccountState>(previous);
                if (!IsValidState(state, uid)) return;
                state.PendingEvents ??= new List<DemoPaymentEvent>();
                state.PendingEvents.Add(new DemoPaymentEvent {
                    EventId=progress.EventId, OrderId=progress.OrderId, ProductId=progress.ProductId,
                    Kind=progress.Kind.ToString(), Source=progress.PurchaseSource, Stage=progress.Stage,
                    Result=progress.Result, AttemptId=progress.AttemptId, RecoveryReason=progress.RecoveryReason,
                    VerificationAttempts=progress.VerificationAttempts, Timestamp=progress.TimestampUnixMilliseconds });
                SaveState(uid, state, previous);
                Emit($"支付阶段 {progress.Stage}，来源 {progress.PurchaseSource}，恢复 {progress.RecoveryReason}，结果 {progress.Result}。");
                TryReportProgress(uid, state);
            }
            catch (Exception) { Emit("演示支付阶段事件保存或上报失败，不影响订单与发货。"); }
        }

        /// <summary>引力可用后重试持久化阶段事件，复用 event_id；SDK无接收确认，网络端去重由后端负责。</summary>
        private void TryReportProgress(string uid, DemoAccountState state)
        {
            if (!m_Plugin.IsGravityInitialized || uid != NetService.UID || state.PendingEvents == null) return;
            while (state.PendingEvents.Count > 0)
            {
                var item = state.PendingEvents[0];
                try
                {
                    m_Plugin.TrackGravityEvent("nova_demo_wx_payment_stage", new Dictionary<string, object> {
                        ["event_id"]=item.EventId, ["order_id"]=item.OrderId, ["product_id"]=item.ProductId,
                        ["payment_kind"]=item.Kind, ["purchase_source"]=item.Source, ["stage"]=item.Stage,
                        ["result"]=item.Result, ["attempt_id"]=item.AttemptId, ["recovery_reason"]=item.RecoveryReason,
                        ["verification_attempts"]=item.VerificationAttempts, ["occurred_at_ms"]=item.Timestamp });
                    string previous = m_Storage.GetString(c_Classify, uid, string.Empty);
                    state.PendingEvents.RemoveAt(0);
                    try { SaveState(uid, state, previous); }
                    catch { state.PendingEvents.Insert(0, item); throw; }
                }
                catch (Exception) { break; }
            }
        }

        /// <summary>整份业务存档一次保存，失败回滚内存快照并拒绝确认发货。</summary>
        private void SaveState(string userId, DemoAccountState state, string previous)
        {
            try
            {
                m_Storage.SetString(c_Classify, userId, JsonUtility.ToJson(state));
                if (!m_Storage.Save(c_Classify)) throw new InvalidOperationException("演示业务存档未能持久化。");
            }
            catch
            {
                m_Storage.SetString(c_Classify, userId, previous);
                throw;
            }
        }

        /// <summary>输出独立日志并通知当前订阅页面，页面关闭后不访问其 Unity 对象。</summary>
        private void Emit(string message)
        {
            Debug.Log(message);
            try { Feedback?.Invoke(message); }
            catch (Exception exception) { Debug.LogWarning("Demo 页面反馈失败，不影响幂等发货：" + exception.Message); }
        }

        /// <summary>插件释放注册句柄时清除反馈订阅和实例引用，持久业务存档保留供恢复。</summary>
        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            m_Plugin.PaymentProgress -= OnPaymentProgress;
            m_Plugin.GravityInitialized -= OnGravityInitialized;
            Feedback = null;
            if (ReferenceEquals(s_Current, this)) s_Current = null;
        }
    }
}
