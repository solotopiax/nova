/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TagFilterPanelControl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 树形 LogTag 筛选面板，动态从 LogTag 常量反射构建标签列表，每秒刷新各 Tag 的日志数量。
    /// 所有节点共用同一个 TagLeafRowPrefab，无需 TagRowPrefab。
    /// Toggle 操作为临时状态，点击确定后才向外提交；点击取消则还原到打开面板时的快照。
    /// </summary>
    public class TagFilterPanelControl : DebugMonoBehaviourEx
    {
        /// <summary>
        /// 匹配 [Tag] 片段的正则表达式，Compiled 模式全局复用。
        /// </summary>
        private static readonly Regex s_TagRegex = new Regex(@"\[[^\]]+\]", RegexOptions.Compiled);

        /// <summary>
        /// 标签行容器，ScrollView 的 Content RectTransform。
        /// </summary>
        [RequiredField] public RectTransform ItemContainer;

        /// <summary>
        /// 行 Prefab（含 Spacer + Toggle + Label），父子节点共用，通过 Spacer 宽度区分层级。
        /// </summary>
        [RequiredField] public GameObject TagLeafRowPrefab;

        /// <summary>
        /// 确定按钮，点击后将 pending 集合提交并通知外部关闭面板。
        /// </summary>
        [RequiredField] public GameObject ConfirmButton;

        /// <summary>
        /// 全选/全取消 Toggle，isOn=true 全选所有可见叶子，isOn=false 全部取消；随 pending 状态自动同步。
        /// </summary>
        [RequiredField] public Toggle SelectAllToggle;

        /// <summary>
        /// 取消按钮，点击后丢弃 pending，还原 Toggle UI 到打开时快照，不触发过滤变更。
        /// </summary>
        [RequiredField] public GameObject CancelButton;

        /// <summary>
        /// 搜索框，用于在当前显示的标签中按标签名过滤可见行。
        /// </summary>
        public InputField SearchInput;

        /// <summary>
        /// 左侧分类行 Prefab，挂载 TagCategoryRowItem 组件。
        /// 为 null 时跳过左侧 Tab 初始化（向后兼容旧预制体）。
        /// </summary>
        public GameObject CategoryRowPrefab;

        /// <summary>
        /// 左侧分类列表容器，ScrollView 的 Content RectTransform。
        /// </summary>
        public RectTransform CategoryContainer;

        /// <summary>
        /// 已选中标签数量 / 总数 Label，显示如"已选择 16/16"。
        /// 为 null 时不更新。
        /// </summary>
        public Text SelectedCountLabel;

        /// <summary>
        /// 点击确定后触发，参数为提交后的完整叶子标签集合（空集合表示不过滤）。
        /// </summary>
        public Action<IReadOnlyCollection<string>> OnTagFilterChanged;

        /// <summary>
        /// 点击确定按钮时触发，由外部（ConsoleTabController）处理面板关闭与 Toggle 状态同步。
        /// </summary>
        public Action OnConfirmClicked;

        /// <summary>
        /// 点击取消/关闭按钮时触发，由外部（ConsoleTabController）处理面板关闭与 Toggle 状态同步。
        /// </summary>
        public Action OnCloseClicked;

        /// <summary>
        /// 面板内部工作集：记录本次打开后用户的临时勾选状态，确定前不向外提交。
        /// </summary>
        private readonly HashSet<string> m_PendingTags = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 打开面板时从外部传入的已提交集合快照，取消时用于还原 Toggle UI。
        /// </summary>
        private readonly HashSet<string> m_SnapshotTags = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 是否已完成初始化，防止重复调用 Initialize。
        /// </summary>
        private bool m_Initialized;

        /// <summary>
        /// 树形标签节点，保存节点层级、Toggle 引用及子节点有序字典。
        /// </summary>
        private class TagNode
        {
            /// <summary>
            /// 完整路径标签（如 "[SDK][AppsFlyerPlugin]"）。
            /// </summary>
            public string Label;

            /// <summary>
            /// 标签描述文字，从 LogTagDescriptionAttribute 读取。
            /// </summary>
            public string Description = string.Empty;

            /// <summary>
            /// 在树中的深度，根节点为 0。
            /// </summary>
            public int Depth;

            /// <summary>
            /// 有序子节点，key 为完整路径。
            /// </summary>
            public SortedDictionary<string, TagNode> Children = new SortedDictionary<string, TagNode>(StringComparer.Ordinal);

            /// <summary>
            /// 是否为叶子节点（无子节点）。
            /// </summary>
            public bool IsLeaf => Children.Count == 0;

            /// <summary>
            /// 对应的 Toggle 组件引用，RenderTree 后赋值。
            /// </summary>
            public Toggle Toggle;

            /// <summary>
            /// 对应的行 UI 组件引用，RenderTree 后赋值，用于定时更新日志数量显示。
            /// </summary>
            public TagRowItem RowItem;
        }

        /// <summary>
        /// 根节点有序字典，key 为完整路径。
        /// </summary>
        private readonly SortedDictionary<string, TagNode> m_RootNodes = new SortedDictionary<string, TagNode>(StringComparer.Ordinal);

        /// <summary>
        /// 所有叶子节点总数，BuildTree 完成后计算，用于 SelectedCountLabel。
        /// </summary>
        private int m_TotalLeafCount;

        /// <summary>
        /// 定时刷新计数的累计时间，每超过 1 秒触发一次 RefreshCounts。
        /// </summary>
        private float m_CountUpdateTimer;

        /// <summary>
        /// 左侧分类行列表，用于单选互斥逻辑，与 m_CategoryNodes 下标一一对应。
        /// </summary>
        private readonly List<TagCategoryRowItem> m_CategoryRows = new List<TagCategoryRowItem>();

        /// <summary>
        /// 与 m_CategoryRows 下标对应的根节点，null 表示"全部标签"（不过滤）。
        /// </summary>
        private readonly List<TagNode> m_CategoryNodes = new List<TagNode>();

        /// <summary>
        /// 当前选中的分类根节点，null 表示"全部标签"；切换分类时更新，供搜索过滤使用。
        /// </summary>
        private TagNode m_CurrentFilterNode;

        /// <summary>
        /// 当前搜索关键词，空字符串表示不过滤。
        /// </summary>
        private string m_SearchKeyword = string.Empty;

        /// <summary>
        /// 由外部（ConsoleTabController）在 Start 阶段主动调用，完成树构建与 UI 渲染。
        /// 不依赖 GameObject 激活状态，可在 SetActive(false) 时安全调用。
        /// </summary>
        public void Initialize()
        {
            if (m_Initialized)
                return;
            m_Initialized = true;
            BuildTree();
            m_TotalLeafCount = CountLeaves(m_RootNodes);
            RenderTree(m_RootNodes, ItemContainer);
            InitCategoryTabs();
            var confirmBtn = ConfirmButton.GetComponent<Button>();
            if (confirmBtn != null)
                confirmBtn.onClick.AddListener(OnConfirm);
            if (SelectAllToggle != null)
                SelectAllToggle.onValueChanged.AddListener(OnSelectAllToggleChanged);
            var cancelBtn = CancelButton.GetComponent<Button>();
            if (cancelBtn != null)
                cancelBtn.onClick.AddListener(OnClose);
            if (SearchInput != null)
                SearchInput.onValueChanged.AddListener(OnSearchChanged);
            RefreshSelectedCountLabel();
        }

        /// <summary>
        /// 打开面板：拍下当前已提交集合的快照，用快照初始化 pending 并同步 Toggle UI，再激活 GameObject。
        /// 由外部（ConsoleTabController.FilterToggleValueChanged）在显示面板前调用，替代直接 SetActive(true)。
        /// </summary>
        /// <param name="committed">当前已提交的 Tag 集合（空或 null 表示不过滤）。</param>
        public void Open(IReadOnlyCollection<string> committed)
        {
            m_SnapshotTags.Clear();
            m_PendingTags.Clear();

            if (committed != null)
            {
                foreach (var tag in committed)
                {
                    m_SnapshotTags.Add(tag);
                    m_PendingTags.Add(tag);
                }
            }

            SyncTogglesToPending(m_RootNodes);
            SyncSelectAllToggle();
            m_SearchKeyword = string.Empty;
            if (SearchInput != null)
                SearchInput.SetTextWithoutNotify(string.Empty);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// MonoBehaviour Start 生命周期，保留以确保父类初始化逻辑正常执行。
        /// </summary>
        protected override void Start()
        {
            base.Start();
        }

        /// <summary>
        /// 每帧累计时间，超过 1 秒时刷新所有行的日志数量显示。
        /// </summary>
        protected override void Update()
        {
            base.Update();
            if (!m_Initialized)
                return;
            m_CountUpdateTimer += Time.unscaledDeltaTime;
            if (m_CountUpdateTimer >= 1f)
            {
                m_CountUpdateTimer = 0f;
                RefreshCounts();
            }
        }

        /// <summary>
        /// 面板激活时强制重建布局，确保 ContentSizeFitter 在 SetActive(true) 后正确计算内容高度。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_Initialized && ItemContainer != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(ItemContainer);
        }

        /// <summary>
        /// 通过反射遍历 LogTag 中所有 public static const string 字段，
        /// 按标签路径深度构建 m_RootNodes 树形字典。
        /// </summary>
        private void BuildTree()
        {
            var logTagType = RuntimeDebugger.LogTagType;
            if (logTagType == null)
            {
                Debug.LogError("[TagFilterPanel] RuntimeDebugger.LogTagType 未设置，无法构建 Tag 树。请在首次打开 Console Tab 前调用 RuntimeDebugger.LogTagType = typeof(YourLogTag)。");
                return;
            }

            var fields = logTagType.GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            // 先临时构建到 children，深度从 0 开始
            var children = new SortedDictionary<string, TagNode>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string) || !field.IsLiteral)
                    continue;
                var tagValue = (string)field.GetValue(null);
                if (string.IsNullOrEmpty(tagValue))
                    continue;
                var matches = s_TagRegex.Matches(tagValue);
                if (matches.Count == 0)
                    continue;
                string tagDesc = RuntimeDebugger.LogTagDescriptionResolver?.Invoke(field) ?? string.Empty;

                if (matches.Count == 1)
                {
                    if (!children.ContainsKey(tagValue))
                        children[tagValue] = new TagNode { Label = tagValue, Depth = 0, Description = tagDesc };
                }
                else
                {
                    var current = children;
                    for (int i = 0; i < matches.Count - 1; i++)
                    {
                        var pathKey = BuildPath(matches, 0, i);
                        if (!current.ContainsKey(pathKey))
                            current[pathKey] = new TagNode { Label = pathKey, Depth = i };
                        current = current[pathKey].Children;
                    }
                    if (!current.ContainsKey(tagValue))
                        current[tagValue] = new TagNode { Label = tagValue, Depth = matches.Count - 1, Description = tagDesc };
                }
            }

            foreach (var kvp in children)
                m_RootNodes[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// 将正则匹配结果中 [from, to] 范围内的片段拼接为完整路径字符串。
        /// </summary>
        /// <param name="matches">s_TagRegex 对完整标签值的匹配集合。</param>
        /// <param name="from">起始下标（含）。</param>
        /// <param name="to">结束下标（含）。</param>
        /// <returns>拼接后的路径字符串，如 "[SDK][AppsFlyerPlugin]"。</returns>
        private static string BuildPath(MatchCollection matches, int from, int to)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = from; i <= to; i++)
                sb.Append(matches[i].Value);
            return sb.ToString();
        }

        /// <summary>
        /// 递归将节点字典平铺实例化为 UI 行，通过 TagRowItem.Setup 设置标签文字。
        /// </summary>
        /// <param name="nodes">当前层级的有序节点字典。</param>
        /// <param name="container">行 GameObject 的父 Transform。</param>
        private void RenderTree(SortedDictionary<string, TagNode> nodes, Transform container)
        {
            foreach (var kvp in nodes)
            {
                var node = kvp.Value;
                var go = Instantiate(TagLeafRowPrefab, container);

                var row = go.GetComponent<TagRowItem>();
                if (row == null)
                {
                    Debug.LogError("[TagFilterPanel] TagLeafRowPrefab missing TagRowItem component.");
                    continue;
                }

                var captured = node;
                row.Setup(node.Label, node.Description, 0, false, isOn => OnToggleChanged(captured, isOn));
                node.Toggle = row.Toggle;
                node.RowItem = row;

                if (!node.IsLeaf)
                    RenderTree(node.Children, container);
            }
        }

        /// <summary>
        /// Toggle 状态变更回调：仅更新 m_PendingTags，不向外通知。
        /// </summary>
        /// <param name="node">状态发生变化的节点。</param>
        /// <param name="isOn">Toggle 当前是否选中。</param>
        private void OnToggleChanged(TagNode node, bool isOn)
        {
            if (isOn)
                AddNodeToPending(node);
            else
                RemoveNodeFromPending(node);
            RefreshSelectedCountLabel();
            SyncSelectAllToggle();
        }

        /// <summary>
        /// 将节点对应的叶子标签加入 pending 集合；非叶子节点同时递归同步子节点 Toggle 状态。
        /// </summary>
        /// <param name="node">要加入的节点。</param>
        private void AddNodeToPending(TagNode node)
        {
            m_PendingTags.Add(node.Label);
            if (!node.IsLeaf)
            {
                foreach (var child in node.Children.Values)
                {
                    child.Toggle?.SetIsOnWithoutNotify(true);
                    AddNodeToPending(child);
                }
            }
        }

        /// <summary>
        /// 将节点对应的叶子标签从 pending 集合移除；非叶子节点同时递归同步子节点 Toggle 状态。
        /// </summary>
        /// <param name="node">要移除的节点。</param>
        private void RemoveNodeFromPending(TagNode node)
        {
            m_PendingTags.Remove(node.Label);
            if (!node.IsLeaf)
            {
                foreach (var child in node.Children.Values)
                {
                    child.Toggle?.SetIsOnWithoutNotify(false);
                    RemoveNodeFromPending(child);
                }
            }
        }

        /// <summary>
        /// 递归将节点字典中所有 Toggle 按 m_PendingTags 静默同步，不触发 onValueChanged 回调。
        /// </summary>
        /// <param name="nodes">当前层级的有序节点字典。</param>
        private void SyncTogglesToPending(SortedDictionary<string, TagNode> nodes)
        {
            foreach (var node in nodes.Values)
            {
                if (node.IsLeaf)
                {
                    node.Toggle?.SetIsOnWithoutNotify(m_PendingTags.Contains(node.Label));
                }
                else
                {
                    SyncTogglesToPending(node.Children);
                    node.Toggle?.SetIsOnWithoutNotify(m_PendingTags.Contains(node.Label) && AreAllDescendantsSelected(node));
                }
            }
        }

        /// <summary>
        /// 递归判断节点自身及所有后代是否全部在 pending 中。
        /// </summary>
        /// <param name="node">要检查的节点。</param>
        /// <returns>节点自身及所有后代均已选中则返回 true。</returns>
        private bool AreAllDescendantsSelected(TagNode node)
        {
            if (!m_PendingTags.Contains(node.Label))
                return false;
            if (!node.IsLeaf)
            {
                foreach (var child in node.Children.Values)
                {
                    if (!AreAllDescendantsSelected(child))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 清空 pending 集合，同步所有 Toggle 为未选中。
        /// </summary>
        public void ClearAll()
        {
            m_PendingTags.Clear();
            SyncTogglesToPending(m_RootNodes);
            RefreshSelectedCountLabel();
            SyncSelectAllToggle();
        }

        /// <summary>
        /// 将所有叶子标签加入 pending 集合，同步所有 Toggle 为选中。
        /// </summary>
        public void SelectAll()
        {
            SelectAllNodes(m_RootNodes);
            RefreshSelectedCountLabel();
            SyncSelectAllToggle();
        }

        /// <summary>
        /// SelectAllToggle 值变更回调：isOn=true 执行全选，isOn=false 执行全取消。
        /// </summary>
        /// <param name="isOn">Toggle 当前状态。</param>
        private void OnSelectAllToggleChanged(bool isOn)
        {
            if (isOn)
                SelectAll();
            else
                ClearAll();
        }

        /// <summary>
        /// 静默同步 SelectAllToggle 状态：所有叶子均已选中则 isOn=true，否则 false。
        /// </summary>
        private void SyncSelectAllToggle()
        {
            if (SelectAllToggle == null)
                return;
            bool allSelected = CountLeaves(m_PendingTags, m_RootNodes) == m_TotalLeafCount;
            SelectAllToggle.SetIsOnWithoutNotify(allSelected);
        }

        /// <summary>
        /// 搜索框输入变更回调：更新关键词并重新应用可见性过滤。
        /// </summary>
        /// <param name="keyword">当前输入的搜索词。</param>
        private void OnSearchChanged(string keyword)
        {
            m_SearchKeyword = keyword ?? string.Empty;
            ApplyVisibility();
        }

        /// <summary>
        /// 递归将节点字典中所有叶子加入 pending 集合，所有 Toggle 静默置为选中。
        /// </summary>
        /// <param name="nodes">当前层级的有序节点字典。</param>
        private void SelectAllNodes(SortedDictionary<string, TagNode> nodes)
        {
            foreach (var node in nodes.Values)
            {
                node.Toggle?.SetIsOnWithoutNotify(true);
                m_PendingTags.Add(node.Label);
                if (!node.IsLeaf)
                    SelectAllNodes(node.Children);
            }
        }

        /// <summary>
        /// 确定按钮回调：将 pending 集合提交，触发 OnTagFilterChanged，再通知外部关闭面板。
        /// </summary>
        private void OnConfirm()
        {
            OnTagFilterChanged?.Invoke(m_PendingTags);
            OnConfirmClicked?.Invoke();
        }

        /// <summary>
        /// 关闭按钮回调：丢弃 pending，用快照还原 Toggle UI，不触发 OnTagFilterChanged。
        /// </summary>
        private void OnClose()
        {
            m_PendingTags.Clear();
            foreach (var tag in m_SnapshotTags)
                m_PendingTags.Add(tag);
            SyncTogglesToPending(m_RootNodes);
            RefreshSelectedCountLabel();
            OnCloseClicked?.Invoke();
        }

        /// <summary>
        /// 从 m_RootNodes 按一级子节点构建左侧分类 Tab 列表。
        /// CategoryRowPrefab 或 CategoryContainer 为 null 时跳过（兼容旧预制体）。
        /// 分类名称取一级节点 Label 的最后一个 [Tag] 片段；"全部标签"固定为第一条，点击后不过滤。
        /// </summary>
        private void InitCategoryTabs()
        {
            if (CategoryRowPrefab == null || CategoryContainer == null)
                return;

            SpawnCategoryRow("全部标签", m_TotalLeafCount, true, null);
            foreach (var node in m_RootNodes.Values)
            {
                string display = string.IsNullOrEmpty(node.Description) ? node.Label : node.Description;
                int count = 1 + (node.IsLeaf ? 0 : CountLeaves(node.Children));
                SpawnCategoryRow(display, count, false, node);
            }
        }

        /// <summary>
        /// 实例化一个分类行并加入 CategoryContainer。
        /// </summary>
        /// <param name="display">分类显示名称。</param>
        /// <param name="count">该分类标签数量。</param>
        /// <param name="selected">初始是否选中。</param>
        /// <param name="node">对应的根节点，null 表示"全部标签"。</param>
        private void SpawnCategoryRow(string display, int count, bool selected, TagNode node)
        {
            var go = Instantiate(CategoryRowPrefab, CategoryContainer);
            var row = go.GetComponent<TagCategoryRowItem>();
            if (row == null)
            {
                Debug.LogError("[TagFilterPanel] CategoryRowPrefab missing TagCategoryRowItem component.");
                return;
            }
            int index = m_CategoryRows.Count;
            m_CategoryRows.Add(row);
            m_CategoryNodes.Add(node);
            row.Setup(display, count, selected, () => SelectCategoryRow(index));
        }

        /// <summary>
        /// 单选逻辑：选中指定索引的分类行，其余取消；并按对应根节点过滤右侧行可见性。
        /// index 为 0（全部标签）时显示所有行；否则只显示该根节点及其所有子孙节点的行。
        /// </summary>
        /// <param name="index">要选中的行索引。</param>
        private void SelectCategoryRow(int index)
        {
            for (int i = 0; i < m_CategoryRows.Count; i++)
                m_CategoryRows[i].SetSelected(i == index);

            m_CurrentFilterNode = m_CategoryNodes[index];
            ApplyVisibility();
        }

        /// <summary>
        /// 综合当前分类过滤和搜索关键词，设置所有行的可见性。
        /// 先按分类确定候选节点集，再按搜索词在候选集内进一步过滤。
        /// </summary>
        private void ApplyVisibility()
        {
            SetAllRowsVisible(m_RootNodes, false);

            if (m_CurrentFilterNode == null)
                ApplySearchFilter(m_RootNodes);
            else
            {
                if (m_CurrentFilterNode.RowItem != null)
                    ApplySearchFilterSingle(m_CurrentFilterNode);
                if (!m_CurrentFilterNode.IsLeaf)
                    ApplySearchFilter(m_CurrentFilterNode.Children);
            }
        }

        /// <summary>
        /// 对节点字典中每个节点递归应用搜索过滤，关键词为空时全部显示。
        /// </summary>
        /// <param name="nodes">要过滤的节点字典。</param>
        private void ApplySearchFilter(SortedDictionary<string, TagNode> nodes)
        {
            foreach (var node in nodes.Values)
            {
                ApplySearchFilterSingle(node);
                if (!node.IsLeaf)
                    ApplySearchFilter(node.Children);
            }
        }

        /// <summary>
        /// 对单个节点应用搜索过滤，关键词为空或节点 Label 包含关键词时显示，否则隐藏。
        /// </summary>
        /// <param name="node">目标节点。</param>
        private void ApplySearchFilterSingle(TagNode node)
        {
            if (node.RowItem == null)
                return;
            bool visible = string.IsNullOrEmpty(m_SearchKeyword) ||
                           node.Label.IndexOf(m_SearchKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
            node.RowItem.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 递归设置节点字典中所有行的可见性。
        /// </summary>
        /// <param name="nodes">要遍历的节点字典，为 null 时跳过。</param>
        /// <param name="visible">目标可见状态。</param>
        /// <param name="selfNode">额外需要设置可见性的单个节点（用于根节点自身），可为 null。</param>
        private static void SetAllRowsVisible(SortedDictionary<string, TagNode> nodes, bool visible, TagNode selfNode = null)
        {
            if (selfNode?.RowItem != null)
                selfNode.RowItem.gameObject.SetActive(visible);
            if (nodes == null)
                return;
            foreach (var node in nodes.Values)
            {
                if (node.RowItem != null)
                    node.RowItem.gameObject.SetActive(visible);
                if (!node.IsLeaf)
                    SetAllRowsVisible(node.Children, visible);
            }
        }

        /// <summary>
        /// 统计节点字典中所有节点数量（含父节点自身，每个节点都是一个可独立筛选的标签）。
        /// </summary>
        /// <param name="nodes">要统计的节点字典。</param>
        /// <returns>节点总数。</returns>
        private static int CountLeaves(SortedDictionary<string, TagNode> nodes)
        {
            int total = 0;
            foreach (var node in nodes.Values)
                total += 1 + (node.IsLeaf ? 0 : CountLeaves(node.Children));
            return total;
        }

        /// <summary>
        /// 更新 SelectedCountLabel 文字为"已选择 N/Total"。
        /// </summary>
        private void RefreshSelectedCountLabel()
        {
            if (SelectedCountLabel == null)
                return;
            int selected = CountLeaves(m_PendingTags, m_RootNodes);
            SelectedCountLabel.text = string.Format("已选择 {0}/{1}", selected, m_TotalLeafCount);
        }

        /// <summary>
        /// 统计 pending 集合中实际命中的叶子数量（与节点树对照，避免计入无效 key）。
        /// </summary>
        /// <param name="pending">当前 pending 集合。</param>
        /// <param name="nodes">节点字典。</param>
        /// <returns>已选中叶子数量。</returns>
        private static int CountLeaves(HashSet<string> pending, SortedDictionary<string, TagNode> nodes)
        {
            int total = 0;
            foreach (var node in nodes.Values)
            {
                total += pending.Contains(node.Label) ? 1 : 0;
                if (!node.IsLeaf)
                    total += CountLeaves(pending, node.Children);
            }
            return total;
        }

        /// <summary>
        /// 遍历所有日志条目，按 tag 统计数量后更新各节点行的 CountLabel。
        /// 父节点显示其所有叶子的日志总和。
        /// </summary>
        private void RefreshCounts()
        {
            var entries = Service.Console.Entries;
            UpdateNodeCounts(m_RootNodes, entries);
        }

        /// <summary>
        /// 递归统计节点字典内每个节点匹配的日志数量，叶子节点直接统计，父节点汇总子节点。
        /// </summary>
        /// <param name="nodes">当前层级节点字典。</param>
        /// <param name="entries">全量日志条目列表。</param>
        /// <returns>当前层级所有节点匹配的日志总数。</returns>
        private static int UpdateNodeCounts(SortedDictionary<string, TagNode> nodes, IReadOnlyList<ConsoleEntry> entries)
        {
            int total = 0;
            foreach (var node in nodes.Values)
            {
                int childTotal = node.IsLeaf ? 0 : UpdateNodeCounts(node.Children, entries);
                int selfCount = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    var msg = entries[i].Message;
                    if (msg.IndexOf(node.Label, System.StringComparison.Ordinal) < 0)
                        continue;
                    if (!node.IsLeaf)
                    {
                        // 父节点只统计不属于任何子节点的日志（精确属于自身的条目）
                        bool matchesChild = false;
                        foreach (var child in node.Children.Values)
                        {
                            if (msg.IndexOf(child.Label, System.StringComparison.Ordinal) >= 0)
                            {
                                matchesChild = true;
                                break;
                            }
                        }
                        if (!matchesChild)
                            selfCount++;
                    }
                    else
                    {
                        selfCount++;
                    }
                }
                int count = selfCount + childTotal;
                node.RowItem?.UpdateCount(count);
                total += count;
            }
            return total;
        }
    }
}
