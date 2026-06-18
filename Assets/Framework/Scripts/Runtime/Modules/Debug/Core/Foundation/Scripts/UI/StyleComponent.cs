/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StyleComponent.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.UI;

    [ExecuteInEditMode]
    [AddComponentMenu(ComponentMenuPaths.StyleComponent)]
    public class StyleComponent : DebugMonoBehaviour
    {
        private Style _activeStyle;
        private StyleRoot _cachedRoot;
        private Graphic _graphic;
        private bool _hasStarted;
        private Image _image;
        private bool _pendingRefresh;
        private Selectable _selectable;

        [SerializeField] [FormerlySerializedAs("StyleKey")] [HideInInspector] private string _styleKey;

        public bool IgnoreImage = false;

        public string StyleKey
        {
            get { return _styleKey; }
            set
            {
                _styleKey = value;
                Refresh(false);
            }
        }

        private void Start()
        {
            Refresh(true);
            _hasStarted = true;
        }

        private void OnEnable()
        {
            if (_hasStarted || !string.IsNullOrEmpty(StyleKey))
            {
                Refresh(false);
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyStyle();
                return;
            }
#endif

            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                Refresh(true);
            }
        }

        private void OnTransformParentChanged()
        {
            _cachedRoot = null;

            if (!string.IsNullOrEmpty(StyleKey))
            {
                _pendingRefresh = true;
            }
        }

        public void Refresh(bool invalidateCache)
        {
            if (string.IsNullOrEmpty(StyleKey))
            {
                _activeStyle = null;
                return;
            }

            if (!isActiveAndEnabled)
            {
                _cachedRoot = null;
                return;
            }

            if (_cachedRoot == null || invalidateCache)
            {
                _cachedRoot = GetStyleRoot();
            }

            if (_cachedRoot == null)
            {
                _pendingRefresh = true;
                _activeStyle = null;
                return;
            }

            var s = _cachedRoot.GetStyle(StyleKey);

            if (s == null)
            {
                Debug.LogWarning("[StyleComponent] Style not found ({0})".Fmt(StyleKey), this);
                _activeStyle = null;
                return;
            }

            _activeStyle = s;
            _pendingRefresh = false;
            ApplyStyle();
        }

        /// <summary>
        /// Find the nearest enable style root component in parents
        /// </summary>
        /// <returns></returns>
        private StyleRoot GetStyleRoot()
        {
            var t = CachedTransform;
            StyleRoot root;

            var i = 0;

            do
            {
                root = t.GetComponentInParent<StyleRoot>();

                if (root != null)
                {
                    t = root.transform.parent;
                }

                ++i;

                if (i > 100)
                {
                    Debug.LogWarning("Breaking Loop");
                    break;
                }
            } while ((root != null && !root.enabled) && t != null);

            return root;
        }

        private void ApplyStyle()
        {
            if (_activeStyle == null)
            {
                return;
            }

            if (_graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }

            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }

            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (!IgnoreImage && _image != null)
            {
                _image.sprite = _activeStyle.Image;
                _image.SetAllDirty();

                var rectTransform = _image.transform as RectTransform;
                if (rectTransform != null)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
                }
            }

            if (_selectable != null)
            {
                var colours = _selectable.colors;
                colours.normalColor = _activeStyle.NormalColor;
                colours.highlightedColor = _activeStyle.HoverColor;
                colours.pressedColor = _activeStyle.ActiveColor;
                colours.disabledColor = _activeStyle.DisabledColor;
                colours.colorMultiplier = 1f;

                _selectable.colors = colours;

                if (_graphic != null)
                {
                    _graphic.color = Color.white;
                }
            }
            else if (_graphic != null)
            {
                _graphic.color = _activeStyle.NormalColor;
            }
        }

        private void DebugStyleDirty()
        {
            // If inactive, invalidate the cached root and return. Next time it is enabled
            // a new root will be found
            if (!CachedGameObject.activeInHierarchy)
            {
                _cachedRoot = null;
                return;
            }

            Refresh(true);
        }
    }
}
