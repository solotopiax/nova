/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  IAPPluginConfigDrawer.cs
 * author:    yingzheng
 * created:   2026/6/3
 * descrip:   IAPPluginConfig 自定义 PropertyDrawer，依次绘制所有配置字段
 ***************************************************************/

using UnityEditor;
using UnityEngine;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// IAPPluginConfig 的自定义 PropertyDrawer。
    /// 依次绘制所有配置字段（通过各自 PropertyDrawer），商品列表由 IAPProductListDrawer 负责渲染。
    /// </summary>
    [CustomPropertyDrawer(typeof(IAPPluginConfig))]
    public sealed class IAPPluginConfigDrawer : PropertyDrawer
    {
        /// <summary>
        /// 计算整个 IAPPluginConfig 属性的总高度：所有可见子属性的高度之和（含 tooltip HelpBox）。
        /// </summary>
        /// <param name="property">对应 IAPPluginConfig 的 SerializedProperty。</param>
        /// <param name="label">显示标签（保留供基类接口使用）。</param>
        /// <returns>整个属性绘制所需的像素高度。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = 0f;
            SerializedProperty iter = property.Copy();
            SerializedProperty end = property.GetEndProperty();

            // 进入子属性层（第一次传 true，后续传 false 不继续展开子树）
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iter, end))
                {
                    break;
                }

                h += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
                if (!string.IsNullOrEmpty(iter.tooltip))
                {
                    float helpH = EditorStyles.helpBox.CalcHeight(new GUIContent(iter.tooltip), EditorGUIUtility.currentViewWidth - 32f);
                    h += helpH + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return h;
        }

        /// <summary>
        /// 绘制整个 IAPPluginConfig 属性：逐一绘制所有可见子属性（含各字段自身 PropertyDrawer）。
        /// </summary>
        /// <param name="position">Inspector 分配的绘制 Rect。</param>
        /// <param name="property">对应 IAPPluginConfig 的 SerializedProperty。</param>
        /// <param name="label">显示标签。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float y = position.y;

            // 绘制所有可见子属性
            SerializedProperty iter = property.Copy();
            SerializedProperty end = property.GetEndProperty();

            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iter, end))
                {
                    break;
                }

                float propH = EditorGUI.GetPropertyHeight(iter, true);
                Rect propRect = new Rect(position.x, y, position.width, propH);
                // 通过 EditorUtil.Draw Rect 版 PropertyField 绘制（含各子属性自身的 PropertyDrawer）
                NovaFramework.Editor.EditorUtil.Draw.PropertyField(propRect, iter, iter.displayName, true, false);
                y += propH + EditorGUIUtility.standardVerticalSpacing;
                if (!string.IsNullOrEmpty(iter.tooltip))
                {
                    float helpH = EditorStyles.helpBox.CalcHeight(new GUIContent(iter.tooltip), position.width);
                    Rect helpRect = new Rect(position.x, y, position.width, helpH);
                    NovaFramework.Editor.EditorUtil.Draw.HelpBox(helpRect, MessageType.Info, iter.tooltip, false);
                    y += helpH + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            EditorGUI.EndProperty();
        }

    }
}
