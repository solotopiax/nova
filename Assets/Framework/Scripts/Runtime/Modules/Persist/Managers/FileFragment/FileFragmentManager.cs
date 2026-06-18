/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFragmentManager.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   文件片段持久化管理器
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文件片段持久化管理器。
    /// 每个 classify 对应一个 .dat 文件（JSON 格式，可选 AES 加密）。
    /// IO 优化：初始化时全量加载 + 脏追踪（只序列化变更的片段）。
    /// </summary>
    internal sealed partial class FileFragmentManager : PersistManagerBase<FileFragmentManagerConfig>, IFileFragmentManager
    {
        /// <summary>
        /// 初始化 FileFragmentManager 的新实例。
        /// </summary>
        public FileFragmentManager() { }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override async UniTask Initialize(FileFragmentManagerConfig config)
        {
            InitializeBase(config);
            m_RootFolderPath = Path.Persist.FileFragment.FolderFullPath;
            await Load();
        }

        /// <summary>
        /// 管理器轮询：驱动自动保存计时器。
        /// </summary>
        public override void Update()
        {
            TickAutoSave(UnityEngine.Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 关闭并清理管理器：强制保存所有脏片段并删除待删文件。
        /// </summary>
        public override void Shutdown()
        {
            try
            {
                Save();
            }
            catch (System.Exception ex)
            {
                Log.Error(LogTag.Persist, "FileFragmentManager.Shutdown Save failed: {0}", ex.Message);
            }
            finally
            {
                m_ItemGroups.Clear();
                m_LoadedFragments.Clear();
                m_DirtyFragments.Clear();
                m_PendingDeletes.Clear();
            }
        }

        /// <summary>
        /// 扫描文件目录并将所有存档文件并行反序列化到内存。
        /// 重入保护：若上一次 Load 尚未完成则立即返回 false。
        /// </summary>
        /// <returns>成功返回 true，重入时返回 false。</returns>
        public override async UniTask<bool> Load()
        {
            if (m_IsLoading)
            {
                return false;
            }

            m_IsLoading = true;
            try
            {
                return await LoadInternal();
            }
            finally
            {
                m_IsLoading = false;
            }
        }

        /// <summary>
        /// 实际加载逻辑：扫描文件目录并将所有存档文件并行反序列化到内存。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        private async UniTask<bool> LoadInternal()
        {
            Util.SysIO.Directory.CreateIfNotExist(m_RootFolderPath);

            var files = Util.SysIO.Directory.GetFiles(m_RootFolderPath, "*" + c_FileExtension);
            var toLoad = files.Where(f =>
            {
                var classify = Util.SysIO.File.GetName(f, includeFileExtension: false);
                return !m_LoadedFragments.Contains(classify);
            }).ToArray();

            if (toLoad.Length == 0)
            {
                return true;
            }

            var tasks = toLoad.Select(file => UniTask.RunOnThreadPool(() =>
            {
                var classify = Util.SysIO.File.GetName(file, includeFileExtension: false);
                var group = new FileFragmentItemGroup();
                var success = group.Deserialize(file, m_UseAESEncrypt);
                return (classify, group, success);
            }));

            var results = await UniTask.WhenAll(tasks);

            foreach (var (classify, group, success) in results)
            {
                if (success && !m_LoadedFragments.Contains(classify))
                {
                    m_ItemGroups[classify] = group;
                    m_LoadedFragments.Add(classify);
                }
            }

            return true;
        }

        /// <summary>
        /// 将所有脏片段序列化到文件，并执行待删除文件的物理删除。
        /// 逐个 try-catch：成功的才从脏集合移除，失败的保留。
        /// </summary>
        /// <returns>全部成功返回 true，任一失败返回 false。</returns>
        public override bool Save()
        {
            ProcessPendingDeletes();

            bool allSuccess = true;
            var saved = new List<string>();

            foreach (var classify in m_DirtyFragments)
            {
                if (!m_ItemGroups.TryGetValue(classify, out var group))
                {
                    continue;
                }

                try
                {
                    group.Serialize(BuildFilePath(classify), m_UseAESEncrypt);
                    saved.Add(classify);
                }
                catch (System.Exception ex)
                {
                    Log.Error(LogTag.Persist, "FileFragmentManager.Save [{0}] failed: {1}", classify, ex.Message);
                    allSuccess = false;
                }
            }

            foreach (var classify in saved)
            {
                m_DirtyFragments.Remove(classify);
            }

            return allSuccess;
        }

        /// <summary>
        /// 将指定分类（文件片段）序列化到文件。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>成功返回 true。</returns>
        public override bool Save(string classify)
        {
            ValidateClassify(classify);

            if (!m_ItemGroups.TryGetValue(classify, out var group))
            {
                return false;
            }

            group.Serialize(BuildFilePath(classify), m_UseAESEncrypt);
            m_DirtyFragments.Remove(classify);
            return true;
        }

        /// <summary>
        /// 判断指定条目是否存在（首次访问时懒加载文件）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>存在返回 true。</returns>
        public override bool HasItem(string classify, string item)
        {
            ValidateAndEnsure(classify, item);
            return m_ItemGroups.TryGetValue(classify, out var group) && group.HasItem(item);
        }

        /// <summary>
        /// 删除指定条目（首次访问时懒加载文件）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>删除成功返回 true。</returns>
        public override bool RemoveItem(string classify, string item)
        {
            ValidateAndEnsure(classify, item);
            if (!m_ItemGroups.TryGetValue(classify, out var group))
            {
                return false;
            }

            var removed = group.RemoveItem(item);
            if (removed)
            {
                MarkDirty(classify);
                if (group.Count == 0)
                {
                    m_ItemGroups.Remove(classify);
                    m_LoadedFragments.Remove(classify);
                    m_DirtyFragments.Remove(classify);
                    m_PendingDeletes.Add(classify);
                }
            }

            return removed;
        }

        /// <summary>
        /// 删除指定分类下的全部条目，并标记该文件片段为待删除。
        /// </summary>
        /// <param name="classify">分类名。</param>
        public override void RemoveAll(string classify)
        {
            ValidateClassify(classify);
            m_ItemGroups.Remove(classify);
            m_LoadedFragments.Remove(classify);
            m_DirtyFragments.Remove(classify);
            m_PendingDeletes.Add(classify);
        }

        /// <summary>
        /// 获取指定分类下的全部条目名数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目名数组，不存在时返回空数组。</returns>
        public override string[] GetAllItemNames(string classify)
        {
            ValidateClassify(classify);
            EnsureLoaded(classify);
            return m_ItemGroups.TryGetValue(classify, out var group) ? group.GetAllItemNames() : System.Array.Empty<string>();
        }

        /// <summary>
        /// 将指定分类下的全部条目名填充到列表。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="results">结果列表，方法会追加而非清空。</param>
        public override void GetAllItemNames(string classify, List<string> results)
        {
            ValidateClassify(classify);
            EnsureLoaded(classify);
            m_ItemGroups.GetValueOrDefault(classify)?.GetAllItemNames(results);
        }

        /// <summary>
        /// 获取指定分类下的条目数量。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目数量。</returns>
        public override int Count(string classify)
        {
            ValidateClassify(classify);
            EnsureLoaded(classify);
            return m_ItemGroups.TryGetValue(classify, out var group) ? group.Count : 0;
        }

        /// <summary>
        /// 获取所有已注册分类（文件片段）的名称。
        /// </summary>
        /// <returns>分类名数组。</returns>
        public override string[] GetAllClassifyNames()
        {
            var keys = m_ItemGroups.Keys;
            var result = new string[keys.Count];
            keys.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// 读取布尔值（首次访问时懒加载文件）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override bool GetBool(string classify, string item, bool defaultValue = default)
        {
            ValidateAndEnsure(classify, item);
            return m_ItemGroups.TryGetValue(classify, out var group) ? group.GetBool(item, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 读取整型值（首次访问时懒加载文件）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override int GetInt(string classify, string item, int defaultValue = default)
        {
            ValidateAndEnsure(classify, item);
            return m_ItemGroups.TryGetValue(classify, out var group) ? group.GetInt(item, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 读取浮点值（首次访问时懒加载文件）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override float GetFloat(string classify, string item, float defaultValue = default)
        {
            ValidateAndEnsure(classify, item);
            return m_ItemGroups.TryGetValue(classify, out var group) ? group.GetFloat(item, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 读取字符串值（首次访问时懒加载文件）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override string GetString(string classify, string item, string defaultValue = "")
        {
            ValidateAndEnsure(classify, item);
            return m_ItemGroups.TryGetValue(classify, out var group) ? group.GetString(item, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 写入布尔值（首次访问时懒加载文件，标记片段为脏）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetBool(string classify, string item, bool value)
        {
            ValidateAndEnsure(classify, item);
            m_ItemGroups[classify].SetBool(item, value);
            MarkDirty(classify);
        }

        /// <summary>
        /// 写入整型值（首次访问时懒加载文件，标记片段为脏）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetInt(string classify, string item, int value)
        {
            ValidateAndEnsure(classify, item);
            m_ItemGroups[classify].SetInt(item, value);
            MarkDirty(classify);
        }

        /// <summary>
        /// 写入浮点值（首次访问时懒加载文件，标记片段为脏）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetFloat(string classify, string item, float value)
        {
            ValidateAndEnsure(classify, item);
            m_ItemGroups[classify].SetFloat(item, value);
            MarkDirty(classify);
        }

        /// <summary>
        /// 写入字符串值（首次访问时懒加载文件，标记片段为脏）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetString(string classify, string item, string value)
        {
            ValidateAndEnsure(classify, item);
            m_ItemGroups[classify].SetString(item, value);
            MarkDirty(classify);
        }
    }
}
