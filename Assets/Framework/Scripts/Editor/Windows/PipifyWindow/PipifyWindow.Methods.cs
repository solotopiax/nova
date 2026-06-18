/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.Methods.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 窗口总调度（OnGUI / DrawBody / DrawMainTitle / EnsureStyles）及 TopBar 辅助方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 窗口启用时：优先按当前 active scene 所属 sample 自动绑定配对的 PipifySettingsSO，
        /// 再回退到全项目扫描兜底；同时订阅场景切换监听，避免多 sample 共存时切 scene 后串味。
        /// </summary>
        private void OnEnable()
        {
            TryAutoBindSettings();
            EditorSceneManager.sceneOpened -= OnSceneOpenedRefresh;
            EditorSceneManager.sceneOpened += OnSceneOpenedRefresh;
        }

        /// <summary>
        /// 窗口关闭时注销场景切换监听，避免重复订阅。
        /// </summary>
        private void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpenedRefresh;
        }

        /// <summary>
        /// 自动绑定 PipifySettingsSO：
        /// ① 当前 active scene 若位于 Assets/Samples/... 下，优先逐级向上推断配对的 Editor/PipifySettings.asset；
        /// ② 推断失败时再全项目扫描兜底；
        /// ③ 全项目多份时保留 Warning，提示用户可在顶栏手动切换。
        /// </summary>
        private void TryAutoBindSettings()
        {
            PipifySettingsSO sceneMatched = EditorUtil.Pipify.FindSettingsForActiveScene();
            if (sceneMatched != null)
            {
                BindSettingsSilently(sceneMatched);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:" + nameof(PipifySettingsSO));
            if (guids == null || guids.Length == 0) return;
            if (guids.Length > 1)
            {
                Log.Warning(LogTag.Editor, "{0} 项目内存在 {1} 份 PipifySettingsSO 资产；当前 scene 未匹配到 sample 内配对文件，已回退绑定首个。如需切换请使用顶部「编辑器存档文件」选择框。", c_LogTag, guids.Length);
            }
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                PipifySettingsSO loaded = EditorUtil.Asset.Operator.LoadAt<PipifySettingsSO>(path);
                if (loaded == null) continue;
                BindSettingsSilently(loaded);
                return;
            }
        }

        /// <summary>
        /// scene 切换回调（仅响应 Single 模式）：
        /// 切到新 sample 后重新按 active scene 绑定对应的 PipifySettingsSO，避免窗口常开时继续指向旧 sample。
        /// </summary>
        private void OnSceneOpenedRefresh(Scene scene, OpenSceneMode mode)
        {
            if (mode != OpenSceneMode.Single) return;

            PipifySettingsSO fresh = EditorUtil.Pipify.FindSettingsForScene(scene.path);
            if (fresh == null)
                fresh = EditorUtil.Pipify.FindFirstSettings();

            if (ReferenceEquals(fresh, m_Settings)) return;
            if (!ConfirmDiscardDirty()) return;

            BindSettingsSilently(fresh);
            Repaint();
        }

        /// <summary>
        /// 直接绑定 settings，不弹确认框；供自动恢复/场景切换后的已确认分支复用。
        /// </summary>
        private void BindSettingsSilently(PipifySettingsSO settings)
        {
            m_Settings = settings;
            m_SettingsSO = settings == null ? null : new SerializedObject(settings);
            m_IsDirty = false;
            m_SelectedBatchIndex = -1;
        }

        /// <summary>
        /// 绘制窗口界面；Play Mode 下整体禁用并在顶部提示。
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();
            EditorUtil.Draw.DisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode, DrawBody);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "请退出 Play Mode 后编辑。" }, false);
            }
        }

        /// <summary>
        /// 主标题 + 顶部工具栏 + 左列表与右面板横向布局的三段式结构。
        /// </summary>
        private void DrawBody()
        {
            DrawMainTitle();
            DrawTopBar();
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                DrawLeftList();
                DrawRightPanel();
            });
        }

        /// <summary>
        /// 绘制窗口主标题行（居中加粗，上下各 8px 空行，底部细分隔线）。
        /// </summary>
        private void DrawMainTitle()
        {
            // 保险调用：防止域重载等边缘路径绕过 OnGUI 顶层 EnsureStyles 的情况。
            EnsureStyles();
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Label("Pipify 自动化管线编排中心", m_MainTitleStyle, false, GUILayout.ExpandWidth(true));
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 延迟初始化自定义 GUIStyle。每个 style 独立判空，防止域重载后 native 对象失效但 C# 引用非 null 的情况。
        /// </summary>
        private void EnsureStyles()
        {
            // 注意：域重载后 GUIStyle 的 native 层会失效，不能用单一守卫字段统一拦截，
            // 必须每个 style 独立 null 检查，保证任意入口进来都能安全重建。
            if (m_MainTitleStyle == null)
            {
                m_MainTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
        }

        /// <summary>
        /// 切换 PipifySettingsSO 绑定：有脏数据时弹确认对话框，通过后重建 SerializedObject 并重置选中状态。
        /// </summary>
        /// <param name="newSettings">要切换到的目标 PipifySettingsSO；传 null 时清空绑定。</param>
        private void RebindSettings(PipifySettingsSO newSettings)
        {
            if (!ConfirmDiscardDirty()) return;
            m_Settings = newSettings;
            m_SettingsSO = newSettings == null ? null : new SerializedObject(newSettings);
            m_IsDirty = false;
            m_SelectedBatchIndex = -1;
        }

        /// <summary>
        /// 创建按钮回调：弹 SaveFilePanelInProject 让用户选择位置，创建新的 PipifySettings.asset 并绑定。
        /// </summary>
        private void OnClickCreate()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建 PipifySettings", "PipifySettings", "asset", "选择创建位置");
            if (string.IsNullOrEmpty(path)) return;
            PipifySettingsSO created = EditorUtil.Asset.Operator.CreateAt<PipifySettingsSO>(path);
            Selection.activeObject = created;
            RebindSettings(created);
        }

        /// <summary>
        /// 保存按钮回调：将 m_Settings 的改动持久化到磁盘，并重置脏标志。
        /// </summary>
        private void OnClickSave()
        {
            if (m_Settings == null) return;
            EditorUtility.SetDirty(m_Settings);
            AssetDatabase.SaveAssets();
            m_IsDirty = false;
        }

        /// <summary>
        /// 打开文件夹按钮回调：在 OS 文件管理器中定位当前 PipifySettings.asset；未绑定时静默返回。
        /// </summary>
        private void OnClickRevealInFinder()
        {
            if (m_Settings == null) return;
            string path = AssetDatabase.GetAssetPath(m_Settings);
            if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// 检查是否有未保存的脏数据并弹确认对话框。
        /// 无脏数据时直接返回 true；有脏数据时询问用户：保存→持久化后返回 true，取消→返回 false，丢弃→回滚后返回 true。
        /// </summary>
        /// <returns>调用方可继续操作时返回 true；用户选择取消时返回 false。</returns>
        private bool ConfirmDiscardDirty()
        {
            if (m_SettingsSO == null || !m_IsDirty) return true;
            int choice = EditorUtility.DisplayDialogComplex(
                "未保存的改动",
                "当前有未保存的编辑，切换前是否保存？",
                "保存", "取消", "丢弃");
            if (choice == 0)
            {
                if (m_Settings != null) EditorUtility.SetDirty(m_Settings);
                AssetDatabase.SaveAssets();
                m_IsDirty = false;
                return true;
            }
            if (choice == 2)
            {
                m_SettingsSO?.Update();
                m_IsDirty = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 标记数据已脏（有未保存改动）并触发界面重绘。
        /// </summary>
        private void MarkDirty()
        {
            m_IsDirty = true;
            Repaint();
        }

        /// <summary>
        /// 新建 Batch 按钮回调：弹 PipifyInputDialog 获取名称，校验非空且唯一后插入列表并选中。
        /// </summary>
        private void OnClickNewBatch()
        {
            if (m_Settings == null) return;
            // 推迟到下一帧再弹模态，避免 ShowModal 的嵌套消息泵在 OnGUI 布局栈内触发 EndLayoutGroup 不对称。
            EditorApplication.delayCall += DoNewBatch;
        }

        /// <summary>
        /// 新建 Batch 的实际执行（由 EditorApplication.delayCall 调度，脱离当前 OnGUI 栈）。
        /// </summary>
        private void DoNewBatch()
        {
            if (m_Settings == null) return;
            string name = PipifyInputDialog.Show("新建 Batch", "Batch 名称", GenerateUniqueBatchName("New Batch"));
            if (name == null) return;
            name = name.Trim();
            if (string.IsNullOrEmpty(name))
            {
                EditorUtility.DisplayDialog("名称无效", "Batch 名称不能为空。", "确认");
                return;
            }
            if (IsBatchNameDuplicate(name))
            {
                EditorUtility.DisplayDialog("名称重复", $"已存在名为 \"{name}\" 的 Batch，请使用其他名称。", "确认");
                return;
            }
            Batch newBatch = new Batch { Name = name };
            m_Settings.Batches.Add(newBatch);
            m_SelectedBatchIndex = m_Settings.Batches.Count - 1;
            MarkDirty();
            Repaint();
        }

        /// <summary>
        /// 重命名指定索引的 Batch：弹 PipifyInputDialog 获取新名称，校验非空且唯一后写入。
        /// </summary>
        /// <param name="index">要重命名的 Batch 索引。</param>
        private void OnRenameBatch(int index)
        {
            if (m_Settings == null || index < 0 || index >= m_Settings.Batches.Count) return;
            // 推迟到下一帧再弹模态，避免 ShowModal 的嵌套消息泵在 OnGUI 布局栈内触发 EndLayoutGroup 不对称。
            int captured = index;
            EditorApplication.delayCall += () => DoRenameBatch(captured);
        }

        /// <summary>
        /// 重命名 Batch 的实际执行（由 EditorApplication.delayCall 调度）。
        /// </summary>
        /// <param name="index">要重命名的 Batch 索引。</param>
        private void DoRenameBatch(int index)
        {
            if (m_Settings == null || index < 0 || index >= m_Settings.Batches.Count) return;
            Batch batch = m_Settings.Batches[index];
            string newName = PipifyInputDialog.Show("重命名 Batch", "新名称", batch.Name);
            if (newName == null) return;
            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("名称无效", "Batch 名称不能为空。", "确认");
                return;
            }
            if (IsBatchNameDuplicate(newName, index))
            {
                EditorUtility.DisplayDialog("名称重复", $"已存在名为 \"{newName}\" 的 Batch，请使用其他名称。", "确认");
                return;
            }
            batch.Name = newName;
            MarkDirty();
            Repaint();
        }

        /// <summary>
        /// 复制指定索引的 Batch：通过 JSON 往返深拷贝，追加 "(Copy)" 后缀直到名称唯一，插入列表末尾。
        /// </summary>
        /// <param name="index">要复制的 Batch 索引。</param>
        private void OnDuplicateBatch(int index)
        {
            if (m_Settings == null || index < 0 || index >= m_Settings.Batches.Count) return;
            Batch original = m_Settings.Batches[index];
            // 通过 JSON 往返实现深拷贝，保持所有子字段独立
            string json = Util.Json.Serialize(original);
            Batch copy = Util.Json.Deserialize<Batch>(json);
            copy.Name = GenerateUniqueBatchName(original.Name + " (Copy)");
            m_Settings.Batches.Add(copy);
            m_SelectedBatchIndex = m_Settings.Batches.Count - 1;
            MarkDirty();
        }

        /// <summary>
        /// 删除指定索引的 Batch：弹二次确认对话框后删除，并调整选中索引。
        /// </summary>
        /// <param name="index">要删除的 Batch 索引。</param>
        private void OnDeleteBatch(int index)
        {
            if (m_Settings == null || index < 0 || index >= m_Settings.Batches.Count) return;
            // 推迟到下一帧，避免 DisplayDialog 的嵌套消息泵可能触及 OnGUI 布局栈。
            int captured = index;
            EditorApplication.delayCall += () => DoDeleteBatch(captured);
        }

        /// <summary>
        /// 删除 Batch 的实际执行（由 EditorApplication.delayCall 调度）。
        /// </summary>
        /// <param name="index">要删除的 Batch 索引。</param>
        private void DoDeleteBatch(int index)
        {
            if (m_Settings == null || index < 0 || index >= m_Settings.Batches.Count) return;
            string batchName = m_Settings.Batches[index].Name;
            bool confirmed = EditorUtility.DisplayDialog(
                "删除 Batch",
                $"确定要删除 Batch \"{batchName}\" 吗？此操作不可撤销。",
                "删除", "取消");
            if (!confirmed) return;
            m_Settings.Batches.RemoveAt(index);
            if (m_SelectedBatchIndex == index)
            {
                m_SelectedBatchIndex = -1;
            }
            else if (m_SelectedBatchIndex > index)
            {
                m_SelectedBatchIndex--;
            }
            MarkDirty();
            Repaint();
        }

        /// <summary>
        /// 弹出指定 Batch 行的右键上下文菜单（重命名 / 复制 / 清除）。
        /// 调用方须在调用前捕获 index 局部变量，避免 GenericMenu 回调的 lambda 闭包陷阱。
        /// </summary>
        /// <param name="index">目标 Batch 的当前索引。</param>
        private void ShowBatchContextMenu(int index)
        {
            GenericMenu menu = new GenericMenu();
            // 必须用局部变量捕获，防止 GenericMenu 延迟回调时 index 已变化
            int capturedIndex = index;
            menu.AddItem(new GUIContent("重命名"), false, () => OnRenameBatch(capturedIndex));
            menu.AddItem(new GUIContent("复制"), false, () => OnDuplicateBatch(capturedIndex));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("删除"), false, () => OnDeleteBatch(capturedIndex));
            menu.ShowAsContext();
        }

        /// <summary>
        /// 检查指定名称在当前 Batches 中是否已存在（可排除指定索引，用于重命名场景）。
        /// </summary>
        /// <param name="name">待检查的名称。</param>
        /// <param name="excludeIndex">排除的索引（重命名时传入当前项索引，-1 表示不排除）。</param>
        /// <returns>名称已被其他 Batch 使用时返回 true。</returns>
        private bool IsBatchNameDuplicate(string name, int excludeIndex = -1)
        {
            if (m_Settings == null) return false;
            for (int i = 0; i < m_Settings.Batches.Count; i++)
            {
                if (i == excludeIndex) continue;
                if (m_Settings.Batches[i].Name == name) return true;
            }
            return false;
        }

        /// <summary>
        /// 根据基础名生成在当前 Batches 中唯一的名称；若基础名已唯一则直接返回，否则追加 " 2"、" 3" … 直到唯一。
        /// </summary>
        /// <param name="baseName">基础名称。</param>
        /// <returns>唯一的 Batch 名称。</returns>
        private string GenerateUniqueBatchName(string baseName)
        {
            if (!IsBatchNameDuplicate(baseName)) return baseName;
            int suffix = 2;
            while (true)
            {
                string candidate = $"{baseName} {suffix}";
                if (!IsBatchNameDuplicate(candidate)) return candidate;
                suffix++;
            }
        }

        /// <summary>
        /// 确保 m_ItemsList 与当前选中 Batch 对应；若 Batch 切换则重建 ReorderableList 并清除展开态和参数缓存。
        /// </summary>
        private void EnsureItemsListForSelectedBatch()
        {
            if (m_SettingsSO == null || m_SelectedBatchIndex < 0) return;
            if (m_ItemsList != null && m_ItemsListBoundBatchIndex == m_SelectedBatchIndex) return;

            // Batch 已切换，重置展开态与参数缓存
            m_ExpandedItemIndices.Clear();
            m_ParamsCache.Clear();
            m_ItemsListBoundBatchIndex = m_SelectedBatchIndex;

            SerializedProperty batchesProp = m_SettingsSO.FindProperty("m_Batches");
            if (batchesProp == null || m_SelectedBatchIndex >= batchesProp.arraySize)
            {
                m_ItemsList = null;
                return;
            }
            SerializedProperty itemsProp = batchesProp.GetArrayElementAtIndex(m_SelectedBatchIndex).FindPropertyRelative("m_Items");
            if (itemsProp == null)
            {
                m_ItemsList = null;
                return;
            }

            m_ItemsList = new ReorderableList(m_SettingsSO, itemsProp, true, true, true, false);

            m_ItemsList.drawHeaderCallback = (Rect r) =>
            {
                int count = m_Settings != null && m_SelectedBatchIndex < m_Settings.Batches.Count
                    ? m_Settings.Batches[m_SelectedBatchIndex].Items.Count
                    : 0;
                GUI.Label(r, $"Steps ({count})");
            };

            m_ItemsList.drawElementCallback = (Rect r, int i, bool active, bool focused) =>
            {
                DrawItemElement(r, i, active, focused);
            };

            m_ItemsList.elementHeightCallback = (int i) =>
            {
                if (m_Settings == null || m_SelectedBatchIndex < 0 || m_SelectedBatchIndex >= m_Settings.Batches.Count) return c_ItemRowHeight;
                Batch b = m_Settings.Batches[m_SelectedBatchIndex];
                if (i < 0 || i >= b.Items.Count) return c_ItemRowHeight;
                if (!m_ExpandedItemIndices.Contains(i)) return c_ItemRowHeight + 4f;
                return c_ItemRowHeight + 4f + EstimateParamsHeight(b, i);
            };

            m_ItemsList.onReorderCallback = (ReorderableList list) =>
            {
                // 拖拽排序后展开态索引可能错位，直接清空最安全
                m_ExpandedItemIndices.Clear();
                m_ParamsCache.Clear();
                MarkDirty();
            };

            m_ItemsList.onAddDropdownCallback = (Rect buttonRect, ReorderableList list) =>
            {
                GenericMenu menu = new GenericMenu();
                foreach (IGrouping<string, PipifyStepInfo> group in EditorUtil.Pipify.Registry.GroupByCategory())
                {
                    string category = group.Key;
                    foreach (PipifyStepInfo info in group)
                    {
                        PipifyStepInfo capturedInfo = info;
                        menu.AddItem(new GUIContent($"{category}/{info.DisplayName}"), false, () => AddBatchItemFromStep(capturedInfo));
                    }
                }
                if (EditorUtil.Pipify.Registry.GetAll().Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("（暂无注册 Step）"));
                }
                menu.ShowAsContext();
            };

            // 禁用内置删除按钮（我们在行内手动绘制）
            m_ItemsList.onRemoveCallback = null;
        }

        /// <summary>
        /// 使指定 Item 索引的参数缓存失效；索引为 -1 时清空全部缓存。
        /// </summary>
        /// <param name="itemIndex">要失效的 Item 索引；传 -1 清空全部。</param>
        private void InvalidateParamsCache(int itemIndex)
        {
            if (itemIndex < 0)
            {
                m_ParamsCache.Clear();
                return;
            }
            m_ParamsCache.Remove(itemIndex);
        }

        /// <summary>
        /// 向当前选中 Batch 追加新 BatchItem，并以参数类默认实例序列化为初始 JSON。
        /// </summary>
        /// <param name="info">要添加的 Step 元信息。</param>
        private void AddBatchItemFromStep(PipifyStepInfo info)
        {
            if (m_Settings == null || m_SelectedBatchIndex < 0 || m_SelectedBatchIndex >= m_Settings.Batches.Count) return;
            Batch batch = m_Settings.Batches[m_SelectedBatchIndex];
            BatchItem newItem = new BatchItem { StepId = info.Id };
            if (info.ParamsType != null)
            {
                object defaultParams = System.Activator.CreateInstance(info.ParamsType);
                newItem.ParamsJson = Util.Json.Serialize(defaultParams);
            }
            batch.Items.Add(newItem);
            // 重建 list 以同步新条目
            m_ItemsListBoundBatchIndex = -1;
            EnsureItemsListForSelectedBatch();
            MarkDirty();
        }

        /// <summary>
        /// 估算指定 Item 展开参数区所需的高度（像素）。
        /// </summary>
        /// <param name="batch">所属 Batch。</param>
        /// <param name="itemIndex">Item 索引。</param>
        /// <returns>参数区高度（像素）；无参 Step 返回 0。</returns>
        private float EstimateParamsHeight(Batch batch, int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= batch.Items.Count) return 0f;
            BatchItem item = batch.Items[itemIndex];
            PipifyStepInfo info = EditorUtil.Pipify.Registry.FindById(item.StepId);
            if (info == null || info.ParamsType == null) return 0f;
            int visibleCount = CountVisibleFields(itemIndex, item, info);
            return visibleCount * (c_ParamFieldHeight + 2f) + 4f;
        }

        /// <summary>
        /// 计算指定 Item 当前可见参数字段数（按 PipifyVisibleWhen 过滤）。
        /// </summary>
        /// <param name="itemIndex">Item 索引。</param>
        /// <param name="item">BatchItem 数据。</param>
        /// <param name="info">Step 元信息。</param>
        /// <returns>可见字段数。</returns>
        private int CountVisibleFields(int itemIndex, BatchItem item, PipifyStepInfo info)
        {
            FieldInfo[] fields = info.ParamsType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            object paramsInstance = EnsureParamsInstance(itemIndex, item, info);
            if (paramsInstance == null) return fields.Length;
            int visible = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (IsFieldVisible(fields[i], paramsInstance, info.ParamsType)) visible++;
            }
            return visible;
        }

        /// <summary>
        /// 根据 BatchItem.ParamsJson 与参数类型构建参数对象实例。
        /// JSON 为空时返回默认新实例；反序列化失败时 Log.Warning 并返回默认新实例。
        /// </summary>
        /// <param name="item">BatchItem 数据。</param>
        /// <param name="paramsType">参数类型。</param>
        /// <returns>参数对象实例；paramsType 为 null 时返回 null，否则不为 null。</returns>
        private static object BuildParamsInstance(BatchItem item, Type paramsType)
        {
            if (paramsType == null) return null;
            if (string.IsNullOrWhiteSpace(item.ParamsJson))
            {
                return System.Activator.CreateInstance(paramsType);
            }
            try
            {
                object result = Util.Json.Deserialize(item.ParamsJson, paramsType);
                return result ?? System.Activator.CreateInstance(paramsType);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Editor, "[PipifyWindow] 参数反序列化失败（{0}），使用默认值。异常：{1}", paramsType.Name, ex.Message);
                return System.Activator.CreateInstance(paramsType);
            }
        }
    }
}
