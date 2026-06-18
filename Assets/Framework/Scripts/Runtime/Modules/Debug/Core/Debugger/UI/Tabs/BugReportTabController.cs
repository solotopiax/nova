/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BugReportTabController.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    public class BugReportTabController : DebugMonoBehaviourEx, IEnableTab
    {
        [RequiredField] public BugReportSheetController BugReportSheetPrefab;

        [RequiredField] public RectTransform Container; 

        public bool IsEnabled
        {
            get { return DebugServiceRegistry.GetService<IBugReportService>().IsUsable; }
        }
        
        protected override void Start()
        {
            base.Start();

            var sheet = DebugInstantiate.Instantiate(BugReportSheetPrefab);
            sheet.IsCancelButtonEnabled = false;

            // Callbacks when taking screenshot will hide the debug panel so it is not present in the image
            sheet.TakingScreenshot = TakingScreenshot;
            sheet.ScreenshotComplete = ScreenshotComplete;

            sheet.CachedTransform.SetParent(Container, false);
        }

        private void TakingScreenshot()
        {
            RuntimeDebugger.Instance.HideDebugPanel();
        }

        private void ScreenshotComplete()
        {
            RuntimeDebugger.Instance.ShowDebugPanel(false);
        }
    }
}
