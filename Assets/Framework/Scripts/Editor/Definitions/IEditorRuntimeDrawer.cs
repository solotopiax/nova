/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IEditorRuntimeDrawer.cs
 * author:    taoye
 * created:   2026/1/13
 * descrip:   编辑器运行时绘制接口
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 编辑器运行时绘制接口。
    /// </summary>
    public interface IEditorRuntimeDrawer : IDisposable
    {
        /// <summary>
        /// 绘制。
        /// </summary>
        /// <param name="target">目标对象。</param>
        void Draw(UnityEngine.Object target);
    }

    /// <summary>
    /// 编辑器运行时绘制泛型接口，提供类型安全的绘制入口。
    /// </summary>
    /// <typeparam name="TComponent">目标组件类型。</typeparam>
    public interface IEditorRuntimeDrawer<TComponent> : IEditorRuntimeDrawer where TComponent : MonoBehaviour
    {
        /// <summary>
        /// 类型安全的绘制方法。
        /// </summary>
        /// <param name="component">目标组件。</param>
        void DrawTyped(TComponent component);
    }
}
