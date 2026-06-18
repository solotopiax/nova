/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Vector3.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Vector3
 *            提供3D射线检测工具
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 Unity 的扩展方法。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 获取屏幕射线检测到的 3D 对象的 Transform。
        /// </summary>
        /// <param name="origin">屏幕坐标点。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform，未命中返回 null。</returns>
        public static Transform GetRaycastHit3DTransform(this Vector3 origin, Camera camera)
        {
            Ray ray = camera.ScreenPointToRay(origin);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.collider.transform;
            }
            return null;
        }
    }
}
