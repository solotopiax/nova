/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIntegrationUiAssetView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Integration 4.2 — UI + Asset 跨模块联动
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Integration Demo 4.2：Asset 异步取 Sprite 后渲染到 Image，展示 Asset + UI 联动。
    /// API 副标题：Nova.Asset.LoadAsync《Sprite》(loc) -> Nova.UI.OpenUIViewAsync《T》(go)。
    /// 交互触发型：location 输入框 + LoadAndShow 按钮 + 结果 Image 容器。
    /// </summary>
    public sealed class DemoIntegrationUiAssetView : BaseDemoView
    {
        /// <summary>
        /// Asset 地址输入框。
        /// </summary>

        [SerializeField] private TMP_InputField m_LocationInput;

        /// <summary>
        /// 加载并展示按钮。
        /// </summary>

        [SerializeField] private Button m_LoadAndShowButton;

        /// <summary>
        /// 加载结果展示图片组件。
        /// </summary>

        [SerializeField] private Image m_ResultImage;

        /// <summary>
        /// 当前加载任务的取消令牌源，视图关闭时取消。
        /// </summary>
        private CancellationTokenSource m_LoadCts;

        /// <summary>
        /// 当前已加载并持有引用计数的 Sprite 资源句柄，关闭时释放。
        /// </summary>
        private IAssetHandle<Sprite> m_CurrentHandle;

        /// <summary>
        /// 视图初始化钩子，设置标题、API 副标题及默认 location，并绑定按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("UI + Asset");

            if (m_LocationInput != null)
            {
                m_LocationInput.text = "sprite_icon_tree";
            }

            if (m_LoadAndShowButton != null)
            {
                m_LoadAndShowButton.onClick.AddListener(OnLoadAndShowButtonClick);
                SetButtonApiHint(m_LoadAndShowButton, "Nova.Asset.LoadAsync<Sprite>(loc) -> Nova.UI.OpenUIViewAsync<T>(go)");
            }
        }

        /// <summary>
        /// 视图关闭钩子，取消正在进行的加载任务。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_CurrentHandle?.Release();
            m_CurrentHandle = null;

            m_LoadCts?.Cancel();
            m_LoadCts?.Dispose();
            m_LoadCts = null;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// LoadAndShow 按钮点击回调，发起 Asset 异步加载。
        /// </summary>
        private void OnLoadAndShowButtonClick()
        {
            LoadAndShowAsync().Forget();
        }

        /// <summary>
        /// 异步加载 Sprite 并展示到 m_ResultImage，加载成功后打开 Toast 展示 UI handle。
        /// </summary>
        private async UniTaskVoid LoadAndShowAsync()
        {
            string location = m_LocationInput != null ? m_LocationInput.text : "sprite_icon_tree";

            if (string.IsNullOrEmpty(location))
            {
                AppendFeedback("Nova.Asset.LoadAsync<Sprite>() -> location is empty", FeedbackLevel.Error);
                return;
            }

            m_LoadCts?.Cancel();
            m_LoadCts?.Dispose();
            m_LoadCts = new CancellationTokenSource();

            m_CurrentHandle?.Release();
            m_CurrentHandle = null;

            AppendFeedback(string.Format("Nova.Asset.LoadAsync<Sprite>(\"{0}\") -> ...", location), FeedbackLevel.Info);

            IAssetHandle<Sprite> handle = await Nova.Asset.LoadAsync<Sprite>(location, m_LoadCts.Token);

            if (this == null || m_LoadCts == null || m_LoadCts.IsCancellationRequested)
            {
                handle?.Release();
                return;
            }

            if (handle.Asset == null)
            {
                handle.Release();
                AppendFeedback(string.Format("Nova.Asset.LoadAsync<Sprite>(\"{0}\") -> null (asset not found)", location), FeedbackLevel.Error);
                return;
            }

            m_CurrentHandle = handle;
            Sprite sprite = handle.Asset;

            if (m_ResultImage != null)
            {
                m_ResultImage.sprite = sprite;
                m_ResultImage.SetNativeSize();
            }

            AppendFeedback(string.Format("Nova.Asset.LoadAsync<Sprite>(\"{0}\") -> loaded {1}x{2}", location, sprite.texture.width, sprite.texture.height), FeedbackLevel.Success);

            int serialID = Nova.UI.OpenUIViewAsync<DemoToastView>(string.Format("Asset loaded: {0}", location));
            AppendFeedback(string.Format("Nova.UI.OpenUIViewAsync<DemoToastView>() -> handle={0}", serialID), FeedbackLevel.Success);
        }
    }
}
