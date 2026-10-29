/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationComponent.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化组件
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class LocalizationComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒：先发布启动期可读的语言策略，再创建正式本地化管理器。
        /// Unity 会在任意 Start 前完成全部 Awake，因此 ProcedureSplash 可稳定读取该策略。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            LocalizationBootstrapLanguage.Configure(new LocalizationLanguagePolicy(
                m_EditorLanguage,
                m_RuntimeLanguagePrefer,
                m_FallbackLanguage));

            m_LocalizationManager = Util.TypeCreator.Create<ILocalizationManager>(m_CurLocalizationManagerTypeName);
            if (m_LocalizationManager == null)
            {
                throw new InvalidOperationException("LocalizationManager 无效。");
            }
        }

        /// <summary>
        /// 开始（获取跨模块引用并初始化管理器）。
        /// </summary>
        private void Start()
        {
            m_LocalizationManager.Initialize(new LocalizationManagerConfig
            {
                FallbackLanguage = m_FallbackLanguage,
                EditorLanguage = m_EditorLanguage,
                RuntimeLanguagePrefer = m_RuntimeLanguagePrefer,
                AutoFontAdapt = m_AutoFontAdapt,
                DataFormat = m_LocalizationSettings.DataFormat,
                TextUnitSettings = m_LocalizationSettings.TextUnitsSettings,
                FontUnitSettings = m_LocalizationSettings.FontUnitsSettings,
                SupportedLanguagesAssetLocation = m_SupportedLanguagesAssetLocation,
                PersistClassifyName = c_PersistClassifyName,
                PersistItemKey = c_PersistItemKey,
            });
        }

        /// <summary>
        /// 销毁时清理管理器引用。Shutdown 由 FrameworkManagersGroup 统一调度。
        /// </summary>
        private void OnDestroy()
        {
            m_LocalizationManager = null;
        }

        /// <summary>
        /// 异步加载本地化基础数据（语言列表 + 字体配置）。
        /// </summary>
        /// <returns>基础加载流程结果；内置实现正常结束返回 true，不保证支持语言和字体完整。</returns>
        public UniTask<bool> LoadAsync()
        {
            return m_LocalizationManager.LoadAsync();
        }

        /// <summary>
        /// 同步加载本地化基础数据（语言列表 + 字体配置）。
        /// </summary>
        public void LoadSync()
        {
            m_LocalizationManager.LoadSync();
        }

        /// <summary>
        /// 根据优先级决定当前语言并同步加载对应文本数据。供 Procedure 调用完成语言初始化。
        /// </summary>
        /// <returns>解析得到的候选语言；切换可能被拒或加载失败，须另核实际 Language 与文本。</returns>
        public Language InitCurrentLanguageSync()
        {
            return m_LocalizationManager.InitCurrentLanguageSync();
        }

        /// <summary>
        /// 根据优先级决定当前语言并异步加载对应文本数据。供 Procedure 调用完成语言初始化。
        /// </summary>
        /// <returns>解析得到的候选语言；切换可能被拒或加载失败，须另核实际 Language 与文本。</returns>
        public UniTask<Language> InitCurrentLanguageAsync()
        {
            return m_LocalizationManager.InitCurrentLanguageAsync();
        }

        /// <summary>
        /// 检查指定语言是否在已支持列表中。
        /// </summary>
        /// <param name="language">待检查的语言。</param>
        /// <returns>是否已支持。</returns>
        public bool HasSupportedLanguage(Language language)
        {
            return m_LocalizationManager.HasSupportedLanguage(language);
        }

        /// <summary>
        /// 获取所有已支持语言的只读列表。
        /// </summary>
        /// <returns>已支持语言只读列表。</returns>
        public IReadOnlyList<Language> GetSupportedLanguages()
        {
            return m_LocalizationManager.GetSupportedLanguages();
        }

        /// <summary>
        /// 按编辑器强制语言、终端偏好及持久化/系统/回退规则解析候选；此调用不加载文本。
        /// </summary>
        /// <returns>解析后的语言。</returns>
        public Language ResolveLanguage()
        {
            return m_LocalizationManager.ResolveLanguage();
        }

        /// <summary>
        /// 同步切换语言；内置实现使旧异步请求失效，选回当前语言不重复加载或广播。
        /// </summary>
        /// <param name="language">目标语言。</param>
        public void SetLanguageSync(Language language)
        {
            m_LocalizationManager.SetLanguageSync(language);
        }

        /// <summary>
        /// 异步切换语言；内置实现只允许最后一次有效请求提交，完成后须核对实际 Language。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>异步任务。</returns>
        public UniTask SetLanguageAsync(Language language)
        {
            return m_LocalizationManager.SetLanguageAsync(language);
        }

        /// <summary>
        /// 检查指定名称是否存在于当前语言数据中。
        /// </summary>
        /// <param name="name">文本名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasText(string name)
        {
            return m_LocalizationManager.HasText(name);
        }

        /// <summary>
        /// 获取指定名称对应的本地化文本。
        /// </summary>
        /// <param name="name">文本名称。</param>
        /// <returns>本地化文本，不存在时返回名称本身。</returns>
        public string GetText(string name)
        {
            return m_LocalizationManager.GetText(name);
        }

        /// <summary>
        /// 检查是否包含指定类型的本地化文本表。
        /// </summary>
        /// <typeparam name="T">Luban 生成的 TbXxx 表类型。</typeparam>
        /// <returns>是否存在。</returns>
        public bool HasTexts<T>() where T : class, ITable
        {
            return m_LocalizationManager.HasTexts<T>();
        }

        /// <summary>
        /// 检查是否包含指定表名称的本地化文本表。
        /// </summary>
        /// <param name="tbName">表类型短名称（如 "TbCommon"）。</param>
        /// <returns>是否存在。</returns>
        public bool HasTexts(string tbName)
        {
            return m_LocalizationManager.HasTexts(tbName);
        }

        /// <summary>
        /// 获取指定类型的本地化文本表。
        /// </summary>
        /// <typeparam name="T">Luban 生成的 TbXxx 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        public T GetTexts<T>() where T : class, ITable
        {
            return m_LocalizationManager.GetTexts<T>();
        }

        /// <summary>
        /// 获取指定表名称的本地化文本表。
        /// </summary>
        /// <param name="tbName">表类型短名称（如 "TbCommon"）。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        public ITable GetTexts(string tbName)
        {
            return m_LocalizationManager.GetTexts(tbName);
        }

        /// <summary>
        /// 按字体标记获取当前语言的首条匹配配置，不加载字体资源。
        /// </summary>
        /// <param name="mark">字体表的 Mark，按 Ordinal 精确匹配；空值不匹配。</param>
        /// <returns>原始字体配置行；未命中时返回 null，不回退语言。</returns>
        public ILocalizationFontRow GetFontData(string mark)
        {
            return m_LocalizationManager.GetFontData(mark);
        }

        /// <summary>
        /// 按语言和字体标记获取首条匹配配置，不切换语言或加载字体资源。
        /// </summary>
        /// <param name="language">要查询的语言。</param>
        /// <param name="mark">字体表的 Mark，按 Ordinal 精确匹配；空值不匹配。</param>
        /// <returns>原始字体配置行；未命中时返回 null，不回退语言。</returns>
        public ILocalizationFontRow GetFontData(Language language, string mark)
        {
            return m_LocalizationManager.GetFontData(language, mark);
        }

        /// <summary>
        /// 检查指定语言是否存在字体数据。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>是否存在字体数据。</returns>
        public bool HasFontDatas(Language language)
        {
            return m_LocalizationManager.HasFontDatas(language);
        }

        /// <summary>
        /// 获取指定语言的字体数据列表。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>字体数据只读列表，语言不存在时返回 null。</returns>
        public IReadOnlyList<ILocalizationFontRow> GetFontDatas(Language language)
        {
            return m_LocalizationManager.GetFontDatas(language);
        }
    }
}
