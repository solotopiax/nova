/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileServiceHub.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   MobileStore 服务容器，统一持有共享外部依赖与内部服务引用
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileStore 服务容器。
    /// 持有共享外部依赖（Context/Config/Table/Store 属性）以及内部服务引用。
    /// 服务在 MobileStore.InitializeAsync 中按序创建并写入对应属性；
    /// 各服务在运行时（非构造期）通过服务容器属性互相访问，天然解决循环依赖。
    /// </summary>
    internal sealed class MobileServiceHub
    {
        /// <summary>
        /// 移动端官方内购商店运行期后台任务取消源；商店释放时统一取消所有经服务容器启动的后台任务。
        /// </summary>
        private readonly CancellationTokenSource m_RuntimeTaskCts = new CancellationTokenSource();

        /// <summary>
        /// 后台任务取消源是否已经释放，避免 Dispose 后重复读取取消令牌抛异常。
        /// </summary>
        private bool m_IsRuntimeTaskCtsDisposed;

        /// <summary>
        /// IAP 商店运行时上下文，提供持久化、网络、事件桥接等跨模块依赖。
        /// </summary>
        internal IIAPStoreContext Context { get; }

        /// <summary>
        /// MobileStore 专属配置（包名、AppId、AppsFlyer 参数等）。
        /// </summary>
        internal MobileStoreConfig Config { get; }

        /// <summary>
        /// IAP 商品配置表接口，所有服务共用。
        /// </summary>
        internal IIAPProductTable Table { get; }

        /// <summary>
        /// 所属 MobileStore，用于事件回调转发与日志。
        /// </summary>
        internal MobileStore Store { get; }

        /// <summary>
        /// 业务网络 Service，封装验单/查单协议发送。
        /// </summary>
        internal MobileIapNetService PayService { get; set; }

        /// <summary>
        /// Unity IAP 初始化服务。
        /// </summary>
        internal MobileInitService InitService { get; set; }

        /// <summary>
        /// 商品对象缓存与票据解析服务。
        /// </summary>
        internal MobileProductService ProductService { get; set; }

        /// <summary>
        /// 订阅到期时间持久化与倒计时服务。
        /// </summary>
        internal MobileSubscriptionService SubscriptionService { get; set; }

        /// <summary>
        /// 订单状态机与验单队列服务。
        /// </summary>
        internal MobileValidationService ValidationService { get; set; }

        /// <summary>
        /// Restore 流程协调服务。
        /// </summary>
        internal MobileRestoreService RestoreService { get; set; }

        /// <summary>
        /// 购买发起与平台回调处理服务。
        /// </summary>
        internal MobilePurchaseService PurchaseService { get; set; }

        /// <summary>
        /// StoreController 调用收口服务，封装所有平台调用入口。
        /// </summary>
        internal MobileExtendedService ExtendedService { get; set; }

        /// <summary>
        /// 平台生命周期事件路由服务，StoreController 所有 On* 回调的统一入口。
        /// </summary>
        internal MobileStoreService StoreService { get; set; }

        /// <summary>
        /// 移动端官方内购商店运行期后台任务取消令牌；取消后新后台任务会被拒绝启动。
        /// </summary>
        internal CancellationToken RuntimeTaskToken => m_IsRuntimeTaskCtsDisposed ? new CancellationToken(true) : m_RuntimeTaskCts.Token;

        /// <summary>
        /// 构造 MobileServiceHub，写入共享外部依赖；服务引用由 MobileStore.InitializeAsync 填充。
        /// </summary>
        /// <param name="context">IAP 商店运行时上下文。</param>
        /// <param name="config">MobileStore 专属配置。</param>
        /// <param name="table">IAP 商品配置表接口。</param>
        /// <param name="store">所属 MobileStore。</param>
        internal MobileServiceHub(IIAPStoreContext context, MobileStoreConfig config, IIAPProductTable table, MobileStore store)
        {
            Context = context;
            Config = config;
            Table = table;
            Store = store;
        }

        /// <summary>
        /// 通过统一入口启动后台任务，自动接入移动端官方内购商店运行期取消令牌并兜底捕获异常。
        /// </summary>
        /// <param name="taskFactory">接收运行期取消令牌并返回后台任务的工厂方法。</param>
        /// <param name="taskName">后台任务名称，用于日志定位。</param>
        internal void RunBackgroundTask(Func<CancellationToken, UniTask> taskFactory, string taskName)
        {
            if (taskFactory == null)
            {
                Log.Warning(LogTag.IAPMobile, $"后台任务启动失败，任务名={taskName}，原因=任务工厂为空。");
                return;
            }

            if (m_IsRuntimeTaskCtsDisposed || m_RuntimeTaskCts.IsCancellationRequested)
            {
                Log.Debug(LogTag.IAPMobile, $"后台任务已跳过，移动端官方内购商店正在释放或已释放，任务名={taskName}。");
                return;
            }

            RunBackgroundTaskAsync(taskFactory, taskName, RuntimeTaskToken).Forget();
        }

        /// <summary>
        /// 执行后台任务并统一处理取消和异常，避免裸 Forget 丢失异常上下文。
        /// </summary>
        /// <param name="taskFactory">后台任务工厂方法。</param>
        /// <param name="taskName">后台任务名称。</param>
        /// <param name="ct">移动端官方内购商店运行期取消令牌。</param>
        private async UniTaskVoid RunBackgroundTaskAsync(Func<CancellationToken, UniTask> taskFactory, string taskName, CancellationToken ct)
        {
            try
            {
                await taskFactory(ct);
            }
            catch (OperationCanceledException)
            {
                Log.Debug(LogTag.IAPMobile, $"后台任务已取消，任务名={taskName}。");
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.IAPMobile, $"后台任务执行异常，任务名={taskName}，详情={e.Message}");
            }
        }

        /// <summary>
        /// 取消移动端官方内购商店运行期后台任务；该方法幂等，可在 Dispose 多个阶段重复调用。
        /// </summary>
        internal void CancelRuntimeTasks()
        {
            if (m_IsRuntimeTaskCtsDisposed || m_RuntimeTaskCts.IsCancellationRequested)
            {
                return;
            }

            m_RuntimeTaskCts.Cancel();
        }

        /// <summary>
        /// 释放移动端官方内购商店后台任务取消源；调用前会先执行取消。
        /// </summary>
        internal void DisposeRuntimeTasks()
        {
            if (m_IsRuntimeTaskCtsDisposed)
            {
                return;
            }

            CancelRuntimeTasks();
            m_RuntimeTaskCts.Dispose();
            m_IsRuntimeTaskCtsDisposed = true;
        }
    }
}
