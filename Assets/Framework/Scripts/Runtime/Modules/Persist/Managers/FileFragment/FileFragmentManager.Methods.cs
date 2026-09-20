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

            var filePath = BuildFilePath(classify);
            m_ItemGroups[classify] = FileFragmentItemGroup.LoadWithRecovery(filePath, m_UseAESEncrypt);
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
        /// 执行待删除分类的正式文件、备份和临时文件清理。
        /// </summary>
        /// <returns>全部删除成功返回 true；失败分类保留在队列中等待重试。</returns>
        private bool ProcessPendingDeletes()
        {
            bool allSuccess = true;
            var deleted = new System.Collections.Generic.List<string>();
            foreach (var classify in m_PendingDeletes)
            {
                var filePath = BuildFilePath(classify);
                Util.SysIO.File.Delete(FileFragmentItemGroup.GetTemporaryPath(filePath));
                Util.SysIO.File.Delete(FileFragmentItemGroup.GetBackupPath(filePath));

                // 最后删除正式文件：若辅助文件清理失败或中途退出，至少仍保留当前有效存档。
                if (Util.SysIO.File.Exists(FileFragmentItemGroup.GetBackupPath(filePath)) ||
                    Util.SysIO.File.Exists(FileFragmentItemGroup.GetTemporaryPath(filePath)))
                {
                    allSuccess = false;
                    Log.Error(LogTag.Persist, "FileFragmentManager 删除分类文件失败，将在下次 Save 重试: {0}", classify);
                    continue;
                }

                Util.SysIO.File.Delete(filePath);
                if (Util.SysIO.File.Exists(filePath))
                {
                    allSuccess = false;
                    Log.Error(LogTag.Persist, "FileFragmentManager 删除分类文件失败，将在下次 Save 重试: {0}", classify);
                    continue;
                }

                deleted.Add(classify);
            }

            foreach (var classify in deleted)
            {
                m_PendingDeletes.Remove(classify);
            }

            return allSuccess;
        }
    }
}
