/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoPrefabBlockSpinner.cs
 * author:    taoye
 * created:   2026/05/25
 * descrip:   DemoPrefabBlock 自旋脚本（DemoPrefabView 私有依赖），按固定速度沿 Z 轴匀速旋转 RectTransform。
 *            供 DemoPrefabView 演示「Prefab 实例化 → 可见 → 销毁」全流程。
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 让宿主 RectTransform 沿 Z 轴匀速自旋的演示脚本。
    /// 仅依赖 Transform.Rotate，不引入任何资源/外部依赖，便于在 Demo 工程中独立运行。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DemoPrefabBlockSpinner : MonoBehaviour
    {
        /// <summary>
        /// 自旋角速度（度/秒），Inspector 可调，默认 90 度/秒。
        /// </summary>

        [SerializeField] private float m_DegreesPerSecond = 90f;

        /// <summary>
        /// 每帧按 m_DegreesPerSecond 沿 Z 轴旋转 RectTransform。
        /// </summary>
        private void Update()
        {
            transform.Rotate(0f, 0f, m_DegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
