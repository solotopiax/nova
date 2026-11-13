/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAssetPackageLoader.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   可选的显式资源包加载能力，不增加旧资源管理接口的实现负担
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 按精确包名加载资源的可选能力，与 IAssetManager 分离以兼容仅实现旧接口的自定义管理器。
    /// 必须先按原流程完成目标包注册与清单加载；句柄所有权和失败判定沿用各默认包加载通道。
    /// </summary>
    public interface IAssetPackageLoader
    {
        /// <summary>
        /// 同步加载指定包的主资源句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        IAssetHandle<T> LoadFromPackageSync<T>(string package, string location) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载指定包的主资源句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        UniTask<IAssetHandle<T>> LoadFromPackageAsync<T>(string package, string location, CancellationToken ct = default) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载指定包的子资源批量句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        ISubAssetsHandle<T> LoadSubsFromPackageSync<T>(string package, string location) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载指定包的子资源批量句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        UniTask<ISubAssetsHandle<T>> LoadSubsFromPackageAsync<T>(string package, string location, CancellationToken ct = default) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载指定包的全资源批量句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        IAllAssetsHandle<T> LoadAllFromPackageSync<T>(string package, string location) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载指定包的全资源批量句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        UniTask<IAllAssetsHandle<T>> LoadAllFromPackageAsync<T>(string package, string location, CancellationToken ct = default) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载指定包的原始文件句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        IRawFileHandle LoadRawFromPackageSync(string package, string location);

        /// <summary>
        /// 异步加载指定包的原始文件句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        UniTask<IRawFileHandle> LoadRawFromPackageAsync(string package, string location, CancellationToken ct = default);

        /// <summary>
        /// 同步加载指定包的场景句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责调用 UnloadAsync；不提供普通 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="mode">场景加载模式；Single 会替换已有场景。</param>
        /// <returns>由调用方持有并负责卸载的场景句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        ISceneHandle LoadSceneFromPackageSync(string package, string location, LoadSceneMode mode = LoadSceneMode.Single);

        /// <summary>
        /// 异步加载指定包的场景句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责调用 UnloadAsync；不提供普通 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="mode">场景加载模式；Single 会替换已有场景。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责卸载的场景句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        UniTask<ISceneHandle> LoadSceneFromPackageAsync(string package, string location, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken ct = default);
    }
}
