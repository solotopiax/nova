/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IUIGroup.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图分组接口
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图分组接口。
    /// </summary>
    public interface IUIGroup
    {
        /// <summary>
        /// 获取视图分组名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取或设置视图分组深度。
        /// </summary>
        int Depth { get; set; }

        /// <summary>
        /// 获取或设置视图分组是否暂停。
        /// </summary>
        bool Pause { get; set; }

        /// <summary>
        /// 获取视图分组中视图数量。
        /// </summary>
        int UIViewCount { get; }

        /// <summary>
        /// 获取当前视图。
        /// 当前最顶层的视图。
        /// </summary>
        IUIView CurrentUIView { get; }

        /// <summary>
        /// 获取视图分组辅助器。
        /// </summary>
        IUIGroupHelper Helper { get; }

        /// <summary>
        /// 视图分组深度换算系数。视图分组深度乘以此系数后得到 Canvas.sortingOrder。
        /// 由 UIComponent Inspector 配置，UIManager 透传到分组。
        /// </summary>
        int GroupDepthFactor { get; }

        /// <summary>
        /// 视图内部深度换算系数。视图在分组内的深度乘以此系数后叠加到 Canvas.sortingOrder。
        /// 由 UIComponent Inspector 配置，UIManager 透传到分组。
        /// </summary>
        int ViewDepthFactor { get; }

        /// <summary>
        /// 视图分组中是否存在视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>视图分组中是否存在视图。</returns>
        bool HasUIView(int serialID);

        /// <summary>
        /// 视图分组中是否存在视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>视图分组中是否存在视图。</returns>
        bool HasUIView(string assetLocation);

        /// <summary>
        /// 从视图分组中获取视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>要获取的视图。</returns>
        IUIView GetUIView(int serialID);

        /// <summary>
        /// 从视图分组中获取视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图。</returns>
        IUIView GetUIView(string assetLocation);

        /// <summary>
        /// 从视图分组中获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图列表。</returns>
        IUIView[] GetUIViews(string assetLocation);

        /// <summary>
        /// 从视图分组中获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="results">要获取的视图列表。</param>
        void GetUIViews(string assetLocation, List<IUIView> results);

        /// <summary>
        /// 从视图分组中获取所有视图。
        /// </summary>
        /// <returns>视图分组中的所有视图。</returns>
        IUIView[] GetAllUIViews();

        /// <summary>
        /// 从视图分组中获取所有视图。
        /// </summary>
        /// <param name="results">视图分组中的所有视图。</param>
        void GetAllUIViews(List<IUIView> results);
    }
}
