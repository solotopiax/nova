/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.UIBindings.Models.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   已有 UIView 控件绑定的精确输入、只读计划与保存回执
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>为已有普通 Prefab 的 UIView 提供可选字段生成与精确绑定，不创建布局或修改业务监听。</summary>
        public static partial class UIBindings
        {
            /// <summary>恢复不完整异常 Data 中的固定目录键；调用方可展示此路径，不解析异常文本。</summary>
            public const string RecoveryPathDataKey = "Nova.UIBindings.RecoveryPath";

            /// <summary>一个字段和一个实际对象的精确绑定，不把节点名称或整数位置作为对象身份。</summary>
            [Serializable]
            public sealed class Binding
            {
                /// <summary>业务 UIView 中的直接序列化字段名。</summary>
                public string FieldName;
                /// <summary>Inspect 返回的目标 GameObject 或 Component 的 GlobalObjectId 字符串。</summary>
                public string TargetObjectId;
                /// <summary>字段的精确 AssemblyQualifiedName；生成时也以此声明类型。</summary>
                public string TypeName;
            }

            /// <summary>一次只处理同一普通 Prefab 内同一个 UIView 的完整字段集合。</summary>
            [Serializable]
            public sealed class BindingRequest
            {
                /// <summary>项目自有 Assets 内已存在的普通 Prefab 路径。</summary>
                public string PrefabPath;
                /// <summary>Inspect 返回的业务 UIView 的 GlobalObjectId 字符串。</summary>
                public string ViewObjectId;
                /// <summary>一至一百二十八个明确字段；未列出的字段和全部事件保持原样。</summary>
                public Binding[] Bindings;
            }

            /// <summary>实际 Prefab 对象的稳定身份和逐段层级名；同名对象仍各有不同标识。</summary>
            [Serializable]
            public sealed class ObjectInfo
            {
                /// <summary>当前资产对象的 GlobalObjectId 字符串。</summary>
                public string ObjectId;
                /// <summary>从根到目标的逐段原始名称，名称中的斜线不会被当作分隔符。</summary>
                public string[] HierarchyNames;
                /// <summary>实际对象的 AssemblyQualifiedName。</summary>
                public string TypeName;
            }

            /// <summary>业务 UIView 的一个直接对象引用字段。</summary>
            [Serializable]
            public sealed class FieldInfo
            {
                /// <summary>真实字段名。</summary>
                public string FieldName;
                /// <summary>字段声明类型的 AssemblyQualifiedName。</summary>
                public string TypeName;
                /// <summary>当前对象引用的 GlobalObjectId；空引用为空字符串。</summary>
                public string CurrentObjectId;
                /// <summary>字段是否保存了失效引用；失效引用不能被工具悄悄替换。</summary>
                public bool MissingReference;
            }

            /// <summary>一个业务 UIView 的真实脚本、直接字段和当前写入限制。</summary>
            [Serializable]
            public sealed class ViewInfo
            {
                /// <summary>业务 UIView 的 GlobalObjectId。</summary>
                public string ObjectId;
                /// <summary>实际业务类型的 AssemblyQualifiedName。</summary>
                public string TypeName;
                /// <summary>实际 MonoScript 路径。</summary>
                public string ScriptPath;
                /// <summary>可供精确绑定的直接对象引用字段。</summary>
                public FieldInfo[] Fields;
                /// <summary>为空表示当前通过绑定安全检查；生成另须检查 partial 和文件归属。</summary>
                public string BlockedReason;
            }

            /// <summary>只读查询结果；取得对象和字段不表示已保存、编译或运行。</summary>
            [Serializable]
            public sealed class Inspection
            {
                /// <summary>实际 Prefab 路径。</summary>
                public string PrefabPath;
                /// <summary>实际 Prefab GUID。</summary>
                public string PrefabGuid;
                /// <summary>Prefab 全部 GameObject 与现有组件。</summary>
                public ObjectInfo[] Objects;
                /// <summary>Prefab 中已挂载的具体业务 UIView。</summary>
                public ViewInfo[] Views;
            }

            /// <summary>不可外部构造的单次绑定计划；请求副本和快照不会被调用方的数组修改。</summary>
            public sealed class BindingPlan
            {
                internal Snapshot Before;
                internal BindingRequest Input;
                internal int Consumed;
                internal int ChangeCount;
                /// <summary>返回精确输入的独立副本。</summary>
                public BindingRequest Request => CopyRequest(Input);
                /// <summary>当前空字段中实际需要赋值的数量；已绑定相同对象不算变更。</summary>
                public int ChangedFields => ChangeCount;
                /// <summary>计划时磁盘 Prefab 的完整 SHA256。</summary>
                public string PrefabHash => Before.PrefabHash;
                /// <summary>计划时全部 Prefab 对象序列化状态的摘要。</summary>
                public string StateHash => Before.StateHash;
                /// <summary>实际业务脚本路径。</summary>
                public string ScriptPath => Before.ScriptPath;
                /// <summary>计划时业务脚本完整字节摘要。</summary>
                public string ScriptHash => Before.ScriptHash;
                /// <summary>计划时业务脚本元数据摘要。</summary>
                public string ScriptMetaHash => Before.ScriptMetaHash;
                /// <summary>计划时实际业务程序集源码、定义文件及可选生成文件的独立快照。</summary>
                public SourceSnapshot[] Sources => CopySources(Before.Sources);
                /// <summary>仅由已完成整批校验的领域入口创建。</summary>
                internal BindingPlan() { }
            }

            /// <summary>不可外部构造的单次字段生成计划；Generate 只写这一个已核验文件。</summary>
            public sealed class GenerationPlan
            {
                internal Snapshot Before;
                internal BindingRequest Input;
                internal GeneratedField[] Fields;
                internal string Text;
                internal string ExistingHash;
                internal string ExistingMetaHash;
                internal int Consumed;
                internal string Path;
                internal SourceSnapshot[] ProtectedSources;
                /// <summary>业务 MonoScript 同目录中的准确生成路径。</summary>
                public string OutputPath => Path;
                /// <summary>带归属和正文摘要的完整拟生成内容。</summary>
                public string Content => Text;
                /// <summary>内容相同时为 false；无变化不会重写或刷新文件。</summary>
                public bool HasChanges => HashText(Text) != ExistingHash;
                /// <summary>返回精确输入的独立副本。</summary>
                public BindingRequest Request => CopyRequest(Input);
                /// <summary>计划时完整 Prefab 字节摘要。</summary>
                public string PrefabHash => Before.PrefabHash;
                /// <summary>计划时完整 Prefab 对象状态摘要。</summary>
                public string StateHash => Before.StateHash;
                /// <summary>实际业务脚本路径。</summary>
                public string ScriptPath => Before.ScriptPath;
                /// <summary>计划时业务脚本完整字节摘要。</summary>
                public string ScriptHash => Before.ScriptHash;
                /// <summary>计划时业务脚本元数据摘要。</summary>
                public string ScriptMetaHash => Before.ScriptMetaHash;
                /// <summary>当前业务程序集源码与定义文件的独立快照；生成文件本身由独立摘要保护。</summary>
                public SourceSnapshot[] Sources => CopySources(ProtectedSources);
                /// <summary>仅由已完成归属和冲突检查的领域入口创建。</summary>
                internal GenerationPlan() { }
            }

            /// <summary>精确字段保存回执；可序列化跨重载验证，不授予再次执行权限。</summary>
            [Serializable]
            public sealed class BindingReceipt
            {
                /// <summary>本次完整输入的独立副本。</summary>
                public BindingRequest Request;
                /// <summary>真正赋值并保存的字段数量。</summary>
                public int ChangedFields;
                /// <summary>保存后 Prefab 完整字节摘要。</summary>
                public string PrefabHash;
                /// <summary>保存后 Prefab 元数据摘要。</summary>
                public string PrefabMetaHash;
                /// <summary>实际业务 MonoScript 路径。</summary>
                public string ScriptPath;
                /// <summary>实际业务源码字节摘要。</summary>
                public string ScriptHash;
                /// <summary>业务脚本元数据摘要。</summary>
                public string ScriptMetaHash;
                /// <summary>保存时业务程序集与可选生成 partial 的完整来源快照。</summary>
                public SourceSnapshot[] Sources;
            }

            /// <summary>字段源码生成回执；不表示 Unity 已导入、编译或完成 Prefab 绑定。</summary>
            [Serializable]
            public sealed class GenerationReceipt
            {
                /// <summary>生成文件的准确项目相对路径。</summary>
                public string OutputPath;
                /// <summary>生成文件的完整字节摘要。</summary>
                public string OutputHash;
                /// <summary>实际业务 MonoScript 路径。</summary>
                public string ScriptPath;
                /// <summary>生成时业务源码的完整字节摘要。</summary>
                public string ScriptHash;
                /// <summary>业务脚本 GUID。</summary>
                public string ScriptGuid;
                /// <summary>本次是否真正写入源码；无变化不会修改时间戳。</summary>
                public bool Changed;
                /// <summary>调用方必须完成 Unity 导入、编译和重载，再创建新绑定计划。</summary>
                public bool RequiresCompilation;
                /// <summary>生成前受保护的业务程序集源码及定义文件；生成文件本身不在此数组中。</summary>
                public SourceSnapshot[] Sources;
            }

            /// <summary>生成所依赖的现有源码或程序集定义文件的只读字节快照。</summary>
            [Serializable]
            public sealed class SourceSnapshot
            {
                /// <summary>当前工程中的准确源文件路径。</summary>
                public string Path;
                /// <summary>原文件完整字节摘要。</summary>
                public string Hash;
                /// <summary>原元数据摘要；没有元数据时为 null。</summary>
                public string MetaHash;
            }

            /// <summary>只在当前进程使用的完整输入快照；不会变成可重复执行的外部令牌。</summary>
            internal sealed class Snapshot
            {
                internal string PrefabHash;
                internal string PrefabMetaHash;
                internal string StateHash;
                internal string ScriptPath;
                internal string ScriptHash;
                internal string ScriptMetaHash;
                internal SourceSnapshot[] Sources;
            }

            /// <summary>由受保护生成文件或当前精确请求取得的字段定义。</summary>
            internal sealed class GeneratedField
            {
                internal string Name;
                internal Type Type;
            }
        }
    }
}
