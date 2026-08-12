/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.Methods.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   IAP Demo Core 与可选商店模块共用的辅助方法
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;
using UnityEngine;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// IAP Demo Core 与可选商店模块共用方法。
    /// </summary>
    internal sealed partial class DemoIAPBridge
    {
        /// <summary>
        /// 查询指定商品表行对应的基础商品配置。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>商品配置；插件不可用时返回空。</returns>
        internal IAPProductEntry FindProductEntry(long tableId)
        {
            return TryInitialize() ? m_IAP.ProductTable.FindByTableId(tableId) : null;
        }

        /// <summary>
        /// 向反馈区追加一行文本；桥接层释放后静默跳过。
        /// </summary>
        /// <param name="line">反馈文本。</param>
        /// <param name="level">反馈级别。</param>
        internal void AppendFeedback(string line, FeedbackLevel level)
        {
            if (!m_Disposed)
            {
                m_Feedback?.Invoke(line, level);
            }
        }

        /// <summary>
        /// 同步全部已发现商店模块的业务按钮交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        internal void SetPayInteractable(bool interactable)
        {
            if (!m_Disposed)
            {
                m_PayInteractableChanged?.Invoke(interactable);
            }
        }

        /// <summary>
        /// 构建所有商店共用的演示 JSON 透传数据。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>JSON 透传数据。</returns>
        internal static string BuildCustomData(long tableId)
        {
            return JsonUtility.ToJson(new PayPayload { TableId = tableId, Scene = c_SceneName });
        }

        /// <summary>
        /// 将商品分组格式化为带方括号的后缀。
        /// </summary>
        /// <param name="groupLabel">商品分组。</param>
        /// <returns>格式化分组后缀。</returns>
        internal static string FormatGroupLabel(string groupLabel)
        {
            return string.IsNullOrEmpty(groupLabel) ? string.Empty : "  [" + groupLabel + "]";
        }

        /// <summary>
        /// 将支付结果格式化为可读诊断文本。
        /// </summary>
        /// <param name="result">支付结果。</param>
        /// <returns>诊断文本；结果为空时返回 null 字样。</returns>
        internal static string FormatResult(IAPResult result)
        {
            if (result == null)
            {
                return "null";
            }

            return "TableId=" + result.TableId
                   + ", IsSuccess=" + result.IsSuccess
                   + ", OrderId=" + result.OrderId
                   + ", ErrorCode=" + result.ErrorCode
                   + ", ErrorSource=" + result.ErrorSource
                   + ", ErrorDesc=" + result.ErrorDesc
                   + ", IsRecoveredOrder=" + result.IsRecoveredOrder
                   + ", CanDeliver=" + result.CanDeliver
                   + ", ReceiptParam=" + result.ReceiptParam;
        }
    }
}
