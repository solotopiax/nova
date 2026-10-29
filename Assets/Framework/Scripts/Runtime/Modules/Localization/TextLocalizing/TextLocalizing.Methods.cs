/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizing.Methods.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   文本本地化组件-私有方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文本本地化组件。
    /// </summary>
    public sealed partial class TextLocalizing : MonoBehaviour
    {
        /// <summary>
        /// 等待下一帧末尾，使 EventComponent.Start 的事件池重建先完成。
        /// 销毁取消等待，禁用或独立文本接管期间不补订阅；重新启用时复用已完成的启动状态。
        /// </summary>
        /// <returns>首次启动等待和可用实例的订阅刷新任务。</returns>
        private async UniTask InitializeAfterStartAsync()
        {
            if (m_IsTextRenderingRelinquished)
            {
                return;
            }

            try
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return;
            }

            m_IsStartupComplete = true;
            SubscribeAndRefresh();
        }

        /// <summary>
        /// 为已启动且仍启用的实例获取当前管理器，确保真实事件池内只有本实例的一条订阅并刷新显示。
        /// 重启后管理器身份变化时先安全摘除旧订阅，禁止继续使用旧本地化或资源管理器。
        /// </summary>
        private void SubscribeAndRefresh()
        {
            if (!m_IsStartupComplete || !isActiveAndEnabled || m_IsTextRenderingRelinquished || m_TextMeshProUGUI == null)
            {
                return;
            }

            IEventManager currentEventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            if (!ReferenceEquals(m_EventManager, currentEventManager))
            {
                UnsubscribeRefresh();
                m_EventManager = currentEventManager;
            }

            m_LocalizationManager = FrameworkManagersGroup.GetManager<ILocalizationManager>();
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            if (m_EventManager != null && !m_EventManager.Check<LocalizationRefreshEventData>(OnLocalizationRefresh))
            {
                m_EventManager.Subscribe<LocalizationRefreshEventData>(OnLocalizationRefresh);
            }

            RefreshText();
            RefreshFont();
        }

        /// <summary>
        /// 只取消实际事件池仍持有的本实例订阅，兼容初始化重建或 Shutdown 已经清空事件池。
        /// </summary>
        private void UnsubscribeRefresh()
        {
            if (m_EventManager != null && m_EventManager.Check<LocalizationRefreshEventData>(OnLocalizationRefresh))
            {
                m_EventManager.Unsubscribe<LocalizationRefreshEventData>(OnLocalizationRefresh);
            }
        }

        /// <summary>
        /// 本地化刷新事件回调。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件数据。</param>
        private void OnLocalizationRefresh(object sender, EventData e)
        {
            RefreshText();
            RefreshFont();
        }

        /// <summary>
        /// 刷新文本内容。
        /// 根据当前 KeyName 从本地化组件获取译文并赋值到文本组件。
        /// </summary>
        private void RefreshText()
        {
            RefreshTextDirection();

            if (string.IsNullOrEmpty(m_LocalizingKeyName))
            {
                return;
            }

            if (m_LocalizationManager == null)
            {
                return;
            }

            string value = m_LocalizationManager.GetText(m_LocalizingKeyName);

            if (m_TextMeshProUGUI != null)
            {
                m_TextMeshProUGUI.text = value;
            }
        }

        /// <summary>
        /// 根据当前语言更新 TMP 的书写方向和渲染预处理器。
        /// </summary>
        private void RefreshTextDirection()
        {
            if (m_TextMeshProUGUI == null || m_TextPreprocessor == null)
            {
                return;
            }

            Language language = m_LocalizationManager?.Language ?? Language.Unspecified;
            bool languageChanged = m_TextPreprocessor.Language != language;
            m_TextPreprocessor.Language = language;
            m_TextMeshProUGUI.isRightToLeftText = LanguageMetadata.IsRightToLeft(language);
            if (languageChanged)
            {
                m_TextMeshProUGUI.SetVerticesDirty();
                m_TextMeshProUGUI.SetLayoutDirty();
            }
        }

        /// <summary>
        /// 刷新字体。
        /// 根据当前语言和 FontMark 从字体配置中匹配字体并加载赋值。
        /// </summary>
        private void RefreshFont()
        {
            int refreshVersion = ++m_FontRefreshVersion;

            if (m_LocalizationManager == null || !m_LocalizationManager.AutoFontAdapt)
            {
                return;
            }

            if (string.IsNullOrEmpty(m_LocalizingFontMark))
            {
                return;
            }

            ILocalizationFontRow targetFontData = m_LocalizationManager.GetFontData(m_LocalizingFontMark);
            if (targetFontData == null)
            {
                Log.Debug(LogTag.Localization, "TextLocalizing 未找到匹配的字体数据，FontMark='{0}'，Language='{1}'。", m_LocalizingFontMark, m_LocalizationManager.Language);
                return;
            }

            LoadAndApplyFont(targetFontData, refreshVersion, m_LocalizationManager.Language).Forget();
        }

        /// <summary>
        /// 加载并应用字体资源。
        /// </summary>
        /// <param name="fontData">目标字体配置数据。</param>
        /// <param name="refreshVersion">发起加载时的字体刷新版本。</param>
        /// <param name="language">发起加载时的语言。</param>
        private async UniTaskVoid LoadAndApplyFont(ILocalizationFontRow fontData, int refreshVersion, Language language)
        {
            if (m_AssetManager == null)
            {
                Log.Warning(LogTag.Localization, "AssetComponent 不可用，无法加载字体资源。");
                return;
            }

            if (string.IsNullOrEmpty(fontData.AssetLocation))
            {
                Log.Warning(LogTag.Localization, "字体数据的 AssetLocation 为空，FontMark='{0}'。", fontData.Mark);
                return;
            }

            IAssetHandle<UnityEngine.Object> fontHandle = null;
            try
            {
                CancellationToken ct = this.GetCancellationTokenOnDestroy();
                fontHandle = await m_AssetManager.LoadAsync<UnityEngine.Object>(fontData.AssetLocation, ct);
                ct.ThrowIfCancellationRequested();

                if (!CanApplyFontRefresh(refreshVersion, language))
                {
                    fontHandle?.Release();
                    fontHandle = null;
                    return;
                }

                if (fontHandle.Asset == null)
                {
                    fontHandle.Release();
                    fontHandle = null;
                    Log.Warning(LogTag.Localization, "字体资源加载失败，AssetLocation='{0}'。", fontData.AssetLocation);
                    return;
                }

                IAssetHandle<UnityEngine.Object> previousFontHandle = m_LoadedFontHandle;
                m_LoadedFontHandle = fontHandle;
                fontHandle = null;
                try
                {
                    ApplyFontAsset(m_LoadedFontHandle.Asset, fontData, refreshVersion, language);
                }
                finally
                {
                    // TMP 已切到新字体与默认材质后，才能归还旧字体，避免旧材质仍引用已释放的图集。
                    previousFontHandle?.Release();
                }
            }
            catch (OperationCanceledException)
            {
                fontHandle?.Release();
            }
            catch (Exception ex)
            {
                fontHandle?.Release();
                Log.Warning(LogTag.Localization, "字体资源加载异常，FontMark='{0}'，异常信息：{1}", fontData.Mark, ex.Message);
            }
        }

        /// <summary>
        /// 应用新字体并先恢复其默认材质，再加载可选材质，避免同字体的旧材质残留。
        /// </summary>
        /// <param name="fontAsset">加载到的字体资源。</param>
        /// <param name="fontData">字体配置数据。</param>
        /// <param name="refreshVersion">发起加载时的字体刷新版本。</param>
        /// <param name="language">发起加载时的语言。</param>
        private void ApplyFontAsset(
            UnityEngine.Object fontAsset,
            ILocalizationFontRow fontData,
            int refreshVersion,
            Language language)
        {
            if (m_TextMeshProUGUI == null)
            {
                return;
            }

            TMPro.TMP_FontAsset tmpFont = fontAsset as TMPro.TMP_FontAsset;
            if (tmpFont != null)
            {
                m_TextMeshProUGUI.font = tmpFont;
                // TMP 对同一字体赋值会提前返回，必须显式重设材质后再释放旧材质句柄。
                m_TextMeshProUGUI.fontSharedMaterial = tmpFont.material;
                m_LoadedMaterialHandle?.Release();
                m_LoadedMaterialHandle = null;
                ApplyFontSizeScale(fontData.FontSizeScaleRatio);
                LoadAndApplyMaterial(fontData, refreshVersion, language).Forget();
            }
        }

        /// <summary>
        /// 异步加载并应用自定义材质到文本组件。
        /// </summary>
        /// <param name="fontData">字体配置数据。</param>
        /// <param name="refreshVersion">发起加载时的字体刷新版本。</param>
        /// <param name="language">发起加载时的语言。</param>
        private async UniTaskVoid LoadAndApplyMaterial(ILocalizationFontRow fontData, int refreshVersion, Language language)
        {
            if (string.IsNullOrEmpty(fontData.MaterialName))
            {
                return;
            }

            if (m_AssetManager == null)
            {
                Log.Warning(LogTag.Localization, "AssetComponent 不可用，无法加载字体材质。");
                return;
            }

            IAssetHandle<Material> matHandle = null;
            try
            {
                CancellationToken ct = this.GetCancellationTokenOnDestroy();
                matHandle = await m_AssetManager.LoadAsync<Material>(fontData.MaterialName, ct);
                ct.ThrowIfCancellationRequested();

                if (!CanApplyFontRefresh(refreshVersion, language))
                {
                    matHandle?.Release();
                    matHandle = null;
                    return;
                }

                if (matHandle.Asset == null)
                {
                    matHandle.Release();
                    matHandle = null;
                    Log.Warning(LogTag.Localization, "字体材质加载失败，MaterialName='{0}'。", fontData.MaterialName);
                    return;
                }

                m_LoadedMaterialHandle?.Release();
                m_LoadedMaterialHandle = matHandle;
                matHandle = null;
                if (m_TextMeshProUGUI != null)
                {
                    m_TextMeshProUGUI.fontSharedMaterial = m_LoadedMaterialHandle.Asset;
                }
            }
            catch (OperationCanceledException)
            {
                matHandle?.Release();
            }
            catch (Exception ex)
            {
                matHandle?.Release();
                Log.Warning(LogTag.Localization, "字体材质加载异常，FontMark='{0}'，异常信息：{1}", fontData.Mark, ex.Message);
            }
        }

        /// <summary>
        /// 检查异步字体请求是否仍属于当前启用状态和当前语言。
        /// </summary>
        /// <param name="refreshVersion">发起加载时的字体刷新版本。</param>
        /// <param name="language">发起加载时的语言。</param>
        /// <returns>仍可应用时返回 true，否则返回 false。</returns>
        private bool CanApplyFontRefresh(int refreshVersion, Language language)
        {
            return isActiveAndEnabled &&
                   refreshVersion == m_FontRefreshVersion &&
                   m_LocalizationManager != null &&
                   m_LocalizationManager.Language == language;
        }

        /// <summary>
        /// 应用字体大小缩放比例。
        /// 首次调用缓存原始字号及 Auto Size 上下限；后续始终按原值缩放，不累积比例。
        /// Auto Size 开启时同步缩放上下限，防止 TMP 重建网格时覆盖配置的字号比例。
        /// </summary>
        /// <param name="scaleRatio">缩放比例（基准值为 1.0）。</param>
        private void ApplyFontSizeScale(float scaleRatio)
        {
            if (m_TextMeshProUGUI == null)
            {
                return;
            }

            if (m_OriginalFontSize < 0f)
            {
                m_OriginalFontSize = m_TextMeshProUGUI.fontSize;
                m_OriginalFontSizeMin = m_TextMeshProUGUI.fontSizeMin;
                m_OriginalFontSizeMax = m_TextMeshProUGUI.fontSizeMax;
            }

            if (m_TextMeshProUGUI.enableAutoSizing)
            {
                m_TextMeshProUGUI.fontSizeMin = m_OriginalFontSizeMin * scaleRatio;
                m_TextMeshProUGUI.fontSizeMax = m_OriginalFontSizeMax * scaleRatio;
            }

            m_TextMeshProUGUI.fontSize = m_OriginalFontSize * scaleRatio;
        }
    }
}
