/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProfilerLateUpdateListener.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;

    /// <summary>
    /// The profiler has a separate monobehaviour to listen for LateUpdate, and is placed
    /// at the end of the script execution order.
    /// </summary>
    public class ProfilerLateUpdateListener : MonoBehaviour
    {
        public Action OnLateUpdate;

        private void LateUpdate()
        {
            if (OnLateUpdate != null)
            {
                OnLateUpdate();
            }
        }
    }
}
