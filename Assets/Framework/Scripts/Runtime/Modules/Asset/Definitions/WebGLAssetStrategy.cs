/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebGLAssetStrategy.cs
 * author:    taoye
 * created:   2026/9/16
 * descrip:   WebGL 资源运行策略
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebGL 启动阶段的资源加载策略。
    /// </summary>
    public enum WebGLAssetStrategy : byte
    {
        /// <summary>
        /// 启动时预热配置 Tag 对应的 Bundle，完成后释放预热引用，并在启动 DLL 消费完成后清理未使用内存。
        /// 该值固定为零，保证旧场景新增序列化字段后仍采用 Nova 默认策略。
        /// </summary>
        TagsOnLaunch = 0,

        /// <summary>
        /// 启动时不预热，全部资源由业务异步按需加载。
        /// </summary>
        OnDemand = 1,

        /// <summary>
        /// 启动时加载默认包全部 Bundle，并持有到 AssetManager 关闭。
        /// </summary>
        AllOnLaunch = 2,
    }
}
