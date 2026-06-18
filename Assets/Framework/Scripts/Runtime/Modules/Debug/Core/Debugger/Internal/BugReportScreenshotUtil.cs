/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BugReportScreenshotUtil.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System.Collections;
    using UnityEngine;

    public class BugReportScreenshotUtil
    {
        public static byte[] ScreenshotData;

        public static IEnumerator ScreenshotCaptureCo()
        {
            if (ScreenshotData != null)
            {
                Debug.LogWarning("[RuntimeDebugger] Warning, overriding existing screenshot data.");
            }

            yield return new WaitForEndOfFrame();

            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();

            ScreenshotData = tex.EncodeToPNG();

            Object.Destroy(tex);
        }
    }
}
