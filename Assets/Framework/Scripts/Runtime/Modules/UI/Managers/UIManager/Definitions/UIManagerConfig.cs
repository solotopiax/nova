/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManagerConfig.cs
 * author:    taoye
 * created:   2026/3/3
 * descrip:   UI 管理器配置
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 管理器配置。
    /// </summary>
    public class UIManagerConfig
    {
        /// <summary>
        /// 视图实例对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        public float InstanceAutoReleaseInterval;

        /// <summary>
        /// 视图实例对象池的容量上限。
        /// </summary>
        public int InstanceCapacity;

        /// <summary>
        /// 视图实例对象在对象池中的过期秒数。
        /// </summary>
        public float InstanceExpireTime;

        /// <summary>
        /// 视图实例对象池的优先级。
        /// </summary>
        public int InstancePriority;

        /// <summary>
        /// 每帧最多销毁的 UI 数量（回收队列每帧处理上限）。
        /// </summary>
        public int DestroyMaxNumPerFrame;

        /// <summary>
        /// 视图分组深度换算系数。视图分组深度乘以此系数后赋值给 Canvas.sortingOrder，
        /// 控制不同分组之间的层级间隔。Canvas.sortingOrder 有效范围为 short（-32768~32767）。
        /// </summary>
        public int GroupDepthFactor;

        /// <summary>
        /// 视图内部深度换算系数。视图在分组内的深度乘以此系数后叠加到 Canvas.sortingOrder。
        /// 单分组内可容纳视图数量约为 GroupDepthFactor / ViewDepthFactor。
        /// </summary>
        public int ViewDepthFactor;

        /// <summary>
        /// UI 注册表单元设置列表，每个 unit 对应一个注册表 JSON 文件。
        /// </summary>
        public List<UIUnitSetting> UnitSettings;

        /// <summary>
        /// 安全区域数据提供者，为 null 时使用 DefaultSafeAreaProvider（基于 Screen.safeArea）。
        /// WebGL 平台可注入 DY/WX SDK 实现以获取平台特定安全区域。
        /// </summary>
        public ISafeAreaProvider SafeAreaProvider;
    }
}
