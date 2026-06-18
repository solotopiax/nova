/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateManager.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if NOVA_NICEVIBRATIONS
using Lofelt.NiceVibrations;
#endif

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动管理器。
    /// </summary>
    internal sealed partial class VibrateManager : VibrateManagerBase
    {
        /// <summary>
        /// 初始化振动管理器的新实例。
        /// </summary>
        public VibrateManager()
        {
            m_EmphasisGroups = new Dictionary<string, List<IVibrateEmphasisRow>>();
            m_CustomGroups = new Dictionary<string, List<IVibrateCustomRow>>();
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">振动管理器配置。</param>
        public override void Initialize(VibrateManagerConfig config)
        {
            m_EmphasisUnitsSettings = config.EmphasisUnitsSettings;
            m_CustomUnitsSettings = config.CustomUnitsSettings;
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            if (m_PlayCts != null)
            {
                m_PlayCts.Cancel();
                m_PlayCts.Dispose();
                m_PlayCts = null;
            }

            m_EmphasisGroups?.Clear();
            m_CustomGroups?.Clear();
            m_EmphasisUnitsSettings = null;
            m_CustomUnitsSettings = null;
        }

        /// <summary>
        /// 同步加载振动数据。
        /// </summary>
        public override void LoadVibrateDataSync()
        {
            BuildVibrateDataSyncDelegates(out DataReceiver.LoadAssetSyncFunc syncLoadFunc, out DataReceiver.ReleaseAssetAction releaseFunc);

            LubanDataCache dataCache = new LubanDataCache();

            AddSyncLoadTasks(m_EmphasisUnitsSettings, dataCache, syncLoadFunc, releaseFunc);
            AddSyncLoadTasks(m_CustomUnitsSettings, dataCache, syncLoadFunc, releaseFunc);

            BuildGroupsFromCache(dataCache);
        }

        /// <summary>
        /// 异步加载振动数据。
        /// <para>Phase 1：并行加载 Emphasis 和 Custom AB 资源并将数据拆分缓存。</para>
        /// <para>Phase 2：反射构造 VibrateTables，直接从 DataList 构建分组缓存，Tables 局部变量随后释放。</para>
        /// </summary>
        /// <returns>是否加载并解析成功。</returns>
        public override async UniTask<bool> LoadVibrateDataAsync()
        {
            List<UniTask<bool>> tasks = new List<UniTask<bool>>(2);
            BuildVibrateDataAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc);

            LubanDataCache dataCache = new LubanDataCache();

            AddAsyncLoadTasks(m_EmphasisUnitsSettings, dataCache, loadFunc, releaseFunc, tasks);
            AddAsyncLoadTasks(m_CustomUnitsSettings, dataCache, loadFunc, releaseFunc, tasks);

            if (tasks.Count > 0)
            {
                bool[] results = await UniTask.WhenAll(tasks);

                if (m_AssetManager == null)
                {
                    return false;
                }

                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        return false;
                    }
                }
            }

            if (!BuildGroupsFromCache(dataCache))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 播放默认振动。
        /// </summary>
        public override void Play()
        {
            if (!Enable) return;

#if NOVA_NICEVIBRATIONS
            HapticPatterns.PlayConstant(1f, 1f, 0.3f);
#endif
        }

        /// <summary>
        /// 播放指定类型的预设振动。
        /// </summary>
        /// <param name="type">振动类型。</param>
        public override void Play(VibrateType type)
        {
            if (!Enable) return;
            if (type == VibrateType.None) return;

#if NOVA_NICEVIBRATIONS
            HapticPatterns.PlayPreset(GetHapticType(type));
#endif
        }

        /// <summary>
        /// 按名称播放自定义振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        public override void PlayCustom(string name)
        {
            if (!Enable) return;

            if (string.IsNullOrEmpty(name) || m_CustomGroups == null || !m_CustomGroups.ContainsKey(name))
            {
                Log.Warning(LogTag.Vibrate, "未找到自定义振动组合：{0}", name);
                return;
            }

            CancelAndRecreateCts();
            PlayCustomGroupInternal(name, m_PlayCts.Token).Forget();
        }

        /// <summary>
        /// 播放自定义持续振动。
        /// </summary>
        /// <param name="intensity">振动强度（0~1）。</param>
        /// <param name="sharpness">振动锐度（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="duration">振动持续时长（秒）。</param>
        public override void PlayCustom(float intensity, float sharpness, float preDuration, float duration)
        {
            if (!Enable) return;

            ValidateRange01(intensity, nameof(intensity));
            ValidateRange01(sharpness, nameof(sharpness));
            if (preDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(preDuration), "preDuration 不可为负数。");
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), "duration 不可为负数。");

#if NOVA_NICEVIBRATIONS
            if (preDuration > 0f)
            {
                EnsureCts();
                PlayCustomDelayed(intensity, sharpness, preDuration, duration, m_PlayCts.Token).Forget();
                return;
            }

            HapticPatterns.PlayConstant(intensity, sharpness, duration);
#endif
        }

        /// <summary>
        /// 按名称播放强调振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        public override void PlayEmphasis(string name)
        {
            if (!Enable) return;

            if (string.IsNullOrEmpty(name) || m_EmphasisGroups == null || !m_EmphasisGroups.ContainsKey(name))
            {
                Log.Warning(LogTag.Vibrate, "未找到强调振动组合：{0}", name);
                return;
            }

            CancelAndRecreateCts();
            PlayEmphasisGroupInternal(name, m_PlayCts.Token).Forget();
        }

        /// <summary>
        /// 播放强调振动。
        /// </summary>
        /// <param name="amplitude">振幅（0~1）。</param>
        /// <param name="frequency">频率（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="interval">振动间隔时长（秒）。</param>
        public override void PlayEmphasis(float amplitude, float frequency, float preDuration, float interval)
        {
            if (!Enable) return;

            ValidateRange01(amplitude, nameof(amplitude));
            ValidateRange01(frequency, nameof(frequency));
            if (preDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(preDuration), "preDuration 不可为负数。");
            if (interval < 0f)
                throw new ArgumentOutOfRangeException(nameof(interval), "interval 不可为负数。");

#if NOVA_NICEVIBRATIONS
            if (preDuration > 0f)
            {
                EnsureCts();
                PlayEmphasisDelayed(amplitude, frequency, preDuration, m_PlayCts.Token).Forget();
                return;
            }

            HapticPatterns.PlayEmphasis(amplitude, frequency);
#endif
        }
        
        /// <summary>
        /// 停止所有振动。
        /// </summary>
        public override void StopAll()
        {
            CancelAndRecreateCts();

#if NOVA_NICEVIBRATIONS
            HapticController.Stop();
#endif
        }

        /// <summary>
        /// 获取或设置振动是否启用。
        /// </summary>
        public override bool Enable
        {
            get
            {
#if NOVA_NICEVIBRATIONS
                return HapticController.hapticsEnabled;
#else
                return m_Enable;
#endif
            }
            set
            {
#if NOVA_NICEVIBRATIONS
                HapticController.hapticsEnabled = value;
#else
                m_Enable = value;
#endif
            }
        }

        /// <summary>
        /// 获取当前设备是否支持振动。
        /// </summary>
        public override bool IsSupported
        {
            get
            {
#if NOVA_NICEVIBRATIONS
                return DeviceCapabilities.isVersionSupported;
#else
                return false;
#endif
            }
        }
    }
}
