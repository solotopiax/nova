/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ErrorNotifier.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using UnityEngine;

namespace NovaFramework.Runtime
{
    public class ErrorNotifier : MonoBehaviour
    {
        public bool IsVisible
        {
            get { return _isShowing; }
        }

        private const float DisplayTime = 6;

        [SerializeField]
        private Animator _animator = null;

        private int _triggerHash;

        private float _hideTime;
        private bool _isShowing;

        private bool _queueWarning;

        void Awake()
        {
            _triggerHash = Animator.StringToHash("Display");
        }

        public void ShowErrorWarning()
        {
            _queueWarning = true;
        }

        void Update()
        {
            if (_queueWarning)
            {
                _hideTime = Time.realtimeSinceStartup + DisplayTime;

                if (!_isShowing)
                {
                    _isShowing = true;
                    _animator.SetBool(_triggerHash, true);
                }

                _queueWarning = false;
            }

            if (_isShowing && Time.realtimeSinceStartup > _hideTime)
            {
                _animator.SetBool(_triggerHash, false);
                _isShowing = false;
            }
        }
    }
}