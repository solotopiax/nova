/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BugReportService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;

    [Service(typeof (IBugReportService))]
    class BugReportService : IBugReportService
    {
        private IBugReporterHandler _handler;

        public bool IsUsable
        {
            get
            {
                return _handler != null && _handler.IsUsable;
            }
        }

        public void SetHandler(IBugReporterHandler handler)
        {
            Debug.LogFormat("[RuntimeDebugger] Bug Report handler set to {0}", handler);
            _handler = handler;
        }

        public void SendBugReport(BugReport report, BugReportCompleteCallback completeHandler,
            IProgress<float> progress = null)
        {
            if (_handler == null)
            {
                throw new InvalidOperationException("No bug report handler has been configured.");
            }

            if (!_handler.IsUsable)
            {
                throw new InvalidOperationException("Bug report handler is not usable.");
            }

            if (report == null)
            {
                throw new ArgumentNullException("report");
            }

            if (completeHandler == null)
            {
                throw new ArgumentNullException("completeHandler");
            }
            
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                completeHandler(false, "No Internet Connection");
                return;
            }

            _handler.Submit(report, result => completeHandler(result.IsSuccessful, result.ErrorMessage), progress);
        }
    }
}
