/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IRemoteConfigPlugin.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   远程配置 SDK 插件接口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 远程配置接口，抽象 Firebase Remote Config / Appcelerator 等平台的 Key-Value 下发能力。
    /// 使用前须先 FetchAsync 拉取并激活配置；激活后 OnConfigActivated 事件触发，此后 TryGet* 方法返回最新值。
    /// 所有方法主线程调用；FetchAsync 内部可切后台线程，完成前须切回主线程。
    /// </summary>
    public interface IRemoteConfigPlugin : ISDKPlugin
    {
        /// <summary>
        /// 异步拉取并激活最新远程配置。
        /// cacheExpiration 指定本地缓存有效期，null 则使用平台默认值；
        /// 缓存未过期时直接激活本地缓存，不发起网络请求。
        /// 网络异常时激活上一次缓存值并记录 Warning，不向调用方抛异常。
        /// </summary>
        /// <param name="cacheExpiration">本地缓存有效期，null 使用平台默认值。</param>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>拉取并激活完成的异步任务。</returns>
        UniTask FetchAsync(TimeSpan? cacheExpiration = null, CancellationToken ct = default);

        /// <summary>
        /// 尝试获取指定 key 对应的字符串值。
        /// key 不存在或类型不匹配时返回 false，value 为 null；存在时返回 true 并赋值。
        /// </summary>
        /// <param name="key">配置键名，大小写由平台定义（通常大小写敏感）。</param>
        /// <param name="value">输出的字符串值；获取失败时为 null。</param>
        /// <returns>成功获取返回 true，否则 false。</returns>
        bool TryGetString(string key, out string value);

        /// <summary>
        /// 尝试获取指定 key 对应的长整型值。
        /// key 不存在、值无法解析为 long 时返回 false，value 为 0；成功时赋值并返回 true。
        /// </summary>
        /// <param name="key">配置键名。</param>
        /// <param name="value">输出的 long 值；获取失败时为 0。</param>
        /// <returns>成功获取返回 true，否则 false。</returns>
        bool TryGetLong(string key, out long value);

        /// <summary>
        /// 尝试获取指定 key 对应的双精度浮点值。
        /// key 不存在、值无法解析为 double 时返回 false，value 为 0.0；成功时赋值并返回 true。
        /// </summary>
        /// <param name="key">配置键名。</param>
        /// <param name="value">输出的 double 值；获取失败时为 0.0。</param>
        /// <returns>成功获取返回 true，否则 false。</returns>
        bool TryGetDouble(string key, out double value);

        /// <summary>
        /// 尝试获取指定 key 对应的布尔值。
        /// key 不存在、值无法解析为 bool 时返回 false，value 为 false；成功时赋值并返回 true。
        /// </summary>
        /// <param name="key">配置键名。</param>
        /// <param name="value">输出的 bool 值；获取失败时为 false。</param>
        /// <returns>成功获取返回 true，否则 false。</returns>
        bool TryGetBool(string key, out bool value);

        /// <summary>
        /// 配置激活事件，在 FetchAsync 成功拉取并激活新配置后于主线程触发。
        /// 业务层可监听此事件并重新读取关心的配置项以响应远程变更。
        /// </summary>
        event Action OnConfigActivated;
    }
}
