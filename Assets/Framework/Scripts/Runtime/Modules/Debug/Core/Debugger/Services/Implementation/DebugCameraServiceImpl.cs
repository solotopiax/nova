/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugCameraServiceImpl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    [Service(typeof (IDebugCameraService))]
    public class DebugCameraServiceImpl : IDebugCameraService
    {
        private Camera _debugCamera;

        public DebugCameraServiceImpl()
        {
            if (Settings.Instance.UseDebugCamera)
            {
                _debugCamera = new GameObject("RuntimeDebuggerCamera").AddComponent<Camera>();

                _debugCamera.cullingMask = 1 << Settings.Instance.DebugLayer;
                _debugCamera.depth = Settings.Instance.DebugCameraDepth;

                _debugCamera.clearFlags = CameraClearFlags.Depth;

                _debugCamera.transform.SetParent(Hierarchy.Get("RuntimeDebugger"));
            }
        }

        public Camera Camera
        {
            get { return _debugCamera; }
        }
    }
}
