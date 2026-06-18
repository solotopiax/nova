/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabManager.Methods.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   PrefabManager 私有 helper
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class PrefabManager : PrefabManagerBase
    {
        /// <summary>
        /// 向 GameObject 挂载 PrefabInstanceTag，注入 Handle 与回调，写入记录字典与诊断列表。
        /// </summary>
        /// <param name="go">已实例化的 GameObject。</param>
        /// <param name="handle">本次实例化申请的资源句柄。</param>
        /// <param name="location">Prefab location 字符串，用于诊断展示。</param>
        private void RecordInstance(GameObject go, IAssetHandle<GameObject> handle, string location)
        {
            PrefabInstanceTag tag = go.AddComponent<PrefabInstanceTag>();
            tag.Handle = handle;
            tag.OnDestroyed = OnInstanceDestroyed;
            m_InstanceToHandle[go] = handle;
            m_RecordedInstances.Add(new PrefabRecordedInstance(go, location));
        }

        /// <summary>
        /// PrefabInstanceTag.OnDestroy 触发的单路径回调，释放对应句柄并从记录字典与诊断列表移除。
        /// 字典找不到 key（Shutdown 已先行清理或极端竞态）时直接幂等返回。
        /// </summary>
        /// <param name="tag">触发 OnDestroy 的 PrefabInstanceTag 组件。</param>
        private void OnInstanceDestroyed(PrefabInstanceTag tag)
        {
            if (!m_InstanceToHandle.TryGetValue(tag.gameObject, out IAssetHandle<GameObject> handle))
            {
                return;
            }
            m_InstanceToHandle.Remove(tag.gameObject);
            handle?.Release();
            for (int i = m_RecordedInstances.Count - 1; i >= 0; i--)
            {
                if (m_RecordedInstances[i].Instance == tag.gameObject)
                {
                    m_RecordedInstances.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
