/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.AppStore.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   跳应用商店静态工具
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// 跳应用商店工具。
        /// 平台差异（iOS itms-apps:// / Android market://）由调用方在 url 中按平台填好，
        /// 业务通常存放在 AppManagerConfig.PrimaryDownloadUrl。
        /// </summary>
        public static class AppStore
        {
            /// <summary>
            /// 异步打开 url。当前实现走 Application.OpenURL 同步触发，
            /// 返回 UniTask.CompletedTask 以匹配业务侧 await 风格。
            /// </summary>
            /// <param name="url">商店 / 包下载地址。</param>
            /// <returns>已完成的 UniTask。</returns>
            public static UniTask OpenAsync(string url)
            {
                Application.OpenURL(url);
                return UniTask.CompletedTask;
            }
        }
    }
}
