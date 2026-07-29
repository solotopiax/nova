/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   TableComponent Inspector 序列化属性
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        private SerializedProperty m_CurManagerTypeName;
        private SerializedProperty m_Setting;
        private SerializedProperty m_Projects;
        private SerializedProperty m_LoadDescriptions;
        private List<string> m_ManagerTypeNames;
        private readonly Dictionary<string, TableProjectModel> m_ProjectModels = new Dictionary<string, TableProjectModel>();
        private readonly Dictionary<string, TableProjectConfigState> m_ProjectConfigStates =
            new Dictionary<string, TableProjectConfigState>();
        private readonly List<string> m_WatchedProjectDirectories = new List<string>();
        private readonly Dictionary<string, int> m_TablePickerModes = new Dictionary<string, int>();
        private Action m_ProjectFileWatcherCallback;
        private bool m_RuntimeTablesFoldout;
    }
}
