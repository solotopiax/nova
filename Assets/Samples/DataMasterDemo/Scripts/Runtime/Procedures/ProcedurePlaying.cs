/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedurePlaying.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   游戏进行流程
 *            职责：游戏主逻辑运行阶段。
 *            当前为模拟实现，仅输出日志标记进入主循环。
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Sdk.Datamaster.Samples.Runtime
{
    /// <summary>
    /// 游戏进行流程。游戏主逻辑运行阶段。
    /// </summary>
    public class ProcedurePlaying : ProcedureBase
    {
        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            Log.Debug(LogTag.Procedure, "ProcedurePlaying — 进入游戏主循环。");

            // 登录改由 DemoDataMasterView 的「模拟登录并拉取」按钮触发真实 kit-login：
            // 登录成功拿到真实 uid 后再 Nova.SDK.Login(uid)，DataMaster 据此携带该 uid 向服务端拉取。
            // 此处不再用假 uid 预登录，避免以无效 uid 发起拉取。

            int serialID = Nova.UI.OpenUIViewAsync<DemoDataMasterView>();
            if (serialID < 0)
            {
                Log.Error(LogTag.UI, "ProcedurePlaying — DemoDataMasterView 打开失败。");
            }
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
        }
    }
}
