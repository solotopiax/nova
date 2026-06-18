/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ILogHelper.cs
 * author:    taoye
 * created:   2025/11/28
 * descrip:   日志助手功能对外接口
 *            定义了日志记录的统一接口，保证业务模块与具体日志实现解耦
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 日志助手接口。
    /// </summary>
    public interface ILogHelper
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 输出日志。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="tag">日志标签。</param>
        /// <param name="message">日志内容。</param>
        void Print(LogLevel level, string tag, object message);
    }
}
