/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugComponent.Methods.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   调试组件 —— 私有方法
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public sealed partial class DebugComponent : FrameworkComponent
    {
        /// <summary>
        /// 根据 m_DebuggerActiveType 判断当前环境是否应激活 Debugger。
        /// </summary>
        /// <returns>是否激活。</returns>
        private bool IsDebuggerActive()
        {
            switch (m_DebuggerActiveType)
            {
                case DebuggerActiveType.AlwaysEnable:
                    return true;

                case DebuggerActiveType.OnlyEnableWhenDevelopment:
                    return UnityEngine.Debug.isDebugBuild;

                case DebuggerActiveType.OnlyEnableInEditor:
                    return Application.isEditor;

                default:
                    return false;
            }
        }
    }
}
