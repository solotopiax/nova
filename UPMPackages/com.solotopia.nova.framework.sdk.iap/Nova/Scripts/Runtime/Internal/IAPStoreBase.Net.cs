/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.Net.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   IAPStoreBase 通用网络请求能力：URL 解析 + JSON POST（VoucherStore）
 ***************************************************************/

using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine.Networking;

namespace NovaFramework.SDK.IAP.Runtime
{
    public abstract partial class IAPStoreBase
    {
        /// <summary>
        /// HTTP 请求超时时间（秒）。
        /// </summary>
        private const int c_RequestTimeoutSeconds = 30;

        /// <summary>
        /// 通过 NetworkManager 解析指定 NetCmd 行名称 对应的服务端 URL。
        /// NetworkManager 为 null 或解析抛出异常时返回 null。
        /// </summary>
        /// <param name="dtName">已加载 NetCmd 表行的 Name，不带表名前缀。</param>
        /// <returns>URL 字符串；不可用时返回 null。</returns>
        protected string GetNetCmdUrl(string dtName)
        {
            if (Context?.NetworkManager == null)
            {
                return null;
            }

            try
            {
                INetworkCmdRow row = Context.NetworkManager.ResolveNetCmdRow(dtName);
                return Context.NetworkManager.ResolveNetCmdUrl(row);
            }
            catch (Exception ex)
            {
                LogWarning($"GetNetCmdUrl 异常 dtName={dtName}：{ex.Message}");
                return null;
            }
        }

    }
}
