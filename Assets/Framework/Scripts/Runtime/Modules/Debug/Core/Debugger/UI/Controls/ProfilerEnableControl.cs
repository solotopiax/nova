/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProfilerEnableControl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.Profiling;

    public class ProfilerEnableControl : DebugMonoBehaviourEx
    {
        private bool _previousState;
        [RequiredField] public Text ButtonText;
        [RequiredField] public UnityEngine.UI.Button EnableButton;
        [RequiredField] public Text Text;

        protected override void Start()
        {
            base.Start();

            if (!Profiler.supported)
            {
                Text.text = RuntimeDebuggerStrings.Current.Profiler_NotSupported;
                EnableButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            if (!Application.HasProLicense())
            {
                Text.text = RuntimeDebuggerStrings.Current.Profiler_NoProInfo;
                EnableButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            UpdateLabels();
        }

        protected void UpdateLabels()
        {
            if (!Profiler.enabled)
            {
                Text.text = RuntimeDebuggerStrings.Current.Profiler_EnableProfilerInfo;
                ButtonText.text = "Enable";
            }
            else
            {
                Text.text = RuntimeDebuggerStrings.Current.Profiler_DisableProfilerInfo;
                ButtonText.text = "Disable";
            }

            _previousState = Profiler.enabled;
        }

        protected override void Update()
        {
            base.Update();

            if (Profiler.enabled != _previousState)
            {
                UpdateLabels();
            }
        }

        public void ToggleProfiler()
        {
            Debug.Log("Toggle Profiler");
            Profiler.enabled = !Profiler.enabled;
        }
    }
}
