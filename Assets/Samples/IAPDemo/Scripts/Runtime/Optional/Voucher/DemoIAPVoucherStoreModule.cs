/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPVoucherStoreModule.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   Voucher package 存在时启用的金券支付 Demo 适配模块
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Scripting;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 将 Voucher 钱包、测试发放、报价和支付强类型调用隔离在可选程序集内。
    /// </summary>
    [Preserve]
    internal sealed class DemoIAPVoucherStoreModule : IDemoIAPStoreModule
    {
        /// <summary>
        /// 不依赖 Voucher package 的 Core Bridge。
        /// </summary>
        private DemoIAPBridge m_Bridge;

        /// <summary>
        /// Prefab 中序列化的金券支付 Panel 壳。
        /// </summary>
        private DemoIAPVoucherPanelView m_Panel;

        /// <summary>
        /// 获取金券支付商店类型。
        /// </summary>
        public DemoIAPStoreKind Kind => DemoIAPStoreKind.Voucher;

        /// <summary>
        /// 注入 Core Bridge 与金券 Panel 壳，并绑定钱包业务回调。
        /// </summary>
        /// <param name="context">商店模块初始化上下文。</param>
        public void Initialize(DemoIAPStoreContext context)
        {
            m_Bridge = context.Bridge;
            m_Panel = context.Panel as DemoIAPVoucherPanelView;
            if (m_Panel == null)
            {
                throw new InvalidOperationException("Voucher 模块未取得 DemoIAPVoucherPanelView。");
            }

            m_Panel.Configure(BuildProductTitle, tableId => PayAndRefreshAsync(tableId).Forget(),
                () => RefreshAndRenderAsync().Forget(),
                (grantVoucher, assetId, quantity) => TestGrantAndRenderAsync(grantVoucher, assetId, quantity).Forget(),
                context.Feedback);
        }

        /// <summary>
        /// 创建金券支付演示商品卡。
        /// </summary>
        public void BuildProducts()
        {
            m_Panel?.BuildProducts();
        }

        /// <summary>
        /// 刷新当前账号钱包并渲染完整兑换券与代币余额。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask RefreshAsync()
        {
            await RefreshAndRenderAsync();
        }

        /// <summary>
        /// 设置金券 Panel 的业务按钮交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        public void SetInteractable(bool interactable)
        {
            m_Panel?.SetInteractable(interactable);
        }

        /// <summary>
        /// 将金券 Panel 复位到顶部。
        /// </summary>
        public void ResetScrollPosition()
        {
            m_Panel?.ResetScrollPosition();
        }

        /// <summary>
        /// 清理金券商品卡、余额行和模块引用。
        /// </summary>
        public void ClearRuntimeContent()
        {
            m_Panel?.ClearRuntimeContent();
            m_Panel = null;
            m_Bridge = null;
        }

        /// <summary>
        /// 调用 Voucher 钱包刷新接口并把结果转换为 Core 基础文本模型。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async UniTask RefreshAndRenderAsync()
        {
            VoucherWalletSnapshot wallet = await RefreshWalletAsync();
            RenderWallet(wallet);
        }

        /// <summary>
        /// 调用测试发放接口并渲染服务端返回的新钱包快照。
        /// </summary>
        /// <param name="grantVoucher">是否发放兑换券。</param>
        /// <param name="assetId">资产类型 ID。</param>
        /// <param name="quantity">发放数量。</param>
        /// <returns>异步任务。</returns>
        private async UniTask TestGrantAndRenderAsync(bool grantVoucher, int assetId, int quantity)
        {
            if (m_Bridge == null || !m_Bridge.TryInitialize()
                || !m_Bridge.IAP.TryGetCapability(out IIAPVoucherTestCapable capability))
            {
                m_Bridge?.AppendFeedback("Voucher 测试发放能力不可用。", FeedbackLevel.Warn);
                return;
            }

            try
            {
                VoucherTestGrantRequest request = grantVoucher
                    ? new VoucherTestGrantRequest(new[] { new VoucherGrantLine(assetId, quantity) }, null)
                    : new VoucherTestGrantRequest(null, new[] { new CoinGrantLine(assetId, quantity) });
                VoucherTestGrantResult result = await capability.TestGrantAsync(request, m_Bridge.CancellationToken);
                m_Bridge.AppendFeedback(result.IsSuccess
                        ? "模拟发放成功。"
                        : "模拟发放失败：" + result.ErrorCode + "，" + result.ErrorMessage,
                    result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Error);
                RenderWallet(result.Wallet);
            }
            catch (OperationCanceledException)
            {
                m_Bridge.AppendFeedback("模拟发放已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                m_Bridge.AppendFeedback("模拟发放异常：" + exception.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 创建服务端幂等报价订单并支付，完成后刷新钱包。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>异步任务。</returns>
        private async UniTask PayAndRefreshAsync(long tableId)
        {
            await PayAsync(tableId);
            await RefreshAndRenderAsync();
        }

        /// <summary>
        /// 刷新当前账号 Voucher 钱包。
        /// </summary>
        /// <returns>服务端最新钱包；能力不可用时返回空。</returns>
        private async UniTask<VoucherWalletSnapshot> RefreshWalletAsync()
        {
            if (!TryGetVoucherCapability(out IIAPVoucherCapable capability))
            {
                m_Bridge?.AppendFeedback("Voucher 钱包能力不可用。", FeedbackLevel.Warn);
                return null;
            }

            try
            {
                VoucherRefreshResult result = await capability.RefreshWalletAsync(m_Bridge.CancellationToken);
                m_Bridge.AppendFeedback(result.IsSuccess
                        ? "Voucher 钱包刷新完成。"
                        : "Voucher 钱包刷新失败：" + result.ErrorCode + "，" + result.ErrorMessage,
                    result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Error);
                return result.Wallet;
            }
            catch (OperationCanceledException)
            {
                m_Bridge.AppendFeedback("Voucher 钱包刷新已取消。", FeedbackLevel.Warn);
                return capability.Wallet;
            }
            catch (Exception exception)
            {
                m_Bridge.AppendFeedback("Voucher 钱包刷新异常：" + exception.Message, FeedbackLevel.Error);
                return capability.Wallet;
            }
        }

        /// <summary>
        /// 使用当前钱包创建不可变报价并发起 Voucher 支付。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>异步任务。</returns>
        private async UniTask PayAsync(long tableId)
        {
            if (!TryGetVoucherCapability(out IIAPVoucherCapable capability))
            {
                m_Bridge?.AppendFeedback("Voucher 支付能力不可用。", FeedbackLevel.Warn);
                return;
            }
            if (!TryResolvePriceMills(tableId, out long priceMills))
            {
                m_Bridge.AppendFeedback("商品价格无法转换为 mills：tableId=" + tableId, FeedbackLevel.Warn);
                return;
            }

            VoucherQuote quote = capability.Quote(tableId, priceMills);
            if (quote == null || quote.Status != VoucherQuoteStatus.Ready)
            {
                m_Bridge.AppendFeedback("Voucher 报价不可支付：" + (quote != null ? quote.Status.ToString() : "null"),
                    FeedbackLevel.Warn);
                return;
            }

            m_Bridge.SetPayInteractable(false);
            try
            {
                var request = new IAPVoucherRequest(quote)
                {
                    CustomData = DemoIAPBridge.BuildCustomData(tableId),
                };
                IAPResult result = await m_Bridge.IAP.PayAsync<IAPResult>(request, m_Bridge.CancellationToken);
                m_Bridge.AppendFeedback("Voucher 支付结果：" + DemoIAPBridge.FormatResult(result),
                    result != null && result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Error);
            }
            catch (OperationCanceledException)
            {
                m_Bridge.AppendFeedback("Voucher 支付已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                m_Bridge.AppendFeedback("Voucher 支付异常：" + exception.Message, FeedbackLevel.Error);
            }
            finally
            {
                m_Bridge.SetPayInteractable(!m_Bridge.IsDisposed);
            }
        }

        /// <summary>
        /// 尝试取得 Voucher 钱包能力。
        /// </summary>
        /// <param name="capability">Voucher 钱包能力。</param>
        /// <returns>能力可用时返回 true。</returns>
        private bool TryGetVoucherCapability(out IIAPVoucherCapable capability)
        {
            capability = null;
            return m_Bridge != null && m_Bridge.TryInitialize()
                   && m_Bridge.IAP.TryGetCapability(out capability);
        }

        /// <summary>
        /// 把商品表十进制价格精确转换为 mills。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="priceMills">转换后的 mills。</param>
        /// <returns>价格有效且可转换时返回 true。</returns>
        private bool TryResolvePriceMills(long tableId, out long priceMills)
        {
            priceMills = 0L;
            IAPProductEntry entry = m_Bridge?.FindProductEntry(tableId);
            if (entry == null || !decimal.TryParse(entry.Price, NumberStyles.Number, CultureInfo.InvariantCulture,
                    out decimal price) || price <= 0m)
            {
                return false;
            }

            try
            {
                priceMills = decimal.ToInt64(decimal.Round(price * 1000m, 0, MidpointRounding.AwayFromZero));
                return priceMills > 0L;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// 使用基础商品表价格构建金券商品标题。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>商品卡标题。</returns>
        private string BuildProductTitle(long tableId)
        {
            string group = DemoIAPProductCatalog.GetGroupLabel(tableId);
            return m_Bridge?.BuildProductButtonText(tableId, group)
                   ?? "ID" + tableId + DemoIAPBridge.FormatGroupLabel(group);
        }

        /// <summary>
        /// 将 Voucher 强类型钱包转换为 Core Panel 使用的基础文本行。
        /// </summary>
        /// <param name="wallet">Voucher 钱包快照。</param>
        private void RenderWallet(VoucherWalletSnapshot wallet)
        {
            if (wallet == null)
            {
                m_Panel?.RenderWallet(false, 0L, Array.Empty<string>(), Array.Empty<string>());
                return;
            }

            var voucherRows = new List<string>(wallet.VoucherBalances.Count);
            for (int i = 0; i < wallet.VoucherBalances.Count; i++)
            {
                VoucherWalletBalance balance = wallet.VoucherBalances[i];
                voucherRows.Add("兑换券 #" + balance.VoucherTierId + " · 面值 " + balance.FaceValue
                                + "    × " + balance.Quantity);
            }
            var coinRows = new List<string>(wallet.CoinBalances.Count);
            for (int i = 0; i < wallet.CoinBalances.Count; i++)
            {
                VoucherCoinBalance balance = wallet.CoinBalances[i];
                coinRows.Add("代币 #" + balance.CoinId + " · 面值 " + balance.FaceValue
                             + "    × " + balance.Quantity);
            }
            m_Panel?.RenderWallet(wallet.IsReady, wallet.Version, voucherRows, coinRows);
        }
    }
}
