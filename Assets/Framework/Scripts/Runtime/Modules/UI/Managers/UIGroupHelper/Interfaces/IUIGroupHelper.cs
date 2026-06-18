/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IUIGroupHelper.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图分组辅助器接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图分组辅助器接口。
    /// </summary>
    public interface IUIGroupHelper
    {
        /// <summary>
        /// 设置视图分组深度换算系数。
        /// 由 UIManager 在创建视图分组时透传，乘以视图分组深度后得到 Canvas.sortingOrder。
        /// </summary>
        /// <param name="value">视图分组深度换算系数。</param>
        void SetDepthFactor(int value);

        /// <summary>
        /// 设置视图分组深度。
        /// </summary>
        /// <param name="depth">视图分组深度。</param>
        void SetDepth(int depth);
    }
}
