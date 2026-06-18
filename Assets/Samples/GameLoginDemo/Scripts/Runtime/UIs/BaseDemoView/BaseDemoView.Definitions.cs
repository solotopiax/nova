/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BaseDemoView.Definitions.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 基础 View — 嵌套类型定义
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameLogin.Samples.Runtime
{
    /// <summary>
    /// Demo 基础 View 基类，三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// </summary>
    public partial class BaseDemoView
    {
        /// <summary>
        /// 反馈日志等级，决定日志行的显示颜色。
        /// Info=#CCCCCC / Success=#4CAF50 / Warn=#FFB300 / Error=#E53935。
        /// </summary>
        public enum FeedbackLevel
        {
            /// <summary>
            /// 普通信息，颜色 #CCCCCC。
            /// </summary>

            Info,
            /// <summary>
            /// 成功反馈，颜色 #4CAF50。
            /// </summary>

            Success,
            /// <summary>
            /// 警告反馈，颜色 #FFB300。
            /// </summary>

            Warn,
            /// <summary>
            /// 错误反馈，颜色 #E53935。
            /// </summary>

            Error
        }
    }
}
