/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugPanelRoot.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    public class DebugPanelRoot : DebugMonoBehaviourEx
    {
        private const string RaycastBlockerName = "RaycastBlocker";

        [RequiredField] public Canvas Canvas;

        [RequiredField] public CanvasGroup CanvasGroup;

        [RequiredField] public DebuggerTabController TabController;

        protected override void Awake()
        {
            base.Awake();
            EnsureRaycastBlocker();
        }

        public void Close()
        {
            if (Settings.Instance.UnloadOnClose)
            {
                DebugServiceRegistry.GetService<IDebugService>().DestroyDebugPanel();
            }
            else
            {
                DebugServiceRegistry.GetService<IDebugService>().HideDebugPanel();
            }
        }

        public void CloseAndDestroy()
        {
            DebugServiceRegistry.GetService<IDebugService>().DestroyDebugPanel();
        }

        private void EnsureRaycastBlocker()
        {
            if (Canvas == null)
            {
                return;
            }

            var canvasTransform = Canvas.transform;
            var existing = canvasTransform.Find(RaycastBlockerName);
            if (existing != null)
            {
                existing.SetAsFirstSibling();
                return;
            }

            var blocker = new GameObject(RaycastBlockerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blocker.layer = Canvas.gameObject.layer;

            var blockerTransform = blocker.GetComponent<RectTransform>();
            blockerTransform.SetParent(canvasTransform, false);
            blockerTransform.anchorMin = Vector2.zero;
            blockerTransform.anchorMax = Vector2.one;
            blockerTransform.offsetMin = Vector2.zero;
            blockerTransform.offsetMax = Vector2.zero;
            blockerTransform.SetAsFirstSibling();

            var image = blocker.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
        }
    }
}
