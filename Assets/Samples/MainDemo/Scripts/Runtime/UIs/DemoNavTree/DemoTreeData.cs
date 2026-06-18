/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoTreeData.cs
 * author:    taoye
 * created:   2026/05/22
 * descrip:   演示树静态数据源。
 *            职责：在静态构造器中硬编码完整树骨架并设置 Parent 指针，
 *            供 DemoNavTreeView.OnInit 直接消费。
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 演示树导航静态数据源。
    /// 持有整棵 DemoNode 树的单一根节点，按 Nova.prefab 模块挂载顺序构建。
    /// </summary>
    internal static class DemoTreeData
    {
        /// <summary>
        /// 树根节点，静态构造器中完成全树初始化并设置双向 Parent 指针。
        /// </summary>
        public static readonly DemoNode Root;

        /// <summary>
        /// 静态构造器，硬编码完整树骨架并完成 Parent 指针绑定。
        /// </summary>
        static DemoTreeData()
        {
            Root = BuildRoot();
        }

        /// <summary>
        /// 构建完整节点树并返回根节点。
        /// </summary>
        /// <returns>构建完成的根节点。</returns>
        private static DemoNode BuildRoot()
        {
            DemoNode root = MakeBranch("MainDemo", "MainDemo");

            DemoNode core = MakeBranch("1.Core 核心库", "Core");
            AddChild(root, core);
            AddChild(core, MakeViewLeaf<DemoFrameworkComponentView>("1.1 FrameworkComponent 框架组件类"));
            AddChild(core, MakeViewLeaf<DemoFrameworkManagerView>("1.2 FrameworkManager 框架管理器类"));
            AddChild(core, MakeViewLeaf<DemoFsmView>("1.3 Fsm 有限状态机"));
            AddChild(core, MakeViewLeaf<DemoReferenceView>("1.4 Reference 内存引用池"));
            AddChild(core, MakeViewLeaf<DemoLogView>("1.5 Log 日志打印系统"));
            AddChild(core, MakeViewLeaf<DemoUtilView>("1.6 Util 实用函数集"));
            AddChild(core, MakeViewLeaf<DemoCollectionsView>("1.7 Collections 容器封装"));
            AddChild(core, MakeViewLeaf<DemoExtensionsView>("1.8 Extensions 拓展方法"));
            AddChild(core, MakeViewLeaf<DemoEdgeCasesView>("1.9 Edge Cases 极端边界"));

            DemoNode modules = MakeBranch("2.Modules 模块库", "Modules");
            AddChild(root, modules);
            AddChild(modules, MakeViewLeaf<DemoAppView>("2.1 App 应用组件"));
            AddChild(modules, MakeViewLeaf<DemoAssetView>("2.2 Asset 资源组件"));
            AddChild(modules, MakeViewLeaf<DemoPrefabView>("2.3 Prefab 实例化组件"));
            AddChild(modules, MakeViewLeaf<DemoConfigView>("2.4 Config 配置组件"));
            AddChild(modules, MakeViewLeaf<DemoEventView>("2.5 Event 事件组件"));
            AddChild(modules, MakeViewLeaf<DemoTableView>("2.6 Table 表格组件"));
            AddChild(modules, MakeViewLeaf<DemoLocalizationView>("2.7 Localization 本地化组件"));
            AddChild(modules, MakeViewLeaf<DemoUIView>("2.8 UI 界面组件"));
            AddChild(modules, MakeViewLeaf<DemoNetworkView>("2.9 Network 网络组件"));
            AddChild(modules, MakeViewLeaf<DemoProcedureView>("2.10 Procedure 流程组件"));
            AddChild(modules, MakeViewLeaf<DemoObjectPoolView>("2.11 ObjectPool 对象池组件"));
            AddChild(modules, MakeViewLeaf<DemoPersistView>("2.12 Persist 持久化组件"));
            AddChild(modules, MakeViewLeaf<DemoSoundView>("2.13 Sound 声音组件"));
            AddChild(modules, MakeViewLeaf<DemoVibrateView>("2.14 Vibrate 振动组件"));
            AddChild(modules, MakeViewLeaf<DemoSDKView>("2.15 SDK 插件组件"));
            AddChild(modules, MakeViewLeaf<DemoDebugView>("2.16 Debug 调试组件"));

            DemoNode hybridClr = MakeBranch("3.HybridCLR 运行时热更新解决方案", "HybridCLR");
            AddChild(root, hybridClr);
            AddChild(hybridClr, MakeViewLeaf<DemoHybridClrAotMetadataView>("3.1 AOT metadata 加载演示"));
            AddChild(hybridClr, MakeViewLeaf<DemoHybridClrGameDllView>("3.2 业务 dll 加载演示"));
            AddChild(hybridClr, MakeViewLeaf<DemoHybridClrProcedureRegisterView>("3.3 业务 Procedure 注册时序"));

            DemoNode integration = MakeBranch("4.Integration 跨模块联动", "Integration");
            AddChild(root, integration);
            AddChild(integration, MakeViewLeaf<DemoIntegrationUiLocalizationView>("4.1 UI + Localization 切语言"));
            AddChild(integration, MakeViewLeaf<DemoIntegrationUiAssetView>("4.2 UI + Asset 异步加载 prefab"));
            AddChild(integration, MakeViewLeaf<DemoIntegrationProcedureAssetView>("4.3 Procedure + Asset 热更链路"));
            AddChild(integration, MakeViewLeaf<DemoIntegrationEventNetworkView>("4.4 Event + Network 事件桥接"));
            AddChild(integration, MakeViewLeaf<DemoIntegrationConfigHybridClrView>("4.5 Config + HybridCLR Namespace 注入"));

            return root;
        }

        /// <summary>
        /// 创建一个分支节点（有子节点，可折叠展开）。
        /// </summary>
        /// <param name="title">节点显示名称。</param>
        /// <param name="path">节点全路径。</param>
        /// <returns>新建的分支节点。</returns>
        private static DemoNode MakeBranch(string title, string path)
        {
            return new DemoNode
            {
                Title = title,
                Path = path,
                IsLeaf = false,
                IsExpanded = false,
                Parent = null,
                Children = new List<DemoNode>()
            };
        }

        /// <summary>
        /// 创建一个叶子节点，点击后通过 Nova.UI.OpenUIViewAsync 打开对应的 DemoXxxView。
        /// AssetLocation 与 UIView 子类名强一致（收尾-3 已确认 UIs.xlsx Name 列与类名对齐）。
        /// </summary>
        /// <typeparam name="T">要打开的 UIView 子类类型。</typeparam>
        /// <param name="title">节点显示名称。</param>
        /// <returns>新建的叶子节点。</returns>
        private static DemoNode MakeViewLeaf<T>(string title) where T : UIView
        {
            return new DemoNode
            {
                Title = title,
                Path = typeof(T).Name,
                IsLeaf = true,
                IsExpanded = false,
                Parent = null,
                Children = null,
                OpenCallback = () => Nova.UI.OpenUIViewAsync<T>()
            };
        }

        /// <summary>
        /// 将子节点加入父节点，并设置双向 Parent 指针。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="child">子节点。</param>
        private static void AddChild(DemoNode parent, DemoNode child)
        {
            child.Parent = parent;
            parent.Children.Add(child);
        }
    }
}
