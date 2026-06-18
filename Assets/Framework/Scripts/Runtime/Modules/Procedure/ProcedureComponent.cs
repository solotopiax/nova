/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureComponent.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure组件
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Procedure组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ProcedureComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_ProcedureManager = Util.TypeCreator.Create<IProcedureManager>(m_CurManagerTypeName);
            if (m_ProcedureManager == null)
            {
                throw new InvalidOperationException("ProcedureManager 无效。");
            }
        }

        /// <summary>
        /// 开始。仅扫描 Framework 程序集中的 ProcedureBase 非抽象子类并初始化流程管理器。
        /// 业务 Procedure 通过 ProcedureLoadDll 在业务 DLL 加载后延迟注册。
        /// </summary>
        private void Start()
        {
            string[] typeNames = Util.Assembly.GetTypeNames(typeof(ProcedureBase), typeof(ProcedureBase).Assembly);
            if (typeNames == null || typeNames.Length == 0)
            {
                throw new InvalidOperationException("未发现任何 ProcedureBase 的具体子类。");
            }

            ProcedureBase[] procedures = new ProcedureBase[typeNames.Length];
            Type entranceType = null;

            for (int i = 0; i < typeNames.Length; i++)
            {
                Type type = Util.Assembly.GetType(typeNames[i]);
                if (type == null)
                {
                    throw new InvalidOperationException(Txt.Format("无法获取流程类型 {0}。", typeNames[i]));
                }

                ProcedureBase procedure;
                try
                {
                    procedure = (ProcedureBase)Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(Txt.Format("流程类型 {0} 实例化失败，请确认其具有无参构造器。", typeNames[i]), ex);
                }
                procedures[i] = procedure;

                if (typeNames[i] == m_EntranceProcedureTypeName)
                {
                    entranceType = type;
                }
            }

            if (entranceType == null)
            {
                throw new InvalidOperationException(Txt.Format("入口流程 {0} 无效。", m_EntranceProcedureTypeName));
            }

            m_ProcedureManager.Initialize(new ProcedureManagerConfig
            {
                Procedures = procedures,
                EntranceProcedureType = entranceType,
            });
        }

        /// <summary>
        /// 是否存在指定类型的流程。
        /// </summary>
        /// <typeparam name="T">要检查的流程类型。</typeparam>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public bool HasProcedure<T>() where T : ProcedureBase
        {
            return m_ProcedureManager.HasProcedure<T>();
        }

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <typeparam name="T">要获取的流程类型。</typeparam>
        /// <returns>流程实例。</returns>
        public ProcedureBase GetProcedure<T>() where T : ProcedureBase
        {
            return m_ProcedureManager.GetProcedure<T>();
        }

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <param name="procedureType">要获取的流程类型。</param>
        /// <returns>流程实例。</returns>
        public ProcedureBase GetProcedure(Type procedureType)
        {
            return m_ProcedureManager.GetProcedure(procedureType);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 每帧轮询当前 Procedure 类型，驱动跳转历史采集。
        /// </summary>
        private void Update()
        {
            string curName = m_ProcedureManager?.CurrentProcedure?.GetType().FullName;
            RecordProcedureTransition(curName);
        }
#endif

        /// <summary>
        /// 销毁。
        /// </summary>
        private void OnDestroy()
        {
            m_ProcedureManager = null;
        }
    }
}
