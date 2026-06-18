/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolConfig.cs
 * author:    taoye
 * created:   2026/4/7
 * descrip:   对象池创建配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池创建配置。
    /// 用于创建对象池时传入参数，替代多参数组合重载。
    /// </summary>
    public class ObjectPoolConfig
    {
        /// <summary>
        /// 对象池名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        public float AutoReleaseInterval { get; set; } = float.MaxValue;

        /// <summary>
        /// 对象池的容量。
        /// </summary>
        public int Capacity { get; set; } = int.MaxValue;

        /// <summary>
        /// 对象池对象过期秒数。
        /// </summary>
        public float ExpireTime { get; set; } = float.MaxValue;

        /// <summary>
        /// 对象池的优先级（值越小，优先级越高）。
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
