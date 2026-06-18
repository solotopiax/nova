/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   Nova组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class NovaComponentInspector : BaseComponentInspector
    {
        private static readonly float[] s_GameSpeeds = new float[] { 0f, 0.01f, 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f, 4f, 8f };
        private static readonly string[] s_GameSpeedTexts = new string[] { "0x", "0.01x", "0.1x", "0.25x", "0.5x", "1x", "1.5x", "2x", "4x", "8x" };

        /// <summary>
        /// 运行帧率。
        /// 游戏运行时每秒的最高帧数。
        /// </summary>
        private SerializedProperty m_FrameRate;

        /// <summary>
        /// 运行速率。
        /// 游戏运行时的最快速率。
        /// </summary>
        private SerializedProperty m_GameSpeed;

        /// <summary>
        /// 启用后台运行。
        /// 游戏挂起时系统后台是否继续运行游戏。
        /// </summary>
        private SerializedProperty m_RunInBackground;

        /// <summary>
        /// 启用屏幕常亮。
        /// 游戏运行一段时间后屏幕是否常亮以避免设备进入锁定状态。
        /// </summary>
        private SerializedProperty m_NeverSleep;

        /// <summary>
        /// 启用引用严格检查。
        /// </summary>
        private SerializedProperty m_ReferenceStrictCheckType;

        /// <summary>
        /// 当前 Txt 助手类型名称。
        /// </summary>
        private SerializedProperty m_CurTxtHelperTypeName;

        /// <summary>
        /// 当前 Log 助手类型名称。
        /// </summary>
        private SerializedProperty m_CurLogHelperTypeName;

        /// <summary>
        /// 当前 Reference 助手类型名称。
        /// </summary>
        private SerializedProperty m_CurReferenceHelperTypeName;

        /// <summary>
        /// Txt 助手所有类型名称。
        /// </summary>
        private List<string> m_TxtHelperTypeNames;

        /// <summary>
        /// Log 助手所有类型名称。
        /// </summary>
        private List<string> m_LogHelperTypeNames;

        /// <summary>
        /// Reference 助手所有类型名称。
        /// </summary>
        private List<string> m_ReferenceHelperTypeNames;

    }
}
