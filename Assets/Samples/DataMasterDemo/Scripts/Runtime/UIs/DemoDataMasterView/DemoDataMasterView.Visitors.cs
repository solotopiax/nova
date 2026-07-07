/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoDataMasterView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/07/03
 * descrip:   DemoDataMasterView 演示 View — 字段与属性
 ***************************************************************/

using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Datamaster.Samples.Runtime
{
    /// <summary>
    /// DemoDataMasterView 演示 View 的字段声明。
    /// 每个按钮对应 DataMaster 一个常用接口，点击后就近打印反馈。
    /// </summary>
    public sealed partial class DemoDataMasterView
    {
        /// <summary>
        /// 清理 SDK 缓存按钮（ClearRuntimeCache），置于交互区顶部。
        /// </summary>
        [SerializeField] private Button m_ClearCacheButton;

        /// <summary>
        /// 读取实验参数按钮（GetParamValue&lt;T&gt;）。
        /// </summary>
        [SerializeField] private Button m_ReadParamButton;

        /// <summary>
        /// 读取实验参数（通过 JSON）按钮（GetParamValueJson）。
        /// </summary>
        [SerializeField] private Button m_ReadJsonButton;

        /// <summary>
        /// 标记曝光按钮（MarkExposure）。
        /// </summary>
        [SerializeField] private Button m_ExposureButton;

        /// <summary>
        /// 上报实验事件按钮（LogExperimentEvent）。
        /// </summary>
        [SerializeField] private Button m_LogEventButton;

        /// <summary>
        /// 设置分流属性按钮（SetUserProperty）。
        /// </summary>
        [SerializeField] private Button m_SetPropertyButton;

        /// <summary>
        /// 模拟登录并触发拉取按钮（Nova.SDK.Login → RefreshFromServer）。
        /// </summary>
        [SerializeField] private Button m_LoginRefreshButton;
    }
}
