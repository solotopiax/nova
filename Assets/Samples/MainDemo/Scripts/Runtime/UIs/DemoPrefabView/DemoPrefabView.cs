/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoPrefabView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.3 — Prefab 异步实例化与销毁演示 View（交互型）
 *            职责：演示 Nova.Prefab.InstantiateAsync 和 Destroy，
 *            在实例化容器中创建 DemoPrefabBlock 实例并计数，支持单路销毁。
 *            DemoPrefabBlock 是 UI Prefab（Image + Spinner），可挂在 Canvas 容器下直接显示。
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.3 Prefab 演示 View（交互型）。
    /// 演示 Nova.Prefab.InstantiateAsync 异步实例化预制体，以及 Object.Destroy 单路销毁，
    /// 实时展示当前实例计数。
    /// </summary>
    public sealed class DemoPrefabView : BaseDemoView
    {
        /// <summary>
        /// 实例化按钮，触发 Nova.Prefab.InstantiateAsync。
        /// </summary>

        [SerializeField] private Button m_InstantiateButton;

        /// <summary>
        /// 销毁按钮，销毁最近一个实例。
        /// </summary>

        [SerializeField] private Button m_DestroyButton;

        /// <summary>
        /// 当前实例计数文本展示组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CountText;

        /// <summary>
        /// 实例化容器 RectTransform，所有实例挂载到此节点下。
        /// </summary>

        [SerializeField] private RectTransform m_InstanceContainer;

        /// <summary>
        /// 演示预制体的 Asset 地址，Inspector 可调。
        /// </summary>

        [SerializeField] private string m_PrefabLocation = "DemoPrefabBlock";

        /// <summary>
        /// 当前已创建的所有预制体实例列表，LIFO 顺序管理销毁。
        /// </summary>
        private readonly List<GameObject> m_Instances = new List<GameObject>();

        /// <summary>
        /// 当前实例化任务的取消令牌源。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Prefab");

            if (m_InstantiateButton != null)
            {
                m_InstantiateButton.onClick.AddListener(OnInstantiateButtonClick);
                SetButtonApiHint(m_InstantiateButton, "Nova.Prefab.InstantiateAsync(location)");
            }

            if (m_DestroyButton != null)
            {
                m_DestroyButton.onClick.AddListener(OnDestroyButtonClick);
                SetButtonApiHint(m_DestroyButton, "Nova.Prefab.Destroy(go)");
            }
        }

        /// <summary>
        /// 视图打开钩子，重置取消令牌源并刷新计数文本。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();

            RefreshCountText();
        }

        /// <summary>
        /// 视图关闭钩子，取消异步操作并销毁所有实例。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;

            for (int i = 0; i < m_Instances.Count; i++)
            {
                if (m_Instances[i] != null)
                {
                    Destroy(m_Instances[i]);
                }
            }

            m_Instances.Clear();
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 实例化按钮点击回调，启动异步实例化流程。
        /// </summary>
        private void OnInstantiateButtonClick()
        {
            InstantiateAsync().Forget();
        }

        /// <summary>
        /// 销毁按钮点击回调，销毁最近一个实例并刷新计数。
        /// </summary>
        private void OnDestroyButtonClick()
        {
            if (m_Instances.Count == 0)
            {
                AppendFeedback("Destroy -> 无可销毁的实例", FeedbackLevel.Warn);
                return;
            }

            int lastIndex = m_Instances.Count - 1;
            GameObject last = m_Instances[lastIndex];
            m_Instances.RemoveAt(lastIndex);

            if (last != null)
            {
                Destroy(last);
            }

            RefreshCountText();
            AppendFeedback("Nova.Prefab.Destroy(go) -> " + m_Instances.Count + " instance(s) remaining", FeedbackLevel.Success);
        }

        /// <summary>
        /// 异步实例化 DemoPrefabBlock 并将其添加到容器，成功后刷新计数与反馈。
        /// </summary>
        private async UniTaskVoid InstantiateAsync()
        {
            if (Nova.Prefab == null)
            {
                AppendFeedback("Nova.Prefab.InstantiateAsync -> PrefabComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            AppendFeedback("Nova.Prefab.InstantiateAsync(\"" + m_PrefabLocation + "\") -> 实例化中...");

            GameObject instance = null;
            bool cancelled = false;

            try
            {
                Transform parent = m_InstanceContainer != null ? m_InstanceContainer : null;
                instance = await Nova.Prefab.InstantiateAsync(m_PrefabLocation, parent, m_Cts.Token);
            }
            catch (System.OperationCanceledException)
            {
                cancelled = true;
            }

            if (cancelled || m_Cts == null || m_Cts.IsCancellationRequested)
            {
                return;
            }

            if (instance == null)
            {
                AppendFeedback("Nova.Prefab.InstantiateAsync(\"" + m_PrefabLocation + "\") -> 返回 null，请确认资源存在", FeedbackLevel.Error);
                return;
            }

            m_Instances.Add(instance);
            RefreshCountText();
            AppendFeedback("Nova.Prefab.InstantiateAsync(\"" + m_PrefabLocation + "\") -> " + m_Instances.Count + " instance(s)", FeedbackLevel.Success);
        }

        /// <summary>
        /// 刷新当前实例计数文本。
        /// </summary>
        private void RefreshCountText()
        {
            if (m_CountText != null)
            {
                m_CountText.text = "实例数：" + m_Instances.Count;
            }
        }
    }
}
