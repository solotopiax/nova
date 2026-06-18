/*********************************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Quaternion.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Quaternion
 *            提供额外Quaternion操作的实用类，包括缩放、安全归一化、混合和加法等
 ********************************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Unity Quaternion 的扩展方法集合。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 浮点运算的最小阈值，用于安全归一化以避免除零错误。
        /// </summary>
        private const float c_FloatMin = 1e-10f;

        /// <summary>
        /// 表示四元数的零值（0, 0, 0, 0）。
        /// </summary>
        public static readonly Quaternion QuaternionZero = new Quaternion(0f, 0f, 0f, 0f);

        /// <summary>
        /// 缩放四元数的各分量。
        /// </summary>
        /// <param name="q">待缩放的四元数。</param>
        /// <param name="scale">缩放因子。</param>
        /// <returns>缩放后的四元数。</returns>
        public static Quaternion Scale(this Quaternion q, float scale)
        {
            return new Quaternion(q.x * scale, q.y * scale, q.z * scale, q.w * scale);
        }

        /// <summary>
        /// 安全归一化四元数，如果长度足够大则归一化，否则返回单位四元数。
        /// </summary>
        /// <param name="q">待归一化的四元数。</param>
        /// <returns>归一化后的四元数或单位四元数。</returns>
        public static Quaternion NormalizeSafe(this Quaternion q)
        {
            float dot = Quaternion.Dot(q, q);
            if (dot > c_FloatMin)
            {
                float rsqrt = 1.0f / Mathf.Sqrt(dot);
                return new Quaternion(q.x * rsqrt, q.y * rsqrt, q.z * rsqrt, q.w * rsqrt);
            }

            return Quaternion.identity;
        }

        /// <summary>
        /// 在两个四元数之间进行线性混合。
        /// </summary>
        /// <param name="q1">第一个四元数。</param>
        /// <param name="q2">第二个四元数。</param>
        /// <param name="weight">第二个四元数的混合权重。</param>
        /// <returns>混合后的四元数。</returns>
        public static Quaternion Blend(this Quaternion q1, Quaternion q2, float weight)
        {
            return q1.Add(q2.Scale(weight));
        }

        /// <summary>
        /// 对两个四元数进行加法运算，考虑方向一致性。
        /// </summary>
        /// <param name="rhs">第一个四元数。</param>
        /// <param name="lhs">第二个四元数。</param>
        /// <returns>加法后的四元数。</returns>
        public static Quaternion Add(this Quaternion rhs, Quaternion lhs)
        {
            float sign = Mathf.Sign(Quaternion.Dot(rhs, lhs)); // 确保加法方向一致。
            return new Quaternion(rhs.x + sign * lhs.x, rhs.y + sign * lhs.y, rhs.z + sign * lhs.z, rhs.w + sign * lhs.w);
        }
    }
}
