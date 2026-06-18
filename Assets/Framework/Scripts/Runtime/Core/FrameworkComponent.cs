/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FrameworkComponent.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架组件抽象类
 ***************************************************************/

using System.Collections;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架组件抽象类，所有框架组件都应继承该类。
    /// 组件在 Awake 阶段会第一时间注册到 FrameworkComponentsGroup。
    /// </summary>
    public abstract class FrameworkComponent : MonoBehaviour, ICoroutineRunner
    {
        /// <summary>
        /// 当前场景快照中的开发模式。
        /// 由 ConfigWindow 导出时自动回写，不允许在 Inspector 中手动编辑。
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private DevelopMode m_DevelopMode = DevelopMode.Debug;
        public DevelopMode DevelopMode => m_DevelopMode;

        /// <summary>
        /// 在对象实例化时被 Unity 调用，用于第一时间注册框架组件。
        /// 子类可以重写该方法，但建议在重写时调用 base.Awake() 以确保组件注册。
        /// </summary>
        protected virtual void Awake()
        {
            FrameworkComponentsGroup.RegisterComponent(this);
        }

        /// <summary>
        /// 开始协程（ICoroutineRunner 显式实现，外部通过接口调用）。
        /// </summary>
        /// <param name="coroutine">协程方法。</param>
        /// <returns>协程对象。</returns>
        Coroutine ICoroutineRunner.StartCoroutine(IEnumerator coroutine)
        {
            return base.StartCoroutine(coroutine);
        }

        /// <summary>
        /// 停止协程（ICoroutineRunner 显式实现，外部通过接口调用）。
        /// </summary>
        /// <param name="coroutine">协程对象。</param>
        void ICoroutineRunner.StopCoroutine(Coroutine coroutine)
        {
            if (coroutine != null)
            {
                base.StopCoroutine(coroutine);
            }
        }
    }
}
