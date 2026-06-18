/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataReceiver.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   数据接收器基类
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 数据接收器基类，实现 IDataReceiver。
    /// 封装资源加载、解析与释放的完整流程。子类只需 override
    /// OnParseDataAsset(string) 和 OnParseDataAsset(byte[]) 完成数据解析。
    /// </summary>
    public abstract class DataReceiver : IDataReceiver
    {
        /// <summary>
        /// 异步加载资源的委托。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>加载到的资源对象，失败返回 null。</returns>
        public delegate UniTask<Object> LoadAssetAsyncFunc(string assetLocation);

        /// <summary>
        /// 释放资源的委托。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        public delegate void ReleaseAssetAction(object asset);

        /// <summary>
        /// 同步加载资源的委托。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>加载到的资源对象，失败返回 null。</returns>
        public delegate Object LoadAssetSyncFunc(string assetLocation);

        /// <summary>
        /// 资源加载委托。
        /// </summary>
        private readonly LoadAssetAsyncFunc m_LoadAssetAsyncFunc;
        /// <summary>
        /// 资源释放委托。
        /// </summary>
        private readonly ReleaseAssetAction m_ReleaseAssetAction;
        /// <summary>
        /// 同步资源加载委托，仅同步路径使用。
        /// </summary>
        private readonly LoadAssetSyncFunc m_LoadAssetSyncFunc;

        /// <summary>
        /// 读取数据任务完成源，Interlocked 保护并发防重入。
        /// </summary>
        private UniTaskCompletionSource<bool> m_ReadTcs;

        /// <summary>
        /// 构造方法（异步加载）。
        /// </summary>
        /// <param name="loadAssetAsyncFunc">异步加载资源的委托。</param>
        /// <param name="releaseAssetAction">释放资源的委托。</param>
        protected DataReceiver(LoadAssetAsyncFunc loadAssetAsyncFunc, ReleaseAssetAction releaseAssetAction)
        {
            m_LoadAssetAsyncFunc = loadAssetAsyncFunc ?? throw new ArgumentNullException(nameof(loadAssetAsyncFunc));
            m_ReleaseAssetAction = releaseAssetAction ?? throw new ArgumentNullException(nameof(releaseAssetAction));
        }

        /// <summary>
        /// 构造方法（同步加载）。
        /// </summary>
        /// <param name="loadAssetSyncFunc">同步加载资源的委托。</param>
        /// <param name="releaseAssetAction">释放资源的委托。</param>
        protected DataReceiver(LoadAssetSyncFunc loadAssetSyncFunc, ReleaseAssetAction releaseAssetAction)
        {
            m_LoadAssetSyncFunc = loadAssetSyncFunc ?? throw new ArgumentNullException(nameof(loadAssetSyncFunc));
            m_ReleaseAssetAction = releaseAssetAction ?? throw new ArgumentNullException(nameof(releaseAssetAction));
        }

        /// <summary>
        /// 触发资源加载（fire-and-forget）。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        public virtual void ReadDataAsset(string assetLocation)
        {
            if (m_LoadAssetAsyncFunc == null)
                throw new InvalidOperationException("未提供异步加载委托 LoadAssetAsyncFunc，无法执行异步加载。请使用包含 LoadAssetAsyncFunc 的构造函数。");
            ReadDataAssetAsync(assetLocation).Forget();
        }

        /// <summary>
        /// 异步触发资源加载、解析与释放的完整流程。
        /// 使用 Interlocked.CompareExchange 保证并发调用时只有第一次真正执行，后续等待同一个任务。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>是否加载并解析成功。</returns>
        public virtual async UniTask<bool> ReadDataAssetAsync(string assetLocation)
        {
            if (m_LoadAssetAsyncFunc == null)
                throw new InvalidOperationException("未提供异步加载委托 LoadAssetAsyncFunc，无法执行异步加载。请使用包含 LoadAssetAsyncFunc 的构造函数。");
            if (string.IsNullOrEmpty(assetLocation))
                throw new ArgumentException("assetLocation 无效。", nameof(assetLocation));

            var newTcs = new UniTaskCompletionSource<bool>();
            var existingTcs = Interlocked.CompareExchange(ref m_ReadTcs, newTcs, null);
            if (existingTcs != null)
            {
                Log.Warning(LogTag.Base, "DataReceiver 正在加载中，不可重复调用 ReadDataAssetAsync。assetLocation={0}", assetLocation);
                return await existingTcs.Task;
            }

            Object asset = await m_LoadAssetAsyncFunc(assetLocation);
            var task = m_ReadTcs.Task;
            OnAssetLoadComplete(asset, assetLocation);
            return await task;
        }

        /// <summary>
        /// 资源数据解析回调（字符串），子类必须实现。
        /// </summary>
        /// <param name="contentString">内容字符串。</param>
        /// <returns>是否成功。</returns>
        public abstract bool OnParseDataAsset(string contentString);

        /// <summary>
        /// 资源数据解析回调（字节流），子类必须实现。
        /// </summary>
        /// <param name="contentBytes">内容字节流。</param>
        /// <returns>是否成功。</returns>
        public abstract bool OnParseDataAsset(byte[] contentBytes);

        /// <summary>
        /// 释放资源数据，调用构造时传入的 ReleaseAssetAction。
        /// </summary>
        /// <param name="dataAsset">数据资产。</param>
        public virtual void OnReleaseDataAsset(object dataAsset)
        {
            m_ReleaseAssetAction?.Invoke(dataAsset);
        }

        /// <summary>
        /// 同步加载资源、解析与释放。要求构造时传入了 LoadAssetSyncFunc。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>是否加载并解析成功。</returns>
        public virtual bool ReadDataAssetSync(string assetLocation)
        {
            if (string.IsNullOrEmpty(assetLocation))
                throw new ArgumentException("assetLocation 无效。", nameof(assetLocation));
            if (m_LoadAssetSyncFunc == null)
                throw new InvalidOperationException("未提供同步加载委托 LoadAssetSyncFunc，无法执行同步加载。");

            Object asset = m_LoadAssetSyncFunc(assetLocation);
            if (asset == null)
            {
                Log.Error(LogTag.Base, "DataReceiver 同步资源加载结果无效。assetLocation={0}", assetLocation);
                return false;
            }

            TextAsset textAsset = asset as TextAsset;
            if (textAsset == null)
            {
                Log.Error(LogTag.Base, "DataReceiver 同步资源类型无效，期望 TextAsset，实际为 {0}。assetLocation={1}", asset.GetType().FullName, assetLocation);
                OnReleaseDataAsset(asset);
                return false;
            }

            bool success = false;
            try
            {
                success = TryParseTextAsset(textAsset);
            }
            finally
            {
                OnReleaseDataAsset(asset);
            }

            if (!success)
            {
                Log.Error(LogTag.Base, "同步资源解析失败，assetLocation={0}。", assetLocation);
            }

            return success;
        }

        /// <summary>
        /// 资源加载完成处理，先尝试 bytes 解析再 fallback 到 text 解析，最后释放资源并设置任务结果。
        /// </summary>
        /// <param name="asset">加载到的资源对象。</param>
        /// <param name="assetLocation">Asset 地址（用于日志）。</param>
        private void OnAssetLoadComplete(Object asset, string assetLocation)
        {
            if (asset == null)
            {
                Log.Error(LogTag.Base, "DataReceiver 资源加载结果无效。assetLocation={0}", assetLocation);
                m_ReadTcs?.TrySetResult(false);
                m_ReadTcs = null;
                return;
            }

            TextAsset textAsset = asset as TextAsset;
            if (textAsset == null)
            {
                Log.Error(LogTag.Base, "DataReceiver 资源类型无效，期望 TextAsset，实际为 {0}。assetLocation={1}", asset.GetType().FullName, assetLocation);
                m_ReadTcs?.TrySetResult(false);
                m_ReadTcs = null;
                return;
            }

            bool success = false;
            try
            {
                success = TryParseTextAsset(textAsset);
            }
            finally
            {
                OnReleaseDataAsset(asset);
            }

            if (!success)
            {
                Log.Error(LogTag.Base, "资源解析失败，assetLocation={0}。", assetLocation);
            }

            m_ReadTcs?.TrySetResult(success);
            m_ReadTcs = null;
        }

        /// <summary>
        /// 先尝试以 bytes 解析 TextAsset，失败后 fallback 到 text 解析。
        /// bytes 解析抛异常才触发 fallback；bytes 返回 false 但无异常则不 fallback。
        /// </summary>
        /// <param name="textAsset">已加载的 TextAsset。</param>
        /// <returns>任意解析路径成功返回 true，否则返回 false。</returns>
        private bool TryParseTextAsset(TextAsset textAsset)
        {
            try
            {
                if (OnParseDataAsset(textAsset.bytes))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Base, "bytes 解析失败，尝试 text fallback：{0}", e.Message);
            }

            try
            {
                return OnParseDataAsset(textAsset.text);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Base, "text fallback 也失败：{0}", e.Message);
                return false;
            }
        }
    }
}
