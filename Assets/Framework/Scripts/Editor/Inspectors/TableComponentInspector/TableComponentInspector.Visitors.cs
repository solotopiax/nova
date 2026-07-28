/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   TableComponent Inspector 序列化属性
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        private SerializedProperty m_CurManagerTypeName;
        private SerializedProperty m_Setting;
        private SerializedProperty m_Project;
        private SerializedProperty m_Runtime;
        private List<string> m_ManagerTypeNames;
        private bool m_RuntimeTablesFoldout;
    }
}
