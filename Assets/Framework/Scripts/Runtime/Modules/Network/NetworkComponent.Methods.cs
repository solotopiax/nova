/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件 —— 私有方法
 ***************************************************************/

using System.Collections;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 开始协程（ICoroutineRunner 实现）。
        /// </summary>
        /// <param name="coroutine">协程方法。</param>
        /// <returns>协程对象。</returns>
        Coroutine ICoroutineRunner.StartCoroutine(IEnumerator coroutine)
        {
            return StartCoroutine(coroutine);
        }

        /// <summary>
        /// 停止协程（ICoroutineRunner 实现）。
        /// </summary>
        /// <param name="coroutine">协程对象。</param>
        void ICoroutineRunner.StopCoroutine(Coroutine coroutine)
        {
            StopCoroutine(coroutine);
        }
    }
}
