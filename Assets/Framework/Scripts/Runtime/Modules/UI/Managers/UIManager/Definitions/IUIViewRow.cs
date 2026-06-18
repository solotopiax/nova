/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IUIViewRow.cs
 * author:    taoye
 * created:   2026/4/26
 * descrip:   UI 视图数据行接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图数据行接口。
    /// Luban 生成的 UI 视图 bean 类须实现此接口，框架侧通过接口直接访问字段，彻底消除反射。
    /// </summary>
    public interface IUIViewRow
    {
        /// <summary>
        /// 视图类名（作为注册表 Key，与 typeof(T).Name 对齐）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 视图资源地址（格式由业务层约定，框架侧透传）。
        /// </summary>
        string AssetLocation { get; }

        /// <summary>
        /// 视图所属的视图分组名称。
        /// </summary>
        string UIGroupName { get; }

        /// <summary>
        /// 是否暂停被当前视图覆盖的下层视图。
        /// </summary>
        bool PauseCoveredUIView { get; }

        /// <summary>
        /// 是否启用对象池缓存。
        /// true 表示关闭后回收到对象池等待复用，false 表示关闭后直接销毁。
        /// </summary>
        bool InObjectPools { get; }
    }
}
