/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProfilerMemoryBlock.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.Profiling;

    public class ProfilerMemoryBlock : DebugMonoBehaviourEx
    {
        private float _lastRefresh;

        [RequiredField] public Text CurrentUsedText;

        [RequiredField] public Slider Slider;

        [RequiredField] public Text TotalAllocatedText;

        protected override void OnEnable()
        {
            base.OnEnable();
            TriggerRefresh();
        }

        protected override void Update()
        {
            base.Update();

            if (RuntimeDebugger.Instance.IsDebugPanelVisible && (Time.realtimeSinceStartup - _lastRefresh > 1f))
            {
                TriggerRefresh();
                _lastRefresh = Time.realtimeSinceStartup;
            }
        }

        public void TriggerRefresh()
        {
            long max;
            long current;

            max = Profiler.GetTotalReservedMemoryLong();
            current = Profiler.GetTotalAllocatedMemoryLong();

            var maxMb = (max >> 10);
            maxMb /= 1024; // On new line to fix il2cpp

            var currentMb = (current >> 10);
            currentMb /= 1024;

            Slider.maxValue = maxMb;
            Slider.value = currentMb;

            TotalAllocatedText.text = "Reserved: <color=#FFFFFF>{0}</color>MB".Fmt(maxMb);
            CurrentUsedText.text = "<color=#FFFFFF>{0}</color>MB".Fmt(currentMb);
        }

        public void TriggerCleanup()
        {
            StartCoroutine(CleanUp());
        }

        private IEnumerator CleanUp()
        {
            GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();

            TriggerRefresh();
        }
    }
}
