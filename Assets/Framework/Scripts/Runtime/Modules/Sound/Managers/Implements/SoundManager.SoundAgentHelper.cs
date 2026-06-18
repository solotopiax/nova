/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.SoundAgentHelper.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 声音代理辅助器
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 声音代理辅助器。
        /// <para>挂载在声音代理 GameObject 上，封装 AudioSource 操作与淡入淡出协程。</para>
        /// </summary>
        internal sealed class SoundAgentHelper : MonoBehaviour
        {
            /// <summary>
            /// 声音源。
            /// </summary>
            private AudioSource m_AudioSource;

            /// <summary>
            /// 暂停时的音量值。
            /// </summary>
            private float m_VolumeWhenPause;

            /// <summary>
            /// 淡入淡出任务取消令牌源，每次新淡变前先取消上一次。
            /// </summary>
            private CancellationTokenSource m_FadeCts;

            /// <summary>
            /// 获取当前是否正在播放。
            /// </summary>
            public bool IsPlaying => m_AudioSource.isPlaying;

            /// <summary>
            /// 获取声音长度。
            /// </summary>
            public float Length => m_AudioSource.clip != null ? m_AudioSource.clip.length : 0f;

            /// <summary>
            /// 获取或设置播放位置。
            /// </summary>
            public float Time { get => m_AudioSource.time; set => m_AudioSource.time = value; }

            /// <summary>
            /// 获取或设置是否静音。
            /// </summary>
            public bool Mute { get => m_AudioSource.mute; set => m_AudioSource.mute = value; }

            /// <summary>
            /// 获取或设置是否循环播放。
            /// </summary>
            public bool Loop { get => m_AudioSource.loop; set => m_AudioSource.loop = value; }

            /// <summary>
            /// 获取或设置声音优先级。
            /// </summary>
            public int Priority { get => 128 - m_AudioSource.priority; set => m_AudioSource.priority = 128 - value; }

            /// <summary>
            /// 获取或设置音量大小。
            /// </summary>
            public float Volume { get => m_AudioSource.volume; set => m_AudioSource.volume = value; }

            /// <summary>
            /// 获取或设置声音音调。
            /// </summary>
            public float Pitch { get => m_AudioSource.pitch; set => m_AudioSource.pitch = value; }

            /// <summary>
            /// 获取或设置声音立体声声相。
            /// </summary>
            public float PanStereo { get => m_AudioSource.panStereo; set => m_AudioSource.panStereo = value; }

            /// <summary>
            /// 获取或设置声音代理辅助器所在的混音组。
            /// </summary>
            public AudioMixerGroup AudioMixerGroup { get => m_AudioSource.outputAudioMixerGroup; set => m_AudioSource.outputAudioMixerGroup = value; }

            /// <summary>
            /// 唤醒。
            /// </summary>
            private void Awake()
            {
                m_AudioSource = gameObject.GetOrAddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.rolloffMode = AudioRolloffMode.Custom;
                m_VolumeWhenPause = 0f;
                m_FadeCts = new CancellationTokenSource();
            }

            /// <summary>
            /// 销毁时释放取消令牌源。
            /// </summary>
            private void OnDestroy()
            {
                m_FadeCts?.Cancel();
                m_FadeCts?.Dispose();
                m_FadeCts = null;
            }

            /// <summary>
            /// 播放声音。
            /// </summary>
            /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
            public void Play(float fadeInSeconds)
            {
                CancelFade();
                m_AudioSource.Play();
                if (fadeInSeconds > 0f)
                {
                    float volume = m_AudioSource.volume;
                    m_AudioSource.volume = 0f;
                    FadeToVolumeAsync(volume, fadeInSeconds, m_FadeCts.Token).Forget();
                }
            }

            /// <summary>
            /// 停止播放声音。
            /// </summary>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            public void Stop(float fadeOutSeconds)
            {
                CancelFade();
                if (fadeOutSeconds > 0f && gameObject.activeInHierarchy)
                {
                    StopAfterFadeAsync(fadeOutSeconds, m_FadeCts.Token).Forget();
                }
                else
                {
                    m_AudioSource.Stop();
                }
            }

            /// <summary>
            /// 暂停播放声音。
            /// </summary>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            public void Pause(float fadeOutSeconds)
            {
                CancelFade();
                m_VolumeWhenPause = m_AudioSource.volume;
                if (fadeOutSeconds > 0f && gameObject.activeInHierarchy)
                {
                    PauseAfterFadeAsync(fadeOutSeconds, m_FadeCts.Token).Forget();
                }
                else
                {
                    m_AudioSource.Pause();
                }
            }

            /// <summary>
            /// 恢复播放声音。
            /// </summary>
            /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
            public void Resume(float fadeInSeconds)
            {
                CancelFade();
                m_AudioSource.UnPause();
                if (fadeInSeconds > 0f)
                {
                    FadeToVolumeAsync(m_VolumeWhenPause, fadeInSeconds, m_FadeCts.Token).Forget();
                }
                else
                {
                    m_AudioSource.volume = m_VolumeWhenPause;
                }
            }

            /// <summary>
            /// 重置声音代理辅助器。
            /// </summary>
            public void ResetHelper()
            {
                if (m_AudioSource) m_AudioSource.clip = null;
                m_VolumeWhenPause = 0f;
            }

            /// <summary>
            /// 设置声音资源。
            /// </summary>
            /// <param name="soundAsset">声音资源。</param>
            /// <returns>是否设置声音资源成功。</returns>
            public bool SetSoundAsset(object soundAsset)
            {
                AudioClip audioClip = soundAsset as AudioClip;
                if (audioClip == null)
                {
                    return false;
                }

                m_AudioSource.clip = audioClip;
                return true;
            }

            /// <summary>
            /// 取消当前淡变任务并重建取消令牌源。
            /// </summary>
            private void CancelFade()
            {
                m_FadeCts?.Cancel();
                m_FadeCts?.Dispose();
                m_FadeCts = new CancellationTokenSource();
            }

            /// <summary>
            /// 淡出至零音量后停止播放的异步任务。
            /// </summary>
            /// <param name="fadeOutSeconds">淡出时长（秒）。</param>
            /// <param name="token">取消令牌。</param>
            private async UniTaskVoid StopAfterFadeAsync(float fadeOutSeconds, CancellationToken token)
            {
                await FadeToVolumeAsync(0f, fadeOutSeconds, token);
                if (!token.IsCancellationRequested)
                {
                    m_AudioSource.Stop();
                }
            }

            /// <summary>
            /// 淡出至零音量后暂停播放的异步任务。
            /// </summary>
            /// <param name="fadeOutSeconds">淡出时长（秒）。</param>
            /// <param name="token">取消令牌。</param>
            private async UniTaskVoid PauseAfterFadeAsync(float fadeOutSeconds, CancellationToken token)
            {
                await FadeToVolumeAsync(0f, fadeOutSeconds, token);
                if (!token.IsCancellationRequested)
                {
                    m_AudioSource.Pause();
                }
            }

            /// <summary>
            /// 音量淡变异步任务。
            /// </summary>
            /// <param name="targetVolume">目标音量。</param>
            /// <param name="duration">淡变时长（秒）。</param>
            /// <param name="token">取消令牌。</param>
            private async UniTask FadeToVolumeAsync(float targetVolume, float duration, CancellationToken token)
            {
                float elapsed = 0f;
                float originalVolume = m_AudioSource.volume;
                while (elapsed < duration)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    elapsed += UnityEngine.Time.deltaTime;
                    m_AudioSource.volume = Mathf.Lerp(originalVolume, targetVolume, elapsed / duration);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!token.IsCancellationRequested)
                {
                    m_AudioSource.volume = targetVolume;
                }
            }
        }
    }
}
