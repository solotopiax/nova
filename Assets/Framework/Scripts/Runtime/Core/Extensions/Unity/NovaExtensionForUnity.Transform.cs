/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Transform.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Transform
 *            提供Transform组件位置、旋转、缩放以及路径获取等扩展操作
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
        /// 获取 <see cref="RectTransform"/> 组件。
        /// </summary>
        /// <param name="transform"><see cref="Transform"/> 对象。</param>
        /// <returns>RectTransform 组件，如果不存在则返回 null。</returns>
        public static RectTransform rectTransform(this Transform transform)
        {
            return transform.GetComponent<RectTransform>();
        }

        #region Position - World
        /// <summary>
        /// 设置世界坐标下 X 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 X 轴坐标值。</param>
        public static void SetPositionX(this Transform transform, float newValue)
        {
            Vector3 v = transform.position;
            v.x = newValue;
            transform.position = v;
        }

        /// <summary>
        /// 设置世界坐标下 Y 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 Y 轴坐标值。</param>
        public static void SetPositionY(this Transform transform, float newValue)
        {
            Vector3 v = transform.position;
            v.y = newValue;
            transform.position = v;
        }

        /// <summary>
        /// 设置世界坐标下 Z 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 Z 轴坐标值。</param>
        public static void SetPositionZ(this Transform transform, float newValue)
        {
            Vector3 v = transform.position;
            v.z = newValue;
            transform.position = v;
        }

        /// <summary>
        /// 增加世界坐标下 X 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 X 值。</param>
        public static void AddPositionX(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.position;
            v.x += deltaValue;
            transform.position = v;
        }

        /// <summary>
        /// 增加世界坐标下 Y 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 Y 值。</param>
        public static void AddPositionY(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.position;
            v.y += deltaValue;
            transform.position = v;
        }

        /// <summary>
        /// 增加世界坐标下 Z 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 Z 值。</param>
        public static void AddPositionZ(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.position;
            v.z += deltaValue;
            transform.position = v;
        }

        #endregion

        #region Position - Local

        /// <summary>
        /// 设置本地坐标下 X 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 X 轴本地坐标值。</param>
        public static void SetLocalPositionX(this Transform transform, float newValue)
        {
            Vector3 v = transform.localPosition;
            v.x = newValue;
            transform.localPosition = v;
        }

        /// <summary>
        /// 设置本地坐标下 Y 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 Y 轴本地坐标值。</param>
        public static void SetLocalPositionY(this Transform transform, float newValue)
        {
            Vector3 v = transform.localPosition;
            v.y = newValue;
            transform.localPosition = v;
        }

        /// <summary>
        /// 设置本地坐标下 Z 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 Z 轴本地坐标值。</param>
        public static void SetLocalPositionZ(this Transform transform, float newValue)
        {
            Vector3 v = transform.localPosition;
            v.z = newValue;
            transform.localPosition = v;
        }

        /// <summary>
        /// 增加本地坐标下 X 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 X 值。</param>
        public static void AddLocalPositionX(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.localPosition;
            v.x += deltaValue;
            transform.localPosition = v;
        }

        /// <summary>
        /// 增加本地坐标下 Y 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 Y 值。</param>
        public static void AddLocalPositionY(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.localPosition;
            v.y += deltaValue;
            transform.localPosition = v;
        }

        /// <summary>
        /// 增加本地坐标下 Z 轴位置。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 Z 值。</param>
        public static void AddLocalPositionZ(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.localPosition;
            v.z += deltaValue;
            transform.localPosition = v;
        }

        #endregion

        #region LocalScale
        /// <summary>
        /// 设置本地缩放 X 值。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 X 缩放。</param>
        public static void SetLocalScaleX(this Transform transform, float newValue)
        {
            Vector3 v = transform.localScale;
            v.x = newValue;
            transform.localScale = v;
        }

        /// <summary>
        /// 设置本地缩放 Y 值。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 Y 缩放。</param>
        public static void SetLocalScaleY(this Transform transform, float newValue)
        {
            Vector3 v = transform.localScale;
            v.y = newValue;
            transform.localScale = v;
        }

        /// <summary>
        /// 设置本地缩放 Z 值。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="newValue">新的 Z 缩放。</param>
        public static void SetLocalScaleZ(this Transform transform, float newValue)
        {
            Vector3 v = transform.localScale;
            v.z = newValue;
            transform.localScale = v;
        }

        /// <summary>
        /// 增加本地缩放 X 值。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 X 缩放量。</param>
        public static void AddLocalScaleX(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.localScale;
            v.x += deltaValue;
            transform.localScale = v;
        }

        /// <summary>
        /// 增加本地缩放 Y 值。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 Y 缩放量。</param>
        public static void AddLocalScaleY(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.localScale;
            v.y += deltaValue;
            transform.localScale = v;
        }

        /// <summary>
        /// 增加本地缩放 Z 值。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="deltaValue">增加的 Z 缩放量。</param>
        public static void AddLocalScaleZ(this Transform transform, float deltaValue)
        {
            Vector3 v = transform.localScale;
            v.z += deltaValue;
            transform.localScale = v;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// 在二维平面中让 Transform 朝向指定的目标点（世界坐标）。
        /// 使用 Vector3.up 作为参考向上方向。
        /// 适用于 2D 游戏或仅在水平面旋转的 3D 对象。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="lookAtPoint2D">目标点（二维坐标）。</param>
        public static void LookAt2D(this Transform transform, Vector2 lookAtPoint2D)
        {
            Vector3 vector = lookAtPoint2D.ToVector3() - transform.position;
            vector.y = 0f;

            if (vector.magnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(vector.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// 获取当前 Transform 的完整层级路径。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <param name="splitter">路径分隔符（默认 "/"）。</param>
        /// <returns>格式为 "Root/Parent/Child" 的路径字符串。</returns>
        public static string GetRoute(this Transform transform, string splitter = "/")
        {
            var result = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                result = $"{parent.name}{splitter}{result}";
                parent = parent.parent;
            }
            return result;
        }

        /// <summary>
        /// 获取当前 Transform 的层级深度（父节点数量）。
        /// </summary>
        /// <param name="transform">Transform 对象。</param>
        /// <returns>父级层级数量例如：顶级节点返回 0。</returns>
        public static int GetRouteNum(this Transform transform)
        {
            int result = 0;
            var parent = transform.parent;
            while (parent != null)
            {
                result++;
                parent = parent.parent;
            }
            return result;
        }

        #endregion
    }
}
