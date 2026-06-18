/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.LoadByTag.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 按 Tag 查询
 ***************************************************************/

using System;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 通过 tag 查询 location 列表。
        /// </summary>
        /// <param name="tag">tag 名。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <returns>命中 location 数组。</returns>
        public override string[] GetLocationsByTag(string tag, string package = null)
        {
            ResourcePackage pkg = GetPackage(ResolvePackageName(package));
            AssetInfo[] infos = pkg.GetAssetInfos(tag);
            return ProjectAddresses(infos);
        }

        /// <summary>
        /// 通过多 tag 求并集查询 location 列表。
        /// </summary>
        /// <param name="tags">tag 名列表。</param>
        /// <param name="package">包名，null 走默认包。</param>
        /// <returns>命中 location 数组。</returns>
        public override string[] GetLocationsByTag(string[] tags, string package = null)
        {
            ResourcePackage pkg = GetPackage(ResolvePackageName(package));
            AssetInfo[] infos = pkg.GetAssetInfos(tags);
            return ProjectAddresses(infos);
        }

        /// <summary>
        /// 将 AssetInfo 数组映射为 Address 字符串数组。
        /// </summary>
        /// <param name="infos">AssetInfo 数组，允许 null 或空。</param>
        /// <returns>对应的 Address 数组；输入为空时返回空数组。</returns>
        private static string[] ProjectAddresses(AssetInfo[] infos)
        {
            if (infos == null || infos.Length == 0)
            {
                return Array.Empty<string>();
            }
            string[] result = new string[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                result[i] = infos[i].Address;
            }
            return result;
        }
    }
}
