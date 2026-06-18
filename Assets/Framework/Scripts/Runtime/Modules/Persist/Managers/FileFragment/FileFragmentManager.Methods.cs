/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFragmentManager.Methods.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   文件片段持久化管理器 —— 私有方法
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class FileFragmentManager : PersistManagerBase<FileFragmentManagerConfig>, IFileFragmentManager
    {
        /// <summary>
        /// 构造指定分类的 .dat 文件路径。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>文件绝对路径。</returns>
        private string BuildFilePath(string classify)
        {
            return Util.SysIO.Path.Combine(m_RootFolderPath, classify + c_FileExtension);
        }

        /// <summary>
        /// 校验分类名与条目名后，确保指定分类的文件已加载到内存。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        private void ValidateAndEnsure(string classify, string item)
        {
            ValidateClassifyAndItem(classify, item);
            EnsureLoaded(classify);
        }

        /// <summary>
        /// 确保指定分类的文件已加载到内存（懒加载）。
        /// 若文件不存在则创建空容器；若已加载则直接返回。
        /// </summary>
        /// <param name="classify">分类名。</param>
        private void EnsureLoaded(string classify)
        {
            if (m_LoadedFragments.Contains(classify))
            {
                return;
            }

            if (!m_ItemGroups.ContainsKey(classify))
            {
                m_ItemGroups[classify] = new FileFragmentItemGroup();
            }

            var filePath = BuildFilePath(classify);
            if (Util.SysIO.File.Exists(filePath))
            {
                bool success = m_ItemGroups[classify].Deserialize(filePath, m_UseAESEncrypt);
                if (!success)
                {
                    Log.Warning(LogTag.Persist, "FileFragment 懒加载反序列化失败，将以空数据运行：{0}", classify);
                }
            }

            m_LoadedFragments.Add(classify);
        }

        /// <summary>
        /// 将指定分类标记为脏（有未保存变更）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        private void MarkDirty(string classify)
        {
            m_DirtyFragments.Add(classify);
        }

        /// <summary>
        /// 执行待删除文件的物理删除并清空列表。
        /// </summary>
        private void ProcessPendingDeletes()
        {
            foreach (var classify in m_PendingDeletes)
            {
                var filePath = BuildFilePath(classify);
                if (Util.SysIO.File.Exists(filePath))
                {
                    Util.SysIO.File.Delete(filePath);
                }
            }

            m_PendingDeletes.Clear();
        }
    }
}
