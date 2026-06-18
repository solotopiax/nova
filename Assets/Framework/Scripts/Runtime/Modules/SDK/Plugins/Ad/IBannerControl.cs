/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IBannerControl.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   Banner 广告专属控制接口，IAdPlugin / IAdChannelPlugin 继承此接口
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Banner 广告专属控制接口，聚合 Banner 超出三段式之外的额外控制方法。
    /// IAdPlugin 继承此接口；调用方无需单独持有 IBannerControl 引用。
    /// </summary>
    public interface IBannerControl
    {
        /// <summary>
        /// 显示已加载的 Banner 广告。
        /// </summary>
        void ShowBanner();

        /// <summary>
        /// 隐藏 Banner 广告（不销毁，可再次 ShowBanner 恢复）。
        /// </summary>
        void HideBanner();

        /// <summary>
        /// 销毁 Banner 广告实例并释放资源。
        /// </summary>
        void DestroyBanner();

        /// <summary>
        /// 通过枚举更新 Banner 停靠位置。
        /// </summary>
        /// <param name="position">Banner 停靠位置。</param>
        void UpdateBannerPosition(BannerPosition position);

        /// <summary>
        /// 通过像素坐标更新 Banner 位置。
        /// </summary>
        /// <param name="position">Banner 屏幕坐标（逻辑像素）。</param>
        void UpdateBannerPosition(Vector2 position);

        /// <summary>
        /// 开启 Banner 自动刷新（仅 MAX 有效）。
        /// </summary>
        void StartBannerAutoRefresh();

        /// <summary>
        /// 停止 Banner 自动刷新（仅 MAX 有效）。
        /// </summary>
        void StopBannerAutoRefresh();

        /// <summary>
        /// 设置 Banner 宽度（逻辑像素，仅 MAX 有效）。
        /// </summary>
        /// <param name="width">Banner 宽度（逻辑像素）。</param>
        void SetBannerWidth(float width);

        /// <summary>
        /// 获取 Banner 自适应高度（逻辑像素，仅 MAX 有效）。
        /// </summary>
        /// <param name="width">指定宽度（逻辑像素），-1 表示使用屏幕宽度。</param>
        /// <returns>自适应高度（逻辑像素）；不支持时返回 -1。</returns>
        float GetAdaptiveBannerHeight(float width = -1f);

        /// <summary>
        /// 设置 Banner 背景色（仅 MAX 有效）。
        /// </summary>
        /// <param name="color">背景颜色。</param>
        void SetBannerBackgroundColor(Color color);
    }
}
