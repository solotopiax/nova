/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Vector2.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Vector2
 *            提供Vector2与Vector3转换及2D射线检测工具
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 Unity 的扩展方法。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 取 <see cref="Vector2" /> 的 (x, y) 转换为 <see cref="Vector3" /> 的 (x, 0, y)。
        /// </summary>
        /// <param name="vector2">要转换的 Vector2。</param>
        /// <returns>转换后的 Vector3。</returns>
        public static Vector3 ToVector3(this Vector2 vector2)
        {
            return new Vector3(vector2.x, 0f, vector2.y);
        }

        /// <summary>
        /// 取 <see cref="Vector2" /> 的 (x, y) 和给定参数 y 转换为 <see cref="Vector3" /> 的 (x, 参数 y, y)。
        /// </summary>
        /// <param name="vector2">要转换的 Vector2。</param>
        /// <param name="y">Vector3 的 y 值。</param>
        /// <returns>转换后的 Vector3。</returns>
        public static Vector3 ToVector3(this Vector2 vector2, float y)
        {
            return new Vector3(vector2.x, y, vector2.y);
        }

        #region 2D射线检测

        /// <summary>
        /// 获取屏幕 2D 坐标射线检测到的 transform。
        /// </summary>
        /// <param name="origin">屏幕坐标点。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform，未命中返回 null。</returns>
        public static Transform GetRaycastHit2DRaycastTransform(this Vector2 origin, Camera camera)
        {
            Ray myRay = camera.ScreenPointToRay(origin);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(myRay.origin.x, myRay.origin.y), Vector2.zero);
            return hit.collider ? hit.transform : null;
        }

        /// <summary>
        /// 获取屏幕 2D 坐标射线检测到的 transform 集合。
        /// </summary>
        /// <param name="origin">屏幕坐标点。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform 数组，未命中返回空数组。</returns>
        public static Transform[] GetRaycastHit2DRaycastAllTransform(this Vector2 origin, Camera camera)
        {
            List<Transform> transforms = new List<Transform>();
            Ray myRay = camera.ScreenPointToRay(origin);
            var hits = Physics2D.RaycastAll(new Vector2(myRay.origin.x, myRay.origin.y), Vector2.zero);
            if (hits.Length > 0) hits.ToList().ForEach(h => transforms.Add(h.transform));
            return transforms.ToArray();
        }

        /// <summary>
        /// 获取屏幕两点之间的连线碰撞检测到的 transform。
        /// </summary>
        /// <param name="origin">起始点屏幕坐标。</param>
        /// <param name="direction">终点屏幕坐标。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform，未命中返回 null。</returns>
        public static Transform GetRaycastHit2DLineRaycastTransform(this Vector2 origin, Vector2 direction, Camera camera)
        {
            Ray startPoint = camera.ScreenPointToRay(origin);
            Ray endPoint = camera.ScreenPointToRay(direction);
            RaycastHit2D hit = Physics2D.Linecast(new Vector2(startPoint.origin.x, startPoint.origin.y),
                                                  new Vector2(endPoint.origin.x, endPoint.origin.y));
            return hit.collider ? hit.transform : null;
        }

        /// <summary>
        /// 获取屏幕两点之间的连线碰撞检测到的 transform 集合。
        /// </summary>
        /// <param name="origin">起始点屏幕坐标。</param>
        /// <param name="direction">终点屏幕坐标。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform 数组，未命中返回空数组。</returns>
        public static Transform[] GetRaycastHit2DLineCastAllTransform(this Vector2 origin, Vector2 direction, Camera camera)
        {
            List<Transform> transforms = new List<Transform>();
            var startPoint = camera.ScreenToWorldPoint(origin);
            var endPoint = camera.ScreenToWorldPoint(direction);
            var hits = Physics2D.LinecastAll(startPoint, endPoint);
            if (hits.Length > 0) hits.ToList().ForEach(h => transforms.Add(h.transform));
            return transforms.ToArray();
        }

        /// <summary>
        /// 获取屏幕两点之间的 2D 球形连线碰撞检测到的 transform。
        /// </summary>
        /// <param name="origin">起始点屏幕坐标。</param>
        /// <param name="radius">球形半径。</param>
        /// <param name="direction">终点屏幕坐标。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform，未命中返回 null。</returns>
        public static Transform GetRaycastHit2DCircleCastTransform(this Vector2 origin, float radius, Vector2 direction, Camera camera)
        {
            var startPoint = camera.ScreenToWorldPoint(origin);
            var endPoint = camera.ScreenToWorldPoint(direction);
            RaycastHit2D hit = Physics2D.CircleCast(startPoint, radius, (endPoint - startPoint), (endPoint - startPoint).magnitude);
            return hit.collider ? hit.transform : null;
        }

        /// <summary>
        /// 获取屏幕两点之间的 2D 球形连线碰撞检测到的 transform 集合。
        /// </summary>
        /// <param name="origin">起始点屏幕坐标。</param>
        /// <param name="radius">球形半径。</param>
        /// <param name="direction">终点屏幕坐标。</param>
        /// <param name="camera">摄像机对象。</param>
        /// <returns>检测到的 Transform 数组，未命中返回空数组。</returns>
        public static Transform[] GetRaycastHit2DCircleCastAllTransform(this Vector2 origin, float radius, Vector2 direction, Camera camera)
        {
            List<Transform> transforms = new List<Transform>();
            var startPoint = camera.ScreenToWorldPoint(origin);
            var endPoint = camera.ScreenToWorldPoint(direction);
            var hits = Physics2D.CircleCastAll(startPoint, radius, (endPoint - startPoint), (endPoint - startPoint).magnitude);
            if (hits.Length > 0) hits.ToList().ForEach(h => transforms.Add(h.transform));
            return transforms.ToArray();
        }

        #endregion
    }
}
