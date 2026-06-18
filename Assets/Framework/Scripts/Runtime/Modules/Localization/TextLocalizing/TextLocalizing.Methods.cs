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
using System.Collections.Generic;
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
        /// 刷新字体。
        /// 根据当前语言和 FontMark 从字体配置中匹配字体并加载赋值。
        /// </summary>
        private void RefreshFont()
        {
            if (m_LocalizationManager == null || !m_LocalizationManager.AutoFontAdapt)
            {
                return;
            }

            if (string.IsNullOrEmpty(m_LocalizingFontMark))
            {
                return;
            }

            IReadOnlyList<ILocalizationFontRow> fontDatas = m_LocalizationManager.GetFontDatas(m_LocalizationManager.Language);
            if (fontDatas == null || fontDatas.Count == 0)
            {
                return;
            }

            ILocalizationFontRow targetFontData = null;
            for (int i = 0; i < fontDatas.Count; i++)
            {
                if (string.Equals(fontDatas[i].Mark, m_LocalizingFontMark, StringComparison.Ordinal))
                {
                    targetFontData = fontDatas[i];
                    break;
                }
            }

            if (targetFontData == null)
            {
                Log.Debug(LogTag.Localization, "TextLocalizing 未找到匹配的字体数据，FontMark='{0}'，Language='{1}'。", m_LocalizingFontMark, m_LocalizationManager.Language);
                return;
            }

            LoadAndApplyFont(targetFontData).Forget();
        }

        /// <summary>
        /// 加载并应用字体资源。
        /// </summary>
        /// <param name="fontData">目标字体配置数据。</param>
        private async UniTaskVoid LoadAndApplyFont(ILocalizationFontRow fontData)
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

                if (fontHandle.Asset == null)
                {
                    fontHandle.Release();
                    fontHandle = null;
                    Log.Warning(LogTag.Localization, "字体资源加载失败，AssetLocation='{0}'。", fontData.AssetLocation);
                    return;
                }

                m_LoadedFontHandle?.Release();
                m_LoadedFontHandle = fontHandle;
                fontHandle = null;
                ApplyFontAsset(m_LoadedFontHandle.Asset, fontData);
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
        /// 将加载完成的字体资源应用到文本组件上。
        /// </summary>
        /// <param name="fontAsset">加载到的字体资源。</param>
        /// <param name="fontData">字体配置数据。</param>
        private void ApplyFontAsset(UnityEngine.Object fontAsset, ILocalizationFontRow fontData)
        {
            if (m_TextMeshProUGUI == null)
            {
                return;
            }

            TMPro.TMP_FontAsset tmpFont = fontAsset as TMPro.TMP_FontAsset;
            if (tmpFont != null)
            {
                m_TextMeshProUGUI.font = tmpFont;
                ApplyFontSizeScale(fontData.FontSizeScaleRatio);
                LoadAndApplyMaterial(fontData).Forget();
            }
        }

        /// <summary>
        /// 异步加载并应用自定义材质到文本组件。
        /// </summary>
        /// <param name="fontData">字体配置数据。</param>
        private async UniTaskVoid LoadAndApplyMaterial(ILocalizationFontRow fontData)
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
        /// 应用字体大小缩放比例。
        /// 首次调用时缓存原始字号，后续调用基于原始字号进行缩放。
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
            }

            m_TextMeshProUGUI.fontSize = m_OriginalFontSize * scaleRatio;
        }
    }
}
