/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryRowView.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Memory Summary 虚拟滚动行视图：承载三列独立 Label，数据源是 MemoryRowData。
    /// </summary>
    public class MemoryRowView : DebugMonoBehaviourEx, IVirtualView
    {
        /// <summary>
        /// Inspector 引用：名称列 Text 组件。
        /// </summary>
        [RequiredField] public Text NameLabel;

        /// <summary>
        /// Inspector 引用：类型列 Text 组件。
        /// </summary>
        [RequiredField] public Text TypeLabel;

        /// <summary>
        /// Inspector 引用：大小列 Text 组件。
        /// </summary>
        [RequiredField] public Text SizeLabel;


        /// <summary>
        /// IVirtualView 契约：VVLG 把绑定的 MemoryRowData 数据回填到三列 Label。
        /// Name/Type/Size 各列独立赋值；Highlight 为 true 时 NameLabel 文字变黄。
        /// </summary>
        /// <param name="data">
        /// 绑定的数据对象，实际运行时由 ProfilerTabController 塞入 MemoryRowData。
        /// </param>
        public void SetDataContext(object data)
        {
            if (data is not MemoryRowData row)
            {
                NameLabel.text = string.Empty;
                TypeLabel.text = string.Empty;
                SizeLabel.text = string.Empty;
                return;
            }

            NameLabel.text = row.Name ?? string.Empty;
            TypeLabel.text = row.Type ?? string.Empty;
            SizeLabel.text = row.Size ?? string.Empty;
            NameLabel.color = row.Highlight ? Color.yellow : Color.white;
        }
    }
}
