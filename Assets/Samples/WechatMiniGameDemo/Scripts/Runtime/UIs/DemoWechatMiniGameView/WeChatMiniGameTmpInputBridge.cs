/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WeChatMiniGameTmpInputBridge.cs
 * author:    Codex
 * created:   2026/09/28
 * descrip:   Demo TMP 输入框与微信小游戏软键盘的适配
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.WeChatMiniGame.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    /// <summary>
    /// 仅在微信小游戏运行时接管 TMP 输入；Editor 与普通 WebGL 保留 Unity 默认输入行为。
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class WeChatMiniGameTmpInputBridge : MonoBehaviour,
        IPointerClickHandler, ISelectHandler, IDeselectHandler
    {
        private TMP_InputField m_Input;
        private WeChatMiniGamePlugin m_Wechat;
        private CancellationTokenSource m_KeyboardCancellation;
        private IDisposable m_InputSubscription;
        private IDisposable m_ConfirmSubscription;
        private IDisposable m_CompleteSubscription;
        private bool m_KeyboardOpen;

        private void Awake()
        {
            m_Input = GetComponent<TMP_InputField>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OpenKeyboardAsync().Forget();
        }

        public void OnSelect(BaseEventData eventData)
        {
            OpenKeyboardAsync().Forget();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            CloseKeyboard();
        }

        private void OnDisable()
        {
            CloseKeyboard();
        }

        private void OnDestroy()
        {
            CloseKeyboard();
        }

        private async UniTask OpenKeyboardAsync()
        {
            if (m_KeyboardOpen || !Nova.SDK.TryGet(out WeChatMiniGamePlugin plugin) ||
                plugin == null || !plugin.IsAvailable)
            {
                return;
            }

            m_Wechat = plugin;
            m_KeyboardOpen = true;
            m_KeyboardCancellation = new CancellationTokenSource();
            CancellationToken token = m_KeyboardCancellation.Token;
            try
            {
                m_InputSubscription = plugin.SubscribeKeyboardInput(OnKeyboardText);
                m_ConfirmSubscription = plugin.SubscribeKeyboardConfirm(OnKeyboardConfirm);
                m_CompleteSubscription = plugin.SubscribeKeyboardComplete(OnKeyboardComplete);
                await plugin.ShowKeyboardAsync(new WeChatMiniGameKeyboardOptions
                {
                    DefaultValue = m_Input.text,
                    MaxLength = m_Input.characterLimit > 0 ? m_Input.characterLimit : 140,
                    Multiple = m_Input.lineType != TMP_InputField.LineType.SingleLine,
                    KeyboardType = m_Input.contentType == TMP_InputField.ContentType.IntegerNumber
                        ? WeChatMiniGameKeyboardType.Number
                        : WeChatMiniGameKeyboardType.Text
                }, token);
            }
            catch (OperationCanceledException)
            {
                // 输入框失焦时正常取消。
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"微信软键盘未能打开：{exception.Message}");
                CloseKeyboard();
            }
        }

        private void OnKeyboardText(WeChatMiniGameKeyboardInputEvent input)
        {
            if (m_KeyboardOpen && m_Input != null)
            {
                m_Input.text = input.Value;
            }
        }

        private void OnKeyboardConfirm(WeChatMiniGameKeyboardInputEvent input)
        {
            OnKeyboardText(input);
            CloseKeyboard();
        }

        private void OnKeyboardComplete(WeChatMiniGameKeyboardInputEvent input)
        {
            OnKeyboardText(input);
            CloseKeyboard();
        }

        private void CloseKeyboard()
        {
            if (!m_KeyboardOpen)
            {
                return;
            }

            m_KeyboardOpen = false;
            m_KeyboardCancellation?.Cancel();
            m_InputSubscription?.Dispose();
            m_ConfirmSubscription?.Dispose();
            m_CompleteSubscription?.Dispose();
            m_InputSubscription = null;
            m_ConfirmSubscription = null;
            m_CompleteSubscription = null;
            m_KeyboardCancellation?.Dispose();
            m_KeyboardCancellation = null;
            if (m_Wechat != null)
            {
                m_Wechat.HideKeyboardAsync().Forget();
                m_Wechat = null;
            }
        }
    }
}
