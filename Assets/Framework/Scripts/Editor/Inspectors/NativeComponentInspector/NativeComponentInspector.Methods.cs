/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   Native 组件 Inspector 绘制方法
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class NativeComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制 Manager 类型与 Native 模块边界说明。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector(
                "Native 管理器",
                m_NativeManagerTypeNames,
                m_CurNativeManagerTypeName,
                true,
                null,
                GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "通知权限只会在业务显式调用 RequestNotificationPermissionAsync 时请求，框架启动阶段不会自动弹窗。",
                "APNs/FCM Token 与消息生命周期由对应 SDK 管理，不属于 Native 模块。",
            });
            EditorUtil.Draw.Line();
        }
    }
}
