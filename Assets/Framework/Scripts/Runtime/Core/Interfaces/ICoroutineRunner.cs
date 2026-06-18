/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ICoroutineRunner.cs
 * author:    taoye
 * created:   2026/1/12
 * descrip:   协程运行器
 ***************************************************************/

using System.Collections;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 协程运行器接口。
    /// </summary>
    public interface ICoroutineRunner
    {
        /// <summary>
        /// 开始协程。
        /// </summary>
        /// <param name="coroutine">协程方法。</param>
        /// <returns>协程对象。</returns>
        Coroutine StartCoroutine(IEnumerator coroutine);
        
        /// <summary>
        /// 停止协程。
        /// </summary>
        /// <param name="coroutine">协程对象。</param>
        void StopCoroutine(Coroutine coroutine);
        
    }
}
