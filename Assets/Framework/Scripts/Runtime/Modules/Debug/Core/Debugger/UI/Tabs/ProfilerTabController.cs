/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProfilerTabController.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
//#define DEBUGGER_CONSOLE_TRACE

namespace NovaFramework.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using UnityEngine.Profiling;
    using UnityEngine.UI;

    /// <summary>
    /// Profiler 标签页控制器。
    /// Memory Summary 区块通过 VirtualVerticalLayoutGroup 驱动虚拟滚动，
    /// 彻底消除大数据量（数千条 ObjectSample）时的 UGUI 顶点重建卡顿。
    /// </summary>
    public class ProfilerTabController : DebugMonoBehaviourEx
    {
        /// <summary>
        /// 勾选字符。
        /// </summary>
        private const char c_Tick = '✓';

        /// <summary>
        /// 叉号字符。
        /// </summary>
        private const char c_Cross = '×';

        /// <summary>
        /// Profiler 条目名称列颜色。
        /// </summary>
        private const string c_NameColor = "#BCBCBC";

        /// <summary>
        /// Profiler 信息块标题最大字符长度（"Allocated Memory For Graphics Driver".Length + 2 = 38）。
        /// </summary>
        private const int c_MaxTitleLength = 38;

        /// <summary>
        /// Summary/分类模式下 Type/Name 列软上限（字符数），超长在中间截断加省略号。
        /// </summary>
        private const int c_MaxTypeColumnWidth = 40;

        /// <summary>
        /// 分类模式下 Name 列宽的下限（字符数），防止列表过窄时列头错位。
        /// </summary>
        private const int c_MinNameColumnWidth = 20;

        /// <summary>
        /// 脏标记，OnEnable 时置 true，触发下一帧 Refresh。
        /// </summary>
        private bool _isDirty;

        /// <summary>
        /// Summary 模式下的按类型聚合记录列表。
        /// </summary>
        private readonly List<Record> _records = new List<Record>();

        /// <summary>
        /// 分类模式下的单对象采样列表。
        /// </summary>
        private readonly List<ObjectSample> _objectSamples = new List<ObjectSample>();

        /// <summary>
        /// 分类模式下重复项的总内存大小（字节）。
        /// </summary>
        private long _duplicateSampleSize;

        /// <summary>
        /// 分类模式下重复项的数量。
        /// </summary>
        private int _duplicateSampleCount;

        /// <summary>
        /// 最后一次采样使用的模式。
        /// </summary>
        private MemoryMode _lastSampledMode = MemoryMode.Summary;

        /// <summary>
        /// 最后一次采样的时间戳，未采样时为 DateTime.MinValue。
        /// </summary>
        private DateTime _sampleTime = DateTime.MinValue;

        /// <summary>
        /// 最后一次采样的总对象数量。
        /// </summary>
        private int _sampleCount;

        /// <summary>
        /// 最后一次采样的总内存大小（字节）。
        /// </summary>
        private long _sampleSize;

        /// <summary>
        /// 供 RefreshProfilerBlock 复用的 StringBuilder，避免热路径分配。
        /// </summary>
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        /// <summary>
        /// Inspector 引用：固定到顶部的开关。
        /// </summary>
        [RequiredField] public Toggle PinToggle;

        /// <summary>
        /// Inspector 引用：Profiler/Memory 块的流式布局容器。
        /// </summary>
        [RequiredField] public Transform FlowLayoutGroup;

        /// <summary>
        /// Inspector 引用：InfoBlock 预制体。
        /// </summary>
        [RequiredField] public InfoBlock InfoBlockPrefab;

        /// <summary>
        /// Inspector 引用：MemorySummaryBlock 预制体。
        /// </summary>
        [RequiredField] public MemorySummaryBlock MemorySummaryBlockPrefab;

        /// <summary>
        /// 性能信息块。
        /// </summary>
        [RequiredField] public GameObject DebugContent;

        /// <summary>
        /// MonoBehaviour Start：初始化 PinToggle 监听，创建 Profiler/Memory 块。
        /// </summary>
        protected override void Start()
        {
            base.Start();

            ApplyLayoutElementSize();
            PinToggle.onValueChanged.AddListener(PinToggleValueChanged);
            Refresh();
            CreateProfilerBlock();
            CreateMemoryBlock();
        }

        /// <summary>
        /// 根据父节点宽度和祖父节点高度动态设置自身 LayoutElement 的 minWidth/minHeight，
        /// 使本节点恰好填满父节点中其他兄弟节点之外的剩余空间。
        /// </summary>
        private void ApplyLayoutElementSize()
        {
    /*         var le = FlowLayoutGroup.GetComponent<LayoutElement>();
            if (le == null) return;

            var parent = FlowLayoutGroup.parent as RectTransform;
            if (parent == null) return;

            var grandParent = parent.parent as RectTransform;
            if (grandParent == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

            le.minWidth = parent.rect.width;

            float siblingsHeight = 0f;
            for (int i = 0; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling == FlowLayoutGroup || !sibling.gameObject.activeSelf) continue;
                var siblingRect = sibling as RectTransform;
                if (siblingRect != null)
                    siblingsHeight += siblingRect.rect.height;
            }

            le.minHeight = Math.Max(le.minHeight ,grandParent.rect.height - siblingsHeight); */
        }

        /// <summary>
        /// MonoBehaviour OnEnable：标记脏，下一帧同步 PinToggle 状态。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            _isDirty = true;
        }

        /// <summary>
        /// MonoBehaviour Update：处理脏刷新与每帧 Profiler 信息刷新。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (_isDirty)
            {
                Refresh();
            }

            RefreshProfilerBlock();
        }

        /// <summary>
        /// 同步 PinToggle 状态，清除脏标记。
        /// </summary>
        private void Refresh()
        {
            PinToggle.isOn = RuntimeDebugger.Instance.IsProfilerDocked;
            _isDirty = false;
        }

        /// <summary>
        /// PinToggle 值变化回调，同步 IsProfilerDocked。
        /// </summary>
        /// <param name="isOn">
        /// 当前 Toggle 状态。
        /// </param>
        private void PinToggleValueChanged(bool isOn)
        {
            RuntimeDebugger.Instance.IsProfilerDocked = isOn;
        }

        /// <summary>
        /// 实例化并初始化 Profiler 信息块。
        /// </summary>
        private void CreateProfilerBlock()
        {
            InfoBlockPrefab.Title.text = "Profiler Information";
            InfoBlockPrefab.CachedTransform.SetParent(FlowLayoutGroup, false);
        }

        /// <summary>
        /// 每帧刷新 Profiler 信息块文本（运行时实时数据）。
        /// </summary>
        private void RefreshProfilerBlock()
        {
            if (InfoBlockPrefab == null)
            {
                return;
            }

            _stringBuilder.Clear();

            AppendEntry(_stringBuilder, "Supported", Profiler.supported, true);
            AppendEntry(_stringBuilder, "Enabled", Profiler.enabled, false);
            AppendEntry(_stringBuilder, "Enable Binary Log", Profiler.enableBinaryLog ? $"True, {Profiler.logFile}" : "False", false);
            AppendEntry(_stringBuilder, "Area Count", Profiler.areaCount.ToString(), false);
            AppendEntry(_stringBuilder, "Max Used Memory", GetByteLengthString(Profiler.maxUsedMemory), false);
            AppendEntry(_stringBuilder, "Mono Used Size", GetByteLengthString(Profiler.GetMonoUsedSizeLong()), false);
            AppendEntry(_stringBuilder, "Mono Heap Size", GetByteLengthString(Profiler.GetMonoHeapSizeLong()), false);
            AppendEntry(_stringBuilder, "Used Heap Size", GetByteLengthString(Profiler.usedHeapSizeLong), false);
            AppendEntry(_stringBuilder, "Total Allocated Memory", GetByteLengthString(Profiler.GetTotalAllocatedMemoryLong()), false);
            AppendEntry(_stringBuilder, "Total Reserved Memory", GetByteLengthString(Profiler.GetTotalReservedMemoryLong()), false);
            AppendEntry(_stringBuilder, "Total Unused Reserved Memory", GetByteLengthString(Profiler.GetTotalUnusedReservedMemoryLong()), false);
            AppendEntry(_stringBuilder, "Allocated Memory For Graphics Driver", GetByteLengthString(Profiler.GetAllocatedMemoryForGraphicsDriver()), false);
            AppendEntry(_stringBuilder, "Temp Allocator Size", GetByteLengthString(Profiler.GetTempAllocatorSize()), false);

            InfoBlockPrefab.Content.text = _stringBuilder.ToString();
        }

        /// <summary>
        /// 向 StringBuilder 追加一条 bool 类型 Profiler 条目。
        /// </summary>
        /// <param name="sb">
        /// 目标 StringBuilder。
        /// </param>
        /// <param name="title">
        /// 条目名称。
        /// </param>
        /// <param name="value">
        /// bool 值，true 显示勾号，false 显示叉号。
        /// </param>
        /// <param name="isFirst">
        /// 是否是第一条（true 时不在前面加换行）。
        /// </param>
        private static void AppendEntry(StringBuilder sb, string title, bool value, bool isFirst)
        {
            if (!isFirst)
            {
                sb.AppendLine();
            }

            sb.Append("<color=");
            sb.Append(c_NameColor);
            sb.Append(">");
            sb.Append(title);
            sb.Append(": ");
            sb.Append("</color>");

            for (var j = title.Length; j <= c_MaxTitleLength; ++j)
            {
                sb.Append(' ');
            }

            sb.Append(value ? c_Tick : c_Cross);
        }

        /// <summary>
        /// 向 StringBuilder 追加一条字符串类型 Profiler 条目。
        /// </summary>
        /// <param name="sb">
        /// 目标 StringBuilder。
        /// </param>
        /// <param name="title">
        /// 条目名称。
        /// </param>
        /// <param name="value">
        /// 要显示的字符串值。
        /// </param>
        /// <param name="isFirst">
        /// 是否是第一条（true 时不在前面加换行）。
        /// </param>
        private static void AppendEntry(StringBuilder sb, string title, string value, bool isFirst)
        {
            if (!isFirst)
            {
                sb.AppendLine();
            }

            sb.Append("<color=");
            sb.Append(c_NameColor);
            sb.Append(">");
            sb.Append(title);
            sb.Append(": ");
            sb.Append("</color>");

            for (var j = title.Length; j <= c_MaxTitleLength; ++j)
            {
                sb.Append(' ');
            }

            sb.Append(value);
        }

        /// <summary>
        /// 实例化并初始化 Memory Summary 块，绑定采样按钮，执行首次内容刷新。
        /// </summary>
        private void CreateMemoryBlock()
        {
          
            MemorySummaryBlockPrefab.Title.text = "Runtime Memory Summary";
            MemorySummaryBlockPrefab.SampleButton.onClick.AddListener(TakeSample);
            RefreshMemoryBlock();
        }

        /// <summary>
        /// 读取当前选中的内存采样模式（由 TypeToggles 决定）。
        /// </summary>
        /// <returns>
        /// 当前选中的 MemoryMode，无 Toggle 选中时默认返回 Summary。
        /// </returns>
        private MemoryMode GetSelectedMode()
        {
            for (int i = 0; i < MemorySummaryBlockPrefab.TypeToggles.Length; i++)
            {
                if (MemorySummaryBlockPrefab.TypeToggles[i] != null && MemorySummaryBlockPrefab.TypeToggles[i].isOn)
                {
                    return (MemoryMode)i;
                }
            }

            return MemoryMode.Summary;
        }

        /// <summary>
        /// 执行内存采样，根据选中模式分派到 Summary 或分类采样路径，完成后刷新显示。
        /// </summary>
        private void TakeSample()
        {
            MemoryMode mode = GetSelectedMode();
            _lastSampledMode = mode;
            _sampleTime = DateTime.Now;

            _records.Clear();
            _objectSamples.Clear();
            _sampleCount = 0;
            _sampleSize = 0L;
            _duplicateSampleSize = 0L;
            _duplicateSampleCount = 0;

            if (mode == MemoryMode.Summary)
            {
                TakeSampleSummary();
            }
            else
            {
                TakeSampleByType(mode);
            }

            RefreshMemoryBlock();
        }

        /// <summary>
        /// Summary 模式采样：枚举所有 UnityEngine.Object，按类型名聚合为 Record 列表。
        /// </summary>
        private void TakeSampleSummary()
        {
            UnityEngine.Object[] samples = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
            for (int i = 0; i < samples.Length; i++)
            {
                long sampleSize = Profiler.GetRuntimeMemorySizeLong(samples[i]);
                string name = samples[i].GetType().Name;
                _sampleCount++;
                _sampleSize += sampleSize;

                Record record = null;
                for (int j = 0; j < _records.Count; j++)
                {
                    if (_records[j].Name == name)
                    {
                        record = _records[j];
                        break;
                    }
                }

                if (record == null)
                {
                    record = new Record(name);
                    _records.Add(record);
                }

                record.Count++;
                record.Size += sampleSize;
            }

            _records.Sort(RecordComparer);
        }

        /// <summary>
        /// 分类模式采样：根据 mode 枚举对应类型对象，填充 _objectSamples 并标记重复项。
        /// </summary>
        /// <param name="mode">
        /// 要采样的内存分类模式（All/Texture/Mesh 等）。
        /// </param>
        private void TakeSampleByType(MemoryMode mode)
        {
            switch (mode)
            {
                case MemoryMode.All: SampleGeneric<UnityEngine.Object>(); break;
                case MemoryMode.Texture: SampleGeneric<Texture>(); break;
                case MemoryMode.Mesh: SampleGeneric<Mesh>(); break;
                case MemoryMode.Material: SampleGeneric<Material>(); break;
                case MemoryMode.Shader: SampleGeneric<Shader>(); break;
                case MemoryMode.AnimationClip: SampleGeneric<AnimationClip>(); break;
                case MemoryMode.AudioClip: SampleGeneric<AudioClip>(); break;
                case MemoryMode.Font: SampleGeneric<Font>(); break;
                case MemoryMode.TextAsset: SampleGeneric<TextAsset>(); break;
                case MemoryMode.ScriptableObject: SampleGeneric<ScriptableObject>(); break;
            }

            _objectSamples.Sort(ObjectSampleComparer);

            for (int i = 1; i < _objectSamples.Count; i++)
            {
                ObjectSample cur = _objectSamples[i];
                ObjectSample prev = _objectSamples[i - 1];
                if (cur.Name == prev.Name && cur.Type == prev.Type && cur.Size == prev.Size)
                {
                    cur.Highlight = true;
                    _duplicateSampleSize += cur.Size;
                    _duplicateSampleCount++;
                }
            }
        }

        /// <summary>
        /// 泛型采样辅助方法：枚举指定类型的所有对象，填入 _objectSamples。
        /// </summary>
        /// <typeparam name="T">
        /// 要采样的 UnityEngine.Object 子类型。
        /// </typeparam>
        private void SampleGeneric<T>() where T : UnityEngine.Object
        {
            T[] samples = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < samples.Length; i++)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(samples[i]);
                _sampleSize += size;
                _sampleCount++;
                _objectSamples.Add(new ObjectSample(samples[i].name, samples[i].GetType().Name, size));
            }
        }

        /// <summary>
        /// ObjectSample 排序比较器：按 Size 降序，Size 相同则按 Type/Name 升序。
        /// </summary>
        /// <param name="a">
        /// 第一个比较项。
        /// </param>
        /// <param name="b">
        /// 第二个比较项。
        /// </param>
        /// <returns>
        /// 比较结果（负值/0/正值）。
        /// </returns>
        private int ObjectSampleComparer(ObjectSample a, ObjectSample b)
        {
            int result = b.Size.CompareTo(a.Size);
            if (result != 0) return result;
            result = a.Type.CompareTo(b.Type);
            if (result != 0) return result;
            return a.Name.CompareTo(b.Name);
        }

        /// <summary>
        /// Record 排序比较器：按 Size 降序，Size 相同则按 Count/Name 升序。
        /// </summary>
        /// <param name="a">
        /// 第一个比较项。
        /// </param>
        /// <param name="b">
        /// 第二个比较项。
        /// </param>
        /// <returns>
        /// 比较结果（负值/0/正值）。
        /// </returns>
        private int RecordComparer(Record a, Record b)
        {
            int result = b.Size.CompareTo(a.Size);
            if (result != 0)
            {
                return result;
            }

            result = a.Count.CompareTo(b.Count);
            if (result != 0)
            {
                return result;
            }

            return a.Name.CompareTo(b.Name);
        }

        /// <summary>
        /// 刷新 Memory Summary 块：把数据喂给 VVLG，VVLG 自动驱动虚拟滚动。
        /// </summary>
        private void RefreshMemoryBlock()
        {
            if (MemorySummaryBlockPrefab == null)
            {
                return;
            }

            VirtualVerticalLayoutGroup vvlg = MemorySummaryBlockPrefab.VirtualLayoutGroup;
            vvlg.ClearItems();

            if (_sampleTime <= DateTime.MinValue)
            {
                MemorySummaryBlockPrefab.DetailLabel.text = "Please take sample first.";
                MemorySummaryBlockPrefab.DescLabel.text = string.Empty;
                return;
            }

            if (_lastSampledMode == MemoryMode.Summary)
            {
                RefreshSummaryContent(vvlg);
            }
            else
            {
                RefreshObjectListContent(vvlg);
            }
        }

        /// <summary>
        /// Summary 模式：写入 DetailLabel/DescLabel header，VVLG 只存数据行，按 Size 降序。
        /// </summary>
        /// <param name="vvlg">
        /// 目标虚拟垂直布局组。
        /// </param>
        private void RefreshSummaryContent(VirtualVerticalLayoutGroup vvlg)
        {
            MemorySummaryBlockPrefab.DetailLabel.text =
                $"<color={c_NameColor}>Sampled at </color>{_sampleTime:yyyy-MM-dd HH:mm:ss}" +
                $" | <color={c_NameColor}>Mode </color>Summary" +
                $" | {_sampleCount} objects ({GetByteLengthString(_sampleSize)})";

            MemorySummaryBlockPrefab.DescLabel.text = "<b>Type\tCount\tSize</b>";

            for (int i = 0; i < _records.Count; i++)
            {
                Record r = _records[i];
                vvlg.AddItem(new MemoryRowData
                {
                    Name = r.Name,
                    Type = r.Count.ToString(),
                    Size = GetByteLengthString(r.Size),
                    Highlight = false,
                });
            }
        }

        /// <summary>
        /// 分类模式：写入 DetailLabel/DescLabel header，VVLG 只存数据行，duplicate 行用 color=yellow 高亮。
        /// </summary>
        /// <param name="vvlg">
        /// 目标虚拟垂直布局组。
        /// </param>
        private void RefreshObjectListContent(VirtualVerticalLayoutGroup vvlg)
        {
            string typeName = _lastSampledMode.ToString();
            string totalInfo = _duplicateSampleCount > 0
                ? $"{_sampleCount} {typeName}s ({GetByteLengthString(_sampleSize)}), {_duplicateSampleCount} duplicated ({GetByteLengthString(_duplicateSampleSize)})"
                : $"{_sampleCount} {typeName}s ({GetByteLengthString(_sampleSize)})";

            MemorySummaryBlockPrefab.DetailLabel.text =
                $"<color={c_NameColor}>Sampled at </color>{_sampleTime:yyyy-MM-dd HH:mm:ss}" +
                $" | <color={c_NameColor}>Mode </color>{typeName}" +
                $" | {totalInfo}";

            MemorySummaryBlockPrefab.DescLabel.text = "<b>Name\tType\tSize</b>";

            for (int i = 0; i < _objectSamples.Count; i++)
            {
                ObjectSample s = _objectSamples[i];
                vvlg.AddItem(new MemoryRowData
                {
                    Name = s.Name,
                    Type = s.Type,
                    Size = GetByteLengthString(s.Size),
                    Highlight = s.Highlight,
                });
            }
        }

        /// <summary>
        /// 将字节数格式化为带单位的可读字符串（B / KB / MB / GB / TB / PB / EB）。
        /// </summary>
        /// <param name="byteLength">
        /// 字节数。
        /// </param>
        /// <returns>
        /// 格式化后的字符串，如 "1.23 MB"。
        /// </returns>
        private static string GetByteLengthString(long byteLength)
        {
            if (byteLength < 1024L)
            {
                return $"{byteLength} B";
            }

            if (byteLength < 1048576L)
            {
                return $"{(byteLength / 1024f).ToString("F2")} KB";
            }

            if (byteLength < 1073741824L)
            {
                return $"{(byteLength / 1048576f).ToString("F2")} MB";
            }

            if (byteLength < 1099511627776L)
            {
                return $"{(byteLength / 1073741824f).ToString("F2")} GB";
            }

            if (byteLength < 1125899906842624L)
            {
                return $"{(byteLength / 1099511627776f).ToString("F2")} TB";
            }

            if (byteLength < 1152921504606846976L)
            {
                return $"{(byteLength / 1125899906842624f).ToString("F2")} PB";
            }

            return $"{(byteLength / 1152921504606846976f).ToString("F2")} EB";
        }

        /// <summary>
        /// 将字符串截断至 maxLen 字符，超长则在中间插入省略号。
        /// </summary>
        /// <param name="text">
        /// 原始字符串。
        /// </param>
        /// <param name="maxLen">
        /// 上限长度（含省略号）。
        /// </param>
        /// <returns>
        /// 长度不超过 maxLen 的字符串。
        /// </returns>
        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            {
                return text ?? string.Empty;
            }

            if (maxLen <= 1)
            {
                return "…";
            }

            int keep = maxLen - 1;
            int left = keep / 2;
            int right = keep - left;
            return text.Substring(0, left) + "…" + text.Substring(text.Length - right);
        }

        /// <summary>
        /// Summary 模式按类型聚合记录。
        /// </summary>
        private sealed class Record
        {
            /// <summary>
            /// 类型名称。
            /// </summary>
            public string Name;

            /// <summary>
            /// 该类型的对象数量。
            /// </summary>
            public int Count;

            /// <summary>
            /// 该类型的总内存占用（字节）。
            /// </summary>
            public long Size;

            /// <summary>
            /// 用指定类型名初始化 Record。
            /// </summary>
            /// <param name="name">
            /// 类型名称。
            /// </param>
            public Record(string name)
            {
                Name = name;
            }
        }

        /// <summary>
        /// 内存采样的显示模式枚举。
        /// </summary>
        private enum MemoryMode
        {
            /// <summary>
            /// 按类型汇总。
            /// </summary>
            Summary = 0,

            /// <summary>
            /// 枚举所有对象。
            /// </summary>
            All = 1,

            /// <summary>
            /// 仅 Texture。
            /// </summary>
            Texture = 2,

            /// <summary>
            /// 仅 Mesh。
            /// </summary>
            Mesh = 3,

            /// <summary>
            /// 仅 Material。
            /// </summary>
            Material = 4,

            /// <summary>
            /// 仅 Shader。
            /// </summary>
            Shader = 5,

            /// <summary>
            /// 仅 AnimationClip。
            /// </summary>
            AnimationClip = 6,

            /// <summary>
            /// 仅 AudioClip。
            /// </summary>
            AudioClip = 7,

            /// <summary>
            /// 仅 Font。
            /// </summary>
            Font = 8,

            /// <summary>
            /// 仅 TextAsset。
            /// </summary>
            TextAsset = 9,

            /// <summary>
            /// 仅 ScriptableObject。
            /// </summary>
            ScriptableObject = 10,
        }

        /// <summary>
        /// 分类模式下单个对象的采样数据。
        /// </summary>
        private sealed class ObjectSample
        {
            /// <summary>
            /// 对象名称。
            /// </summary>
            public string Name;

            /// <summary>
            /// 类型名称。
            /// </summary>
            public string Type;

            /// <summary>
            /// 运行时内存大小（字节）。
            /// </summary>
            public long Size;

            /// <summary>
            /// 是否是重复项（与前一条记录同名同类同大小），重复项以黄色高亮显示。
            /// </summary>
            public bool Highlight;

            /// <summary>
            /// 用名称、类型、大小初始化 ObjectSample。
            /// </summary>
            /// <param name="name">
            /// 对象名称。
            /// </param>
            /// <param name="type">
            /// 类型名称。
            /// </param>
            /// <param name="size">
            /// 运行时内存大小（字节）。
            /// </param>
            public ObjectSample(string name, string type, long size)
            {
                Name = name;
                Type = type;
                Size = size;
            }
        }

        /// <summary>
        /// Profiler 开关值变化回调，同步 DebugContent 的激活状态。
        /// </summary>
        /// <param name="isOn">
        /// 当前 Toggle 状态。
        /// </param>
        public void OnProfilerToggleValueChanged(bool isOn)
        {
            DebugContent.SetActive(isOn);
        }


        /// <summary>
        /// Memory 开关值变化回调，同步 MemorySummaryBlockPrefab 的激活状态。
        /// </summary>
        /// <param name="isOn">
        /// 当前 Toggle 状态。
        /// </param>
        public void OnMemoryToggleValueChanged(bool isOn)
        {
            MemorySummaryBlockPrefab.gameObject.SetActive(isOn);
        }
    }
}
