/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView IAP 调度桥接层 - 私有方法
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    internal sealed partial class DemoIAPBridge
    {
        /// <summary>
        /// 查询指定商品表行 id 对应的商品配置条目。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <returns>商品配置条目；插件不可用时返回 null。</returns>
        private IAPProductEntry FindProductEntry(long tableId)
        {
            return TryInitialize() ? m_IAP.ProductTable.FindByTableId(tableId) : null;
        }

        /// <summary>
        /// 刷新指定商品列表的支付按钮文本，逐个通过回调通知 View 更新。
        /// </summary>
        /// <param name="tableIds">待刷新商品表行 id 列表。</param>
        private void RefreshProductTexts(IReadOnlyList<long> tableIds)
        {
            if (m_Disposed)
            {
                return;
            }

            if (m_ProductTextChanged == null)
            {
                return;
            }

            for (int i = 0; i < tableIds.Count; i++)
            {
                long tableId = tableIds[i];
                IAPProductEntry entry = FindProductEntry(tableId);
                m_ProductTextChanged(tableId, BuildProductButtonText(tableId, GetGroupLabel(entry)));
            }
        }

        /// <summary>
        /// 向反馈区追加一行文本；已释放时静默跳过。
        /// </summary>
        /// <param name="line">反馈文本内容。</param>
        /// <param name="level">反馈级别。</param>
        private void AppendFeedback(string line, FeedbackLevel level)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Feedback?.Invoke(line, level);
        }

        /// <summary>
        /// 设置支付按钮可交互状态；已释放时静默跳过。
        /// </summary>
        /// <param name="interactable">是否可交互。</param>
        private void SetPayInteractable(bool interactable)
        {
            if (m_Disposed)
            {
                return;
            }

            m_PayInteractableChanged?.Invoke(interactable);
        }

        /// <summary>
        /// 获取商品分组文案（订阅 / 普通）。
        /// </summary>
        /// <param name="entry">商品配置条目。</param>
        /// <returns>分组文案；条目为空时返回空串。</returns>
        private static string GetGroupLabel(IAPProductEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return entry.ProductType == IAPProductType.Subscription ? "订阅" : "普通";
        }

        /// <summary>
        /// 将分组文案格式化为带方括号的后缀；为空时返回空串。
        /// </summary>
        /// <param name="groupLabel">分组文案。</param>
        /// <returns>格式化后的后缀字符串。</returns>
        private static string FormatGroupLabel(string groupLabel)
        {
            return string.IsNullOrEmpty(groupLabel) ? string.Empty : "  [" + groupLabel + "]";
        }

        /// <summary>
        /// 将支付结果格式化为可读的诊断字符串。
        /// </summary>
        /// <param name="result">支付结果。</param>
        /// <returns>诊断字符串；结果为空时返回 "null"。</returns>
        private static string FormatResult(IAPResult result)
        {
            if (result == null)
            {
                return "null";
            }

            return "TableId=" + result.TableId
                   + ", IsSuccess=" + result.IsSuccess
                   + ", OrderId=" + result.OrderId
                   + ", ErrorCode=" + result.ErrorCode
                   + ", FailReason=" + result.FailReason
                   + ", IsRecoveredOrder=" + result.IsRecoveredOrder
                   + ", CanDeliver=" + result.CanDeliver;
        }
    }
}
