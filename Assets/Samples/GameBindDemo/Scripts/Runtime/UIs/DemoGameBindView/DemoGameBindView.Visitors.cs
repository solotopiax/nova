/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameBindView.Visitors.cs
 * author:    taoye
 * created:   2026/07/02
 * descrip:   GameBind Kit 演示 View — 字段与属性
 ***************************************************************/

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Kit.Network.GameBind.Samples.Runtime
{
    /// <summary>
    /// GameBind Kit 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoGameBindView
    {
        /// <summary>
        /// 三方 openId 输入框；绑定 / 冲突查询 / 裁决三步共用同一 openId。
        /// </summary>

        [SerializeField] private TMP_InputField m_OpenIdInput;

        /// <summary>
        /// 二选一选择开关；勾选=existing（保留对方云端账号），不勾=guest（保留当前账号）。
        /// </summary>

        [SerializeField] private Toggle m_ChoiceToggle;

        /// <summary>
        /// 裁决二次验证码输入框；可空，按业务需要填写（高危操作防盗号）。
        /// </summary>

        [SerializeField] private TMP_InputField m_VerifyCodeInput;

        /// <summary>
        /// forceNewAccount 开关；勾选后登录时强制注册新账号（用于新注册用户），传入 Login.Async。
        /// </summary>

        [SerializeField] private Toggle m_ForceNewAccountToggle;

        /// <summary>
        /// 登录按钮；绑定的前提是已登录，点击后触发 Nova.Network.Kit<Login>().Async 取得当前账号 UID。
        /// </summary>

        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 绑定按钮；点击后触发 Nova.Network.Kit&lt;Bind&gt;().BindAsync(ThirdLoginProvider.Google, openId)。
        /// </summary>

        [SerializeField] private Button m_BindButton;

        /// <summary>
        /// 根存档 JSON 输入框；上传时作为整包载荷写入 Nova.Network.Kit<Save>().SetFullAsync(value)。
        /// </summary>

        [SerializeField] private TMP_InputField m_SaveJsonInput;

        /// <summary>
        /// 上传存档按钮；点击后触发 Nova.Network.Kit<Save>().SetFullAsync(json) 全量上传用户根存档。
        /// </summary>

        [SerializeField] private Button m_UploadSaveButton;

        /// <summary>
        /// 获取存档按钮；点击后触发 Nova.Network.Kit<Save>().GetFullAsync() 全量拉取当前用户根存档并打印到日志。
        /// </summary>

        [SerializeField] private Button m_GetSaveButton;

        /// <summary>
        /// 冲突查询按钮；绑定返回 ErrBindConflict(10402) 后点击，触发 Nova.Network.Kit<Bind>().QueryConflictAsync(openId) 拉对方账号进度摘要。
        /// </summary>

        [SerializeField] private Button m_QueryConflictButton;

        /// <summary>
        /// 目标 uid 输入框；查询指定用户存档时填入要查询的 uid，为空则查当前登录用户自身。
        /// </summary>

        [SerializeField] private TMP_InputField m_TargetUidInput;

        /// <summary>
        /// 查询指定 uid 存档按钮；点击后触发 Nova.Network.Kit<Save>().GetFullAsync(targetUid) 拉取指定用户的云端存档并打印到日志。
        /// </summary>

        [SerializeField] private Button m_QuerySaveByUidButton;

        /// <summary>
        /// 裁决按钮；玩家二选一后点击，触发 Nova.Network.Kit<Bind>().ResolveAsync(openId, choice, verifyCode) 做账号归属裁决。
        /// </summary>

        [SerializeField] private Button m_ResolveButton;
    }
}
