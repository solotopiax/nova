/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RuntimeScriptRecompileHelper.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
#if UNITY_EDITOR
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Behaviour that supports RuntimeDebugger reloading itself after a script recompile is detected.
    /// </summary>
    public class RuntimeScriptRecompileHelper : MonoBehaviour
    {
        private static RuntimeScriptRecompileHelper _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("RuntimeDebugger Script Recompile Helper (Editor Only)");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            go.AddComponent<RuntimeScriptRecompileHelper>();
        }

        private bool _hasEnabled;
        private bool _debuggerHasInitialized;

        void OnEnable()
        {
            if(_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Don't take any action on the first OnEnable()
            if (!_hasEnabled)
            {
                _hasEnabled = true;
                return;
            }

            // Next OnEnable() will be due to script reload.
            AutoInitialize.OnLoadBeforeScene();

            if (_debuggerHasInitialized)
            {
                Debug.Log("[RuntimeScriptRecompileHelper] Restoring RuntimeDebugger after script reload.", this);
                RuntimeDebugger.Init();
            }
        }

        void OnApplicationQuit()
        {
            // Destroy this object when leaving play mode (otherwise it will linger and a new instance will be created next time play mode is entered).
            Destroy(gameObject);
        }

        public static void SetHasInitialized()
        {
            if (_instance == null)
            {
                Initialize();
            }
            _instance._debuggerHasInitialized = true;
        }
    }
}
#endif
