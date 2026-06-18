/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView 演示 View — 私有方法
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// DemoIAPView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoIAPView
    {
        /// <summary>
        /// 登录按钮点击回调：置灰登录按钮并展开 4 个支付按钮。
        /// 重复点击直接忽略，避免多次克隆支付按钮。
        /// </summary>
        private void OnLoginClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 登录后展开支付列表，并把登录账号同步给 IAP 桥接层。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid LoginAsync()
        {
            if (m_LoggedIn || m_LoginInProgress)
            {
                return;
            }

            m_LoginInProgress = true;
            if (m_SampleButton != null)
            {
                m_SampleButton.interactable = false;
            }

            string openId = "test_openid_iap";
            bool forceNewAccount = false;
            try
            {
                NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, forceNewAccount);
                if (resp.IsSuccess)
                {
                    m_LoggedIn = true;
                    AppendFeedback("登录成功，展开支付列表。", FeedbackLevel.Success);

                    // 置灰登录按钮：禁用交互并把底图染灰。
                    if (m_SampleButton != null)
                    {
                        Image loginImage = m_SampleButton.GetComponent<Image>();
                        if (loginImage != null)
                        {
                            loginImage.color = new Color32(0xBD, 0xBD, 0xBD, 0xFF);
                        }
                    }

                    if (m_IapBridge != null)
                    {
                        m_IapBridge.CheckLocalOrdersAsync().Forget();
                    }

                    BuildPayButtons();
                }
                else
                {
                    AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", {forceNewAccount}) → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
                }
            }
            finally
            {
                m_LoginInProgress = false;
                if (!m_LoggedIn && m_SampleButton != null)
                {
                    m_SampleButton.interactable = true;
                }
            }
        }

        /// <summary>
        /// 构建 4 个支付按钮（2 普通 + 2 订阅）。
        /// 逐个 tableId 克隆示例按钮、设置 "ID+表行id+价格" 文本，并绑定支付点击回调。
        /// </summary>
        private void BuildPayButtons()
        {
            m_PayButtons.RemoveAll(payButton => payButton == null);
            if (m_PayButtons.Count > 0)
            {
                return;
            }

            BuildPayButtonGroup(s_NormalProductIds, "普通");
            BuildPayButtonGroup(s_SubscriptionProductIds, "订阅");
            BuildRestorePurchasesButton();

            m_IapBridge?.RefreshMobileProductInfoAsync(s_NormalProductIds).Forget();
            m_IapBridge?.RefreshMobileProductInfoAsync(s_SubscriptionProductIds).Forget();
        }

        /// <summary>
        /// 克隆示例按钮生成恢复订阅按钮。
        /// </summary>
        private void BuildRestorePurchasesButton()
        {
            if (m_SampleButton == null || InteractionRoot == null)
            {
                return;
            }

            Button restoreButton = Instantiate(m_SampleButton, InteractionRoot);
            restoreButton.name = c_RestorePurchasesButtonName;
            restoreButton.gameObject.SetActive(true);
            restoreButton.interactable = true;

            Image restoreImage = restoreButton.GetComponent<Image>();
            if (restoreImage != null)
            {
                restoreImage.color = Color.white;
            }

            SetButtonLabel(restoreButton, "恢复订阅");
            SetButtonApiHint(restoreButton, "RestorePurchasesAsync");

            restoreButton.onClick.RemoveAllListeners();
            restoreButton.onClick.AddListener(OnRestorePurchasesClick);

            m_PayButtons.Add(restoreButton);
        }

        /// <summary>
        /// 按一组 tableId 批量克隆支付按钮。
        /// </summary>
        /// <param name="tableIds">商品表行 id 数组。</param>
        /// <param name="groupLabel">分组名（普通 / 订阅），用于反馈与日志区分。</param>
        private void BuildPayButtonGroup(long[] tableIds, string groupLabel)
        {
            if (tableIds == null || m_SampleButton == null || InteractionRoot == null)
            {
                return;
            }

            foreach (long tableId in tableIds)
            {
                Button payButton = Instantiate(m_SampleButton, InteractionRoot);
                payButton.name = "PayButton_" + tableId;
                payButton.gameObject.SetActive(true);
                payButton.interactable = true;

                Image payImage = payButton.GetComponent<Image>();
                if (payImage != null)
                {
                    payImage.color = Color.white;
                }

                SetButtonLabel(payButton, BuildPayButtonText(tableId, groupLabel));
                SetButtonApiHint(payButton, m_IapBridge != null ? m_IapBridge.GetProductSku(tableId) : string.Empty);

                long capturedId = tableId;
                payButton.onClick.RemoveAllListeners();
                payButton.onClick.AddListener(() => OnPayClick(capturedId));

                m_PayButtons.Add(payButton);
            }
        }

        /// <summary>
        /// 拼装支付按钮文本："ID" + 表行 id，并附带从桥接层查询到的价格。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <param name="groupLabel">分组名（普通 / 订阅）。</param>
        /// <returns>按钮显示文本。</returns>
        private string BuildPayButtonText(long tableId, string groupLabel)
        {
            if (m_IapBridge != null)
            {
                return m_IapBridge.BuildProductButtonText(tableId, groupLabel);
            }

            return "ID" + tableId + "  [" + groupLabel + "]  价格未知";
        }

        /// <summary>
        /// 支付按钮点击回调。
        /// </summary>
        /// <param name="tableId">被点击商品的表行 id。</param>
        private void OnPayClick(long tableId)
        {
            if (m_IapBridge == null)
            {
                AppendFeedback("IAP 桥接层不可用，无法发起支付。", FeedbackLevel.Error);
                return;
            }

            m_IapBridge.PayMobileAsync(tableId).Forget();
        }

        /// <summary>
        /// 恢复订阅按钮点击回调。
        /// </summary>
        private void OnRestorePurchasesClick()
        {
            if (m_IapBridge == null)
            {
                AppendFeedback("IAP 桥接层不可用，无法恢复订阅。", FeedbackLevel.Error);
                return;
            }

            AppendFeedback("正在恢复订阅。", FeedbackLevel.Info);
            m_IapBridge.RestorePurchasesAsync().Forget();
        }

        /// <summary>
        /// 更新指定支付按钮的显示文本。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <param name="text">显示文本。</param>
        private void UpdatePayButtonText(long tableId, string text)
        {
            string buttonName = "PayButton_" + tableId;
            for (int i = 0; i < m_PayButtons.Count; i++)
            {
                Button payButton = m_PayButtons[i];
                if (payButton != null && payButton.name == buttonName)
                {
                    SetButtonLabel(payButton, text);
                    return;
                }
            }
        }

        /// <summary>
        /// 设置全部支付按钮的交互状态。
        /// </summary>
        /// <param name="interactable">是否可交互。</param>
        private void SetPayButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < m_PayButtons.Count; i++)
            {
                Button payButton = m_PayButtons[i];
                if (payButton != null)
                {
                    payButton.interactable = interactable;
                }
            }
        }

        /// <summary>
        /// 清理已克隆的支付按钮。
        /// </summary>
        private void ClearPayButtons()
        {
            for (int i = 0; i < m_PayButtons.Count; i++)
            {
                Button payButton = m_PayButtons[i];
                if (payButton != null)
                {
                    Destroy(payButton.gameObject);
                }
            }

            m_PayButtons.Clear();
        }

        /// <summary>
        /// 设置按钮内 TMP 文本（取按钮下首个 TMP_Text 子组件）。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="text">显示文本。</param>
        private void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
