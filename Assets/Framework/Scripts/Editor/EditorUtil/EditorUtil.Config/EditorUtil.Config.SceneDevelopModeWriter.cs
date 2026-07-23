/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.SceneDevelopModeWriter.cs
 * author:    Codex
 * created:   2026/6/9
 * descrip:   Config 导出后将 DevelopMode 场景快照回写到当前激活场景
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 将当前激活场景中所有 FrameworkComponent 的 DevelopMode 快照回写为导出时选中的值。
            /// </summary>
            public static class SceneDevelopModeWriter
            {
                /// <summary>
                /// 回写当前激活场景中的 DevelopMode 快照。
                /// </summary>
                /// <param name="developMode">导出时选中的开发模式。</param>
                public static void WriteActiveScene(DevelopMode developMode)
                {
                    WriteActiveSceneInternal(developMode, null);
                }

                /// <summary>
                /// 回写当前激活场景中的开发模式，并同步 AssetComponent 渠道快照。
                /// </summary>
                public static void WriteActiveScene(DevelopMode developMode, ChannelType channel)
                {
                    WriteActiveSceneInternal(developMode, channel);
                }

                private static void WriteActiveSceneInternal(
                    DevelopMode developMode,
                    ChannelType? channel)
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    if (!activeScene.IsValid() || !activeScene.isLoaded)
                    {
                        return;
                    }

                    bool isModified = false;
                    GameObject[] roots = activeScene.GetRootGameObjects();
                    for (int i = 0; i < roots.Length; i++)
                    {
                        FrameworkComponent[] components = roots[i].GetComponentsInChildren<FrameworkComponent>(true);
                        for (int j = 0; j < components.Length; j++)
                        {
                            FrameworkComponent component = components[j];
                            if (component == null)
                            {
                                continue;
                            }

                            SerializedObject serializedComponent = new SerializedObject(component);
                            SerializedProperty developModeProperty = serializedComponent.FindProperty("m_DevelopMode");
                            bool componentModified = false;
                            if (developModeProperty != null && developModeProperty.enumValueIndex != (int)developMode)
                            {
                                developModeProperty.enumValueIndex = (int)developMode;
                                componentModified = true;
                            }

                            if (channel.HasValue && component is AssetComponent)
                            {
                                SerializedProperty channelProperty = serializedComponent.FindProperty("m_Channel");
                                if (channelProperty != null && channelProperty.enumValueIndex != (int)channel.Value)
                                {
                                    channelProperty.enumValueIndex = (int)channel.Value;
                                    componentModified = true;
                                }
                            }

                            if (!componentModified) continue;
                            serializedComponent.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(component);
                            isModified = true;
                        }
                    }

                    if (isModified)
                    {
                        EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                }
            }
        }
    }
}
