/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameSaveView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/01
 * descrip:   GameSave Kit 演示 View — 字段与属性
 ***************************************************************/

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Kit.Network.GameSave.Samples.Runtime
{
    /// <summary>
    /// GameSave Kit 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoGameSaveView
    {
        /// <summary>
        /// openId 输入框；演示先登录拿到 UID 后再调用 GameSave 接口，存档依赖已登录的用户身份。
        /// </summary>

        [SerializeField] private TMP_InputField m_OpenIdInput;

        /// <summary>
        /// 存档 key 输入框；单 key 的 Set/Get 操作使用此值。
        /// </summary>

        [SerializeField] private TMP_InputField m_KeyInput;

        /// <summary>
        /// 存档 value 输入框；单 key 的 Set 操作与全量 SetFull 操作使用此 JSON 载荷。
        /// </summary>

        [SerializeField] private TMP_InputField m_ValueInput;

        /// <summary>
        /// 登录按钮；点击后触发 Nova.Network.Kit<Login>().Async 取得 UID，后续 GameSave 调用才有有效身份。
        /// </summary>

        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 写入存档按钮；点击后触发 GameSave.SetAsync(key, value) 单 key 写入。
        /// </summary>

        [SerializeField] private Button m_SetButton;

        /// <summary>
        /// 读取存档按钮；点击后触发 GameSave.GetAsync(key) 单 key 读取。
        /// </summary>

        [SerializeField] private Button m_GetButton;

        /// <summary>
        /// 批量写入按钮；点击后触发 GameSave.SetAsync(keys, values) 批量写入示例数据。
        /// </summary>

        [SerializeField] private Button m_SetBatchButton;

        /// <summary>
        /// 批量读取按钮；点击后触发 GameSave.GetAsync(keys) 批量读取示例 key。
        /// </summary>

        [SerializeField] private Button m_GetBatchButton;

        /// <summary>
        /// 全量读取按钮；点击后触发 GameSave.GetFullAsync() 拉取全部存档节点。
        /// </summary>

        [SerializeField] private Button m_GetFullButton;

        /// <summary>
        /// 全量写入按钮；点击后触发 GameSave.SetFullAsync(value) 以 value 作整包载荷写入。
        /// </summary>

        [SerializeField] private Button m_SetFullButton;
    }
}
