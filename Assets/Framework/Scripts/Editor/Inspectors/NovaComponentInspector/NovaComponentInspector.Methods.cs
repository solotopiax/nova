/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   Nova组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class NovaComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 获取游戏速率值。
        /// </summary>
        private static float GetGameSpeed(int index)
        {
            return s_GameSpeeds[Mathf.Clamp(index, 0, s_GameSpeeds.Length - 1)];
        }

        /// <summary>
        /// 获取游戏速率索引。
        /// </summary>
        private static int GetSelectedGameSpeed(float value)
        {
            for (int i = 0; i < s_GameSpeeds.Length; i++)
            {
                if (Mathf.Approximately(s_GameSpeeds[i], value))
                {
                    return i;
                }
            }
            return -1;
        }

    }
}
