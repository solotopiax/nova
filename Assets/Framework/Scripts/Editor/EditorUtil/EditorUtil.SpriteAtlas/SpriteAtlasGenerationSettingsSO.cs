/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteAtlasGenerationSettingsSO.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   项目显式声明的可选图集生产配置
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 图集作者文件格式；必须与项目当前已启用的 Sprite Packer 模式一致，不自动迁移。
    /// </summary>
    public enum SpriteAtlasGenerationFormat
    {
        /// <summary>使用原生 .spriteatlas 文件。</summary>
        V1 = 1,
        /// <summary>使用 SpriteAtlasAsset 作者文件与 .spriteatlasv2 导入器。</summary>
        V2 = 2,
    }

    /// <summary>
    /// 一个具名主图集的显式来源与打包规则；不修改源图片的导入设置或名称。
    /// </summary>
    [Serializable]
    public sealed class SpriteAtlasGenerationRule
    {
        /// <summary>配置内唯一且保持稳定的规则标识；只允许字母、数字、下划线与连字符。</summary>
        [Tooltip("规则的稳定标识；生成后不要复用给另一组图片。")]
        public string Id = "";

        /// <summary>输出文件名，不含目录和扩展名；同一输出目录内忽略大小写后必须唯一。</summary>
        [Tooltip("图集文件名，不含目录和扩展名。")]
        public string AtlasName = "";

        /// <summary>递归读取的项目 Assets 子目录；必须明确列出，不能使用 Assets 根目录。</summary>
        [Tooltip("递归读取这些项目目录中的 Sprite；不改动源图片。")]
        public List<string> SourceFolders = new List<string>();

        /// <summary>来源范围内排除的完整 Assets 文件或目录路径；不使用通配符。</summary>
        [Tooltip("排除的完整 Assets 文件或目录路径，不使用通配符。")]
        public List<string> ExcludedPaths = new List<string>();

        /// <summary>是否让 Unity 随直接引用的 Sprite 将图集包含进 Player；按需加载项目通常保持关闭。</summary>
        [Tooltip("随 Sprite 自动包含进 Player；通过资源包显式加载时通常关闭。")]
        public bool IncludeInBuild;

        /// <summary>图块之间的像素间距；只接受 Unity Inspector 提供的 2、4、8、16、32。</summary>
        [Tooltip("图块间距，单位像素；支持 2、4、8、16、32。")]
        public int Padding = 4;

        /// <summary>是否允许图块旋转；UI 图集默认关闭。</summary>
        [Tooltip("允许旋转图块；UI 通常关闭。")]
        public bool AllowRotation;

        /// <summary>是否按照 Sprite 网格紧密打包。</summary>
        [Tooltip("按照 Sprite 网格紧密打包。")]
        public bool TightPacking;

        /// <summary>是否为输出图集生成 Mip Map；不改变源图片设置。</summary>
        [Tooltip("为图集生成 Mip Map；UI 通常关闭。")]
        public bool GenerateMipMaps;
    }

    /// <summary>
    /// 可选图集生产的权威项目配置。仅创建此资产不会扫描、生成图集或改变项目设置。
    /// 由原生 Inspector 编辑并保存，再显式预演或加入项目选择的 Pipify Batch。
    /// </summary>
    [CreateAssetMenu(fileName = "SpriteAtlasGenerationSettings", menuName = "Nova/Sprite Atlas Generation Settings")]
    public sealed class SpriteAtlasGenerationSettingsSO : ScriptableObject
    {
        /// <summary>显式启用生产；默认关闭，关闭时预演仅报告未启用。</summary>
        [Tooltip("明确启用后才允许生成；创建配置不会自动执行。")]
        public bool Enabled;

        /// <summary>本配置唯一拥有的、已存在且已导入的输出 Assets 子目录；不接管或覆盖目录内的手工资产。</summary>
        [Tooltip("先创建图集输出目录；它不能位于源图目录内，也不能包含源图目录。")]
        public string OutputFolder = "";

        /// <summary>本配置的作者文件格式；项目当前 Sprite Packer 模式必须已与它匹配。</summary>
        [Tooltip("使用项目已经启用的图集格式；工具不会自动切换或迁移。")]
        public SpriteAtlasGenerationFormat Format = SpriteAtlasGenerationFormat.V2;

        /// <summary>显式声明的主图集规则；默认空，不自动按目录创建规则。</summary>
        public List<SpriteAtlasGenerationRule> Rules = new List<SpriteAtlasGenerationRule>();
    }
}
