/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataTableUnitSettingBase.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   数据表单元设置基类
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 数据表单元设置基类，封装 IDataTableUnitSetting 接口中各模块共用的字段与显式实现。
    /// 子类只需提供 Mode 和 IndexField，以及可选地重写 LubanInputPath（特殊路径规则时）。
    /// </summary>
    [Serializable]
    public abstract class DataTableUnitSettingBase : IDataTableUnitSetting
    {
#if UNITY_EDITOR
        /// <summary>
        /// 相对数据源目录的文件路径，用于唯一标识该文件。
        /// </summary>
        public string SourcePath;
        /// <inheritdoc />
        string IDataTableUnitSetting.SourcePath => SourcePath;

        /// <summary>
        /// 该数据源的导出数据文件目标路径。
        /// </summary>
        public string DatasExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.DatasExportPath => DatasExportPath;

        /// <summary>
        /// 该数据源的导出类型定义文件目标路径。
        /// </summary>
        public string ClassesExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.ClassesExportPath => ClassesExportPath;

        /// <inheritdoc />
        /// <summary>
        /// 默认实现直接返回 SourcePath；若需要特殊路径（如 Config 的 _temp/ 前缀），子类重写此属性。
        /// </summary>
        string IDataTableUnitSetting.LubanInputPath => GetLubanInputPath();

        /// <summary>
        /// 返回 Luban __tables__.xml 中 input 属性的路径值。
        /// 默认返回 SourcePath，子类可重写以提供不同规则。
        /// </summary>
        /// <returns>
        /// Luban 输入路径字符串。
        /// </returns>
        protected virtual string GetLubanInputPath() => SourcePath;
#endif

        /// <summary>
        /// 资源的 Asset 地址。
        /// </summary>
        public string AssetLocation;
        /// <inheritdoc />
        string IDataTableUnitSetting.AssetLocation => AssetLocation;

        /// <inheritdoc />
        DataTableMode IDataTableUnitSetting.Mode => GetMode();

        /// <summary>
        /// 返回该单元使用的数据表模式（List / Map / One）。
        /// </summary>
        /// <returns>
        /// 数据表模式枚举值。
        /// </returns>
        protected abstract DataTableMode GetMode();

        /// <inheritdoc />
        string IDataTableUnitSetting.IndexField => GetIndexField();

        /// <summary>
        /// 返回映射模式的索引字段名。仅 Map 模式有意义，List 模式返回空字符串。
        /// </summary>
        /// <returns>
        /// 索引字段名字符串。
        /// </returns>
        protected abstract string GetIndexField();

        /// <summary>
        /// 数据类型短名称列表（不含命名空间），一个 JSON 可包含多个类型。
        /// </summary>
        public List<string> DataTypeNames = new List<string>();
        /// <inheritdoc />
        IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames => DataTypeNames;
    }
}
