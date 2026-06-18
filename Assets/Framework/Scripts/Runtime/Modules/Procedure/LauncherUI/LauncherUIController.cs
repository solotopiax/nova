/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherUIController.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   启动阶段 UI 控制器
 *            统一管理 Splash / Progress / Dialog 三种面板的
 *            生命周期，外部只传入枚举和数值，
 *            所有文本由各面板自身通过 LauncherLocalization 驱动。
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段 UI 控制器。
    /// 面板生命周期完全内聚，Procedure 只传数据；文本由各面板通过 LauncherLocalization 驱动（只走 Resources 通道，与 LocalizationManager 完全解耦）。
    /// </summary>
    public static class LauncherUIController
    {
        /// <summary>
        /// 闪屏面板 Canvas sortingOrder。
        /// 约束层次：Splash（0） < Progress（10） < Dialog（20），框架强制赋值，Prefab 上的 Canvas 配置会被覆盖。
        /// </summary>
        private const int c_SplashSortingOrder = 0;

        /// <summary>
        /// 进度面板 Canvas sortingOrder。
        /// 约束层次：Splash（0） < Progress（10） < Dialog（20），框架强制赋值，Prefab 上的 Canvas 配置会被覆盖。
        /// </summary>
        private const int c_ProgressSortingOrder = 10;

        /// <summary>
        /// 弹窗面板 Canvas sortingOrder。
        /// 约束层次：Splash（0） < Progress（10） < Dialog（20），框架强制赋值，Prefab 上的 Canvas 配置会被覆盖。
        /// </summary>
        private const int c_DialogSortingOrder = 20;

        /// <summary>
        /// 当前启动阶段配置。
        /// </summary>
        private static LauncherSettings s_Settings;

        /// <summary>
        /// 闪屏面板实例。
        /// </summary>
        private static LauncherSplashPanel s_SplashPanel;

        /// <summary>
        /// 进度面板实例。
        /// </summary>
        private static LauncherProgressPanel s_ProgressPanel;

        /// <summary>
        /// 弹窗面板实例。
        /// </summary>
        private static LauncherDialogPanel s_DialogPanel;

        /// <summary>
        /// 初始化控制器，读取 LauncherSettings 中的 Prefab 名称。
        /// </summary>
        /// <param name="settings">启动阶段配置。</param>
        public static void Initialize(LauncherSettings settings)
        {
            DestroyAll();
            s_Settings = settings;
            LauncherLocalization.Initialize(settings?.LocalizationJsonPathTemplate);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器域重载时重置静态状态。
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Settings = null;
            s_SplashPanel = null;
            s_ProgressPanel = null;
            s_DialogPanel = null;
        }
#endif

        /// <summary>
        /// 显示闪屏面板。
        /// </summary>
        public static void ShowSplash()
        {
            if (s_SplashPanel != null) return;
            s_SplashPanel = LoadPanel<LauncherSplashPanel>(s_Settings.SplashPanelPrefab, c_SplashSortingOrder);
        }

        /// <summary>
        /// 销毁闪屏面板。
        /// </summary>
        public static void DestroySplash()
        {
            DestroyPanel(ref s_SplashPanel);
        }

        /// <summary>
        /// 显示进度面板并将进度初始化为 0。
        /// 文本内容由面板自身通过 LauncherLocalization 驱动，调用方只需传 stage 供日志使用。
        /// </summary>
        /// <param name="stage">启动阶段（保留参数，供外部上下文感知，不再用于取文本）。</param>
        public static void ShowProgress(LauncherStage stage)
        {
            EnsureProgressPanel();
            if (s_ProgressPanel == null)
            {
                Log.Error(LogTag.Procedure, "进度面板加载失败，无法显示进度。");
                return;
            }

            s_ProgressPanel.gameObject.SetActive(true);
            s_ProgressPanel.SetProgress(0f);
        }

        /// <summary>
        /// 更新进度数值。
        /// </summary>
        /// <param name="progress">进度值（0 ~ 1）。</param>
        public static void UpdateProgress(float progress)
        {
            if (s_ProgressPanel != null)
            {
                s_ProgressPanel.SetProgress(progress);
            }
        }

        /// <summary>
        /// 隐藏进度面板（不销毁）。
        /// </summary>
        public static void HideProgress()
        {
            if (s_ProgressPanel != null)
            {
                s_ProgressPanel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 销毁进度面板。
        /// </summary>
        public static void DestroyProgress()
        {
            DestroyPanel(ref s_ProgressPanel);
        }

        /// <summary>
        /// 显示弹窗，文本内容由面板自身通过 LauncherLocalization 按 dialogType 驱动。
        /// </summary>
        /// <param name="dialogType">弹窗类型。</param>
        /// <param name="onConfirm">确认回调。</param>
        /// <param name="onCancel">取消回调（为 null 时隐藏取消按钮）。</param>
        public static void ShowDialog(LauncherDialogType dialogType, Action onConfirm, Action onCancel = null)
        {
            EnsureDialogPanel();
            if (s_DialogPanel == null)
            {
                Log.Error(LogTag.Procedure, "弹窗面板加载失败，无法显示弹窗。");
                onConfirm?.Invoke();
                return;
            }

            s_DialogPanel.Show(dialogType, onConfirm, onCancel);
        }

        /// <summary>
        /// 隐藏弹窗。
        /// </summary>
        public static void HideDialog()
        {
            if (s_DialogPanel != null)
            {
                s_DialogPanel.Hide();
            }
        }

        /// <summary>
        /// 销毁弹窗面板。
        /// </summary>
        public static void DestroyDialog()
        {
            DestroyPanel(ref s_DialogPanel);
        }

        /// <summary>
        /// 销毁所有启动阶段面板。
        /// </summary>
        public static void DestroyAll()
        {
            DestroyPanel(ref s_SplashPanel);
            DestroyPanel(ref s_ProgressPanel);
            DestroyPanel(ref s_DialogPanel);
        }

        /// <summary>
        /// 加载 Prefab 并实例化面板，强制覆盖所有 Canvas 的 sortingOrder。
        /// 约束 Splash（0） < Progress（10） < Dialog（20），框架强制赋值，Prefab 上的 Canvas 配置会被覆盖。
        /// </summary>
        /// <typeparam name="T">面板脚本类型。</typeparam>
        /// <param name="prefabName">Prefab 名称。</param>
        /// <param name="sortingOrder">Canvas sortingOrder 强制值。</param>
        /// <returns>面板实例；加载失败返回 null。</returns>
        private static T LoadPanel<T>(string prefabName, int sortingOrder) where T : MonoBehaviour
        {
            GameObject prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("启动阶段 UI Prefab 加载失败: {0}", prefabName));
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = prefabName;
            UnityEngine.Object.DontDestroyOnLoad(instance);

            Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].overrideSorting = true;
                canvases[i].sortingOrder = sortingOrder;
            }

            T component = instance.GetComponent<T>();
            if (component == null)
            {
                Log.Error(LogTag.Procedure, Txt.Format("Prefab {0} 上未找到组件 {1}，已销毁实例。", prefabName, typeof(T).Name));
                UnityEngine.Object.Destroy(instance);
                return null;
            }
            return component;
        }

        /// <summary>
        /// 销毁面板实例并置空引用。
        /// </summary>
        /// <typeparam name="T">面板脚本类型。</typeparam>
        /// <param name="panel">面板引用。</param>
        private static void DestroyPanel<T>(ref T panel) where T : MonoBehaviour
        {
            if (panel == null)
            {
                panel = null;
                return;
            }

            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel.gameObject);
            }

            panel = null;
        }

        /// <summary>
        /// 确保进度面板已加载。
        /// </summary>
        private static void EnsureProgressPanel()
        {
            if (s_ProgressPanel == null)
            {
                s_ProgressPanel = LoadPanel<LauncherProgressPanel>(s_Settings.ProgressPanelPrefab, c_ProgressSortingOrder);
            }
        }

        /// <summary>
        /// 确保弹窗面板已加载。
        /// </summary>
        private static void EnsureDialogPanel()
        {
            if (s_DialogPanel == null)
            {
                s_DialogPanel = LoadPanel<LauncherDialogPanel>(s_Settings.DialogPanelPrefab, c_DialogSortingOrder);
            }
        }
    }
}
