/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIViewOpenResult.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   可选页面打开等待结果
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>单次页面打开请求的终态；不会改变旧序号入口的返回方式。</summary>
    public enum UIViewOpenStatus { Failed, Opened, Cancelled, Closed, Shutdown }

    /// <summary>页面打开终态的只读快照；不拥有视图，也不保证池化视图之后仍属于本次请求。</summary>
    public readonly struct UIViewOpenResult
    {
        /// <summary>建立一次结果；仅 Opened 可携带非空视图，序号未分配时使用 -1。</summary>
        /// <param name="status">准确的请求终态。</param>
        /// <param name="serialID">本次请求序号。</param>
        /// <param name="view">仅成功时提供的视图。</param>
        /// <param name="errorMessage">失败或结束原因。</param>
        public UIViewOpenResult(UIViewOpenStatus status, int serialID, IUIView view = null, string errorMessage = null)
        {
            if (status == UIViewOpenStatus.Opened && view == null)
                throw new ArgumentNullException(nameof(view));
            if (status != UIViewOpenStatus.Opened && view != null)
                throw new ArgumentException("只有打开成功的结果可以携带视图。", nameof(view));
            Status = status;
            SerialID = serialID;
            View = view;
            ErrorMessage = errorMessage;
        }

        /// <summary>获取单次请求终态。</summary>
        public UIViewOpenStatus Status { get; }
        /// <summary>获取请求序号；未产生请求时为 -1。</summary>
        public int SerialID { get; }
        /// <summary>获取成功完成时的视图快照，使用前须核对其当前序号及生命周期。</summary>
        public IUIView View { get; }
        /// <summary>获取失败或结束原因。</summary>
        public string ErrorMessage { get; }
        /// <summary>获取本次请求是否完成打开；不表示页面之后始终保持打开。</summary>
        public bool Succeeded => Status == UIViewOpenStatus.Opened && View != null;
    }
}
