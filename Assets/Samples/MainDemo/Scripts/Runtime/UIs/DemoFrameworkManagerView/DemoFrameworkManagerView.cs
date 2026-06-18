/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFrameworkManagerView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.2 — FrameworkManager 三层继承链与 Priority 排序可视化
 *            枚举 FrameworkManagersGroup 中所有已注册 Manager，
 *            展示 TypeName / Priority / 实现类名。
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.2 FrameworkManager 三层继承链与 Priority 排序可视化 View。
    /// 枚举所有已注册 Manager，展示 TypeName / Priority / 实现类。
    /// </summary>
    public sealed class DemoFrameworkManagerView : BaseDemoView
    {
        /// <summary>
        /// 「枚举 Manager 列表」按钮，点击后刷新所有 Manager 信息到反馈区。
        /// </summary>

        [SerializeField] private Button m_ListManagersButton;

        /// <summary>
        /// 初始化钩子，注册按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_ListManagersButton != null)
            {
                m_ListManagersButton.onClick.AddListener(OnListManagersClick);
            }
        }

        /// <summary>
        /// 打开钩子，设置标题和 API 副标题，并立即刷新 Manager 列表。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("FrameworkManager 列表");
            SetButtonApiHint(m_ListManagersButton, "Util.TypeCreator.Create<IXxxManager>() / Manager.Priority");
            RefreshManagerList();
        }

        /// <summary>
        /// 「枚举 Manager 列表」点击回调。
        /// </summary>
        private void OnListManagersClick()
        {
            ClearFeedback();
            RefreshManagerList();
        }

        /// <summary>
        /// 通过代表性接口枚举所有框架 Manager，输出到反馈区。
        /// 使用 FrameworkManagersGroup.GetManager 逐类型查询。
        /// </summary>
        private void RefreshManagerList()
        {
            List<(string interfaceName, int priority, string implName)> managers = CollectManagers();
            if (managers.Count == 0)
            {
                AppendFeedback("未检测到已注册的 Manager（Start 尚未执行？）", FeedbackLevel.Warn);
                return;
            }

            for (int i = 0; i < managers.Count; i++)
            {
                var (interfaceName, priority, implName) = managers[i];
                AppendFeedback($"{implName} Priority={priority} -> {interfaceName}", FeedbackLevel.Success);
            }
            AppendFeedback($"共 {managers.Count} 个 Manager 已注册", FeedbackLevel.Info);
        }

        /// <summary>
        /// 收集所有框架 Manager 信息列表，通过各模块接口类型查询 FrameworkManagersGroup。
        /// </summary>
        /// <returns>Manager 信息三元组列表（接口名 / Priority / 实现类名）。</returns>
        private static List<(string, int, string)> CollectManagers()
        {
            var result = new List<(string, int, string)>();
            TryAdd<IAssetManager>(result);
            TryAdd<IConfigManager>(result);
            TryAdd<IEventManager>(result);
            TryAdd<ITableManager>(result);
            TryAdd<ILocalizationManager>(result);
            TryAdd<IUIManager>(result);
            TryAdd<INetworkManager>(result);
            TryAdd<IProcedureManager>(result);
            TryAdd<IObjectPoolManager>(result);
            TryAdd<IPersistManager>(result);
            TryAdd<ISoundManager>(result);
            TryAdd<IVibrateManager>(result);
            TryAdd<ISDKManager>(result);
            TryAdd<IDebugManager>(result);
            return result;
        }

        /// <summary>
        /// 尝试通过接口类型查询 Manager，成功则加入列表。
        /// </summary>
        /// <typeparam name="T">Manager 接口类型。</typeparam>
        /// <param name="list">结果列表。</param>
        private static void TryAdd<T>(List<(string, int, string)> list) where T : class
        {
            T manager = FrameworkManagersGroup.GetManager<T>();
            if (manager != null && manager is FrameworkManager fm)
            {
                list.Add((typeof(T).Name, fm.Priority, fm.GetType().Name));
            }
        }
    }
}
