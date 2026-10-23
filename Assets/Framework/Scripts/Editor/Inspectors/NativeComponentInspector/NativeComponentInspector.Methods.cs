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
            if (m_EnableAndroidAppInstall != null)
                EditorUtil.Draw.PropertyField(m_EnableAndroidAppInstall, "允许 Android APK 安装", false);
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1) 通知权限只在业务显式请求时弹窗；APNs/FCM Token 与消息生命周期由对应 SDK 管理。",
                "(2) APK 安装默认关闭。开启后构建会声明来源安装权限，只支持 Android 9 及以上；用户仍需允许此来源并确认系统安装。",
                "(3) APK 下载与安装分别触发；安装只接受当前应用同签名、递增版本的完整文件，不支持签名轮换或静默安装。",
            });
            EditorUtil.Draw.Line();
        }
    }
}
