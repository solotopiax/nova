/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileMenuController.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    public class MobileMenuController : DebugMonoBehaviourEx
    {
        private UnityEngine.UI.Button _closeButton;

        [SerializeField] private float _maxMenuWidth = 185f;

        [SerializeField] private float _peekAmount = 45f;

        private float _targetXPosition;

        [RequiredField] public RectTransform Content;

        [RequiredField] public RectTransform Menu;

        [RequiredField] public UnityEngine.UI.Button OpenButton;

        [RequiredField] public DebugTabController TabController;

        public float PeekAmount
        {
            get { return _peekAmount; }
        }

        public float MaxMenuWidth
        {
            get { return _maxMenuWidth; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            var parent = Menu.parent as RectTransform;

            var layoutElement = Menu.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            // Anchor the menu off the left edge so opening the panel slides it into view.
            Menu.anchorMin = new Vector2(0, 1);
            Menu.anchorMax = new Vector2(0, 1);
            Menu.pivot = new Vector2(1, 1);

            Menu.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Clamp(parent.rect.width - PeekAmount, 0, MaxMenuWidth));

            Menu.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, parent.rect.height);

            Menu.anchoredPosition = new Vector2(0, 0);

            if (_closeButton == null)
            {
                CreateCloseButton();
            }

            OpenButton.gameObject.SetActive(true);

            TabController.ActiveTabChanged += TabControllerOnActiveTabChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            var layoutElement = Menu.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = false;

            // Reset content position in case it has been moved by opening the menu
            Content.anchoredPosition = new Vector2(0, 0);

            _closeButton.gameObject.SetActive(false);
            OpenButton.gameObject.SetActive(false);

            TabController.ActiveTabChanged -= TabControllerOnActiveTabChanged;
        }

        private void CreateCloseButton()
        {
            var go = new GameObject("RuntimeDebuggerCloseButtonCanvas", typeof(RectTransform));
            go.transform.SetParent(Content, false);
            var c = go.AddComponent<Canvas>();
            go.AddComponent<GraphicRaycaster>();
            var rect = go.GetComponentOrAdd<RectTransform>();

            c.overrideSorting = true;
            c.sortingOrder = 122;

            go.AddComponent<LayoutElement>().ignoreLayout = true;

            SetRectSize(rect);

            var cGo = new GameObject("RuntimeDebuggerCloseButton", typeof(RectTransform));
            cGo.transform.SetParent(rect, false);
            var cRect = cGo.GetComponent<RectTransform>();

            SetRectSize(cRect);
            cGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            _closeButton = cGo.AddComponent<UnityEngine.UI.Button>();
            _closeButton.transition = Selectable.Transition.None;
            _closeButton.onClick.AddListener(CloseButtonClicked);
            _closeButton.gameObject.SetActive(false);
        }

        private void SetRectSize(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Content.rect.width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Content.rect.height);
        }

        private void CloseButtonClicked()
        {
            Close();
        }

        protected override void Update()
        {
            base.Update();

            var from = Content.anchoredPosition.x;

            if (Mathf.Abs(_targetXPosition - from) < 2.5f)
            {
                Content.anchoredPosition = new Vector2(_targetXPosition, Content.anchoredPosition.y);
            }
            else
            {
                Content.anchoredPosition =
                    new Vector2(DebugMath.SpringLerp(from, _targetXPosition, 15f, Time.unscaledDeltaTime),
                        Content.anchoredPosition.y);
            }
        }

        private void TabControllerOnActiveTabChanged(DebugTabController srTabController, DebugTab srTab)
        {
            Close();
        }

        [ContextMenu("Open")]
        public void Open()
        {
            _targetXPosition = Menu.rect.width;
            _closeButton.gameObject.SetActive(true);
        }

        [ContextMenu("Close")]
        public void Close()
        {
            _targetXPosition = 0;
            _closeButton.gameObject.SetActive(false);
        }
    }
}
