/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateComponent.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动组件
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class VibrateComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_VibrateManager = Util.TypeCreator.Create<IVibrateManager>(m_CurManagerTypeName);
            if (m_VibrateManager == null)
            {
                throw new InvalidOperationException("VibrateManager 无效。");
            }
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            if (m_Settings == null)
            {
                throw new InvalidOperationException("VibrateSettings 无效，请检查 VibrateComponent 配置。");
            }

            m_VibrateManager.Initialize(new VibrateManagerConfig
            {
                EmphasisUnitsSettings = m_Settings.EmphasisUnitsSettings,
                CustomUnitsSettings = m_Settings.CustomUnitsSettings,
            });
        }

        /// <summary>
        /// 加载振动数据（fire-and-forget）。
        /// </summary>
        public void LoadSync()
        {
            m_VibrateManager.LoadVibrateDataSync();
        }

        /// <summary>
        /// 异步加载振动数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public async UniTask<bool> LoadAsync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            if (m_LoadTcs != null)
            {
                return await m_LoadTcs.Task;
            }

            m_LoadTcs = new UniTaskCompletionSource<bool>();
            var tcs = m_LoadTcs;

            bool success;
            try
            {
                success = await m_VibrateManager.LoadVibrateDataAsync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Vibrate, "VibrateComponent.LoadAsync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            tcs.TrySetResult(success);
            m_LoadTcs = null;

            return success;
        }

        /// <summary>
        /// 播放默认振动。
        /// </summary>
        public void Play()
        {
            m_VibrateManager.Play();
        }

        /// <summary>
        /// 播放指定类型的预设振动。
        /// </summary>
        /// <param name="type">振动类型。</param>
        public void Play(VibrateType type)
        {
            m_VibrateManager.Play(type);
        }

        /// <summary>
        /// 按名称播放自定义振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        public void PlayCustom(string name)
        {
            m_VibrateManager.PlayCustom(name);
        }

        /// <summary>
        /// 播放自定义持续振动。
        /// </summary>
        /// <param name="intensity">振动强度（0~1）。</param>
        /// <param name="sharpness">振动锐度（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="duration">振动持续时长（秒）。</param>
        public void PlayCustom(float intensity, float sharpness, float preDuration, float duration)
        {
            m_VibrateManager.PlayCustom(intensity, sharpness, preDuration, duration);
        }

        /// <summary>
        /// 按名称播放强调振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        public void PlayEmphasis(string name)
        {
            m_VibrateManager.PlayEmphasis(name);
        }

        /// <summary>
        /// 播放强调振动。
        /// </summary>
        /// <param name="amplitude">振幅（0~1）。</param>
        /// <param name="frequency">频率（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="interval">振动间隔时长（秒）。</param>
        public void PlayEmphasis(float amplitude, float frequency, float preDuration, float interval)
        {
            m_VibrateManager.PlayEmphasis(amplitude, frequency, preDuration, interval);
        }
        
        /// <summary>
        /// 停止所有振动。
        /// </summary>
        public void StopAll()
        {
            m_VibrateManager.StopAll();
        }

        /// <summary>
        /// 获取或设置振动是否启用。
        /// </summary>
        public bool Enable
        {
            get => m_VibrateManager.Enable;
            set => m_VibrateManager.Enable = value;
        }

        /// <summary>
        /// 获取当前设备是否支持振动。
        /// </summary>
        public bool IsSupported => m_VibrateManager.IsSupported;
    }
}
