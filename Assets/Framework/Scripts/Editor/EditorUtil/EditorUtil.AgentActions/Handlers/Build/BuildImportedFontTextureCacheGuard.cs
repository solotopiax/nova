/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BuildImportedFontTextureCacheGuard.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   准确导入字体零纹理缓存序列化后同阶段的脏资产分类
 ***************************************************************/

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只识别准确 TrueType 导入器生成且已有主 Font 直接序列化纹理归属的零纹理缓存；不加载字体、清 dirty 或保存。</summary>
    internal static class BuildImportedFontTextureCacheGuard
    {
        private const string ZeroTextureJson = "{\"Texture2D\":{\"m_Name\":\"Font Texture\",\"m_ImageContentsHash\":{\"serializedVersion\":\"2\",\"Hash\":\"00000000000000000000000000000000\"},\"m_IsAlphaChannelOptional\":false,\"serializedVersion\":\"4\",\"m_Width\":0,\"m_Height\":0,\"m_CompleteImageSize\":0,\"m_MipsStripped\":0,\"m_TextureFormat\":4,\"m_MipCount\":1,\"m_IsReadable\":false,\"m_IsPreProcessed\":false,\"m_IgnoreMipmapLimit\":false,\"m_MipmapLimitGroupName\":\"\",\"m_StreamingMipmaps\":false,\"m_StreamingMipmapsPriority\":0,\"m_VTOnly\":false,\"m_AlphaIsTransparency\":false,\"m_ImageCount\":1,\"m_TextureDimension\":2,\"m_TextureSettings\":{\"serializedVersion\":\"2\",\"m_FilterMode\":1,\"m_Aniso\":1,\"m_MipBias\":0.0,\"m_WrapU\":0,\"m_WrapV\":0,\"m_WrapW\":0},\"m_LightmapFormat\":0,\"m_ColorSpace\":0,\"m_PlatformBlob\":[],\"image data\":0,\"_typelessdata\":\"\",\"m_StreamData\":{\"serializedVersion\":\"2\",\"offset\":0,\"size\":0,\"path\":\"\"}}}";

        /// <summary>仅保存该次已执行读取的纯值；未执行字段不存在，不持有Native对象、委托或跨调用状态。</summary>
        internal sealed class DecisionTrace
        {
            internal readonly JObject observed = new JObject();
            internal string lastAttempt;
            internal string rejectedAt;
            internal string exceptionType;
            internal string exactDirtyRow;
            internal bool classified;
        }

        /// <summary>保持原单参入口及分类语义；只执行一次同一核心，不额外探测或收集诊断。</summary>
        /// <param name="value">原guard已确认持久化且dirty的候选。</param>
        /// <returns>原完整准入成立才为true。</returns>
        internal static bool IsImportedEmptyFontTextureCache(UnityEngine.Object value) => ClassifyImportedFontTextureCache(value, null);

        /// <summary>同一次原判定额外返回纯读取记录；不得为缺失记录补读Native或把记录当审批许可。</summary>
        /// <param name="value">该次原guard实际候选。</param><param name="trace">仅已执行读取的纯值、短路位置及实际异常类型。</param>
        /// <returns>与原单参入口完全相同的分类布尔值。</returns>
        internal static bool IsImportedEmptyFontTextureCacheWithDiagnostics(UnityEngine.Object value, out DecisionTrace trace)
        {
            trace = new DecisionTrace(); return ClassifyImportedFontTextureCache(value, trace);
        }

        /// <summary>保留来源/owner/ID门后仅执行原一次ToJson，再按序校验同阶段6项严格零元数据及第二标志；序列化可改变Native缓存，异常边界保持。</summary>
        /// <param name="value">原实际对象。</param><param name="trace">可选纯记录器；null不创建或保留诊断。</param>
        /// <returns>原准确导入器/owner/metadata/JSON分类结果。</returns>
        private static bool ClassifyImportedFontTextureCache(UnityEngine.Object value, DecisionTrace trace)
        {
            string path;
            try
            {
                if (ReadObserved(trace, "candidateNull", () => value == null)) return FinishTrace(trace, false, "candidateNull");
                if (ReadObserved(trace, "candidateType", () => value.GetType()) != typeof(Texture2D)) return FinishTrace(trace, false, "candidateType");
                if (ReadObserved(trace, "nativeAsset", () => AssetDatabase.IsNativeAsset(value))) return FinishTrace(trace, false, "nativeAsset");
                if (!ReadObserved(trace, "foreignAsset", () => AssetDatabase.IsForeignAsset(value))) return FinishTrace(trace, false, "foreignAsset");
                if (!ReadObserved(trace, "subAsset", () => AssetDatabase.IsSubAsset(value))) return FinishTrace(trace, false, "subAsset");
                if (ReadObserved(trace, "firstHideFlags", () => value.hideFlags) != HideFlags.NotEditable) return FinishTrace(trace, false, "firstHideFlags");
                path = ReadObserved(trace, "path", () => AssetDatabase.GetAssetPath(value));
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)) return FinishTrace(trace, false, "fontPath");
            }
            catch (Exception error) { if (trace != null) trace.exceptionType = error.GetType().FullName; throw; }
            try
            {
                AssetImporter importer = ReadObserved(trace, "importerLookup", () => AssetImporter.GetAtPath(path));
                if (ReadObserved(trace, "importerNull", () => importer == null)) return FinishTrace(trace, false, "importerNull");
                if (ReadObserved(trace, "importerType", () => importer.GetType()) != typeof(TrueTypeFontImporter)) return FinishTrace(trace, false, "importerType");
                if (ReadObserved(trace, "importerDirty", () => EditorUtility.IsDirty(importer))) return FinishTrace(trace, false, "importerDirty");
                Font[] owners = ReadObserved(trace, "ownerCount", () => Resources.FindObjectsOfTypeAll<Font>().Where(font => font.GetType() == typeof(Font) &&
                    EditorUtility.IsPersistent(font) && AssetDatabase.IsMainAsset(font) && AssetDatabase.IsForeignAsset(font) &&
                    !AssetDatabase.IsNativeAsset(font) && AssetDatabase.GetAssetPath(font) == path).ToArray());
                if (owners.Length != 1) return FinishTrace(trace, false, "ownerCount");
                if (ReadObserved(trace, "ownerDirty", () => EditorUtility.IsDirty(owners[0]))) return FinishTrace(trace, false, "ownerDirty");
                using (var serialized = ReadObserved(trace, "serializedOwner", () => new SerializedObject(owners[0])))
                {
                    SerializedProperty materialProperty = ReadObserved(trace, "materialPropertyLookup", () => serialized.FindProperty("m_DefaultMaterial"));
                    SerializedProperty textureProperty = ReadObserved(trace, "texturePropertyLookup", () => serialized.FindProperty("m_Texture"));
                    if (ReadObserved(trace, "materialPropertyNull", () => materialProperty == null)) return FinishTrace(trace, false, "materialPropertyNull");
                    if (ReadObserved(trace, "materialPropertyType", () => materialProperty.propertyType) != SerializedPropertyType.ObjectReference) return FinishTrace(trace, false, "materialPropertyType");
                    if (ReadObserved(trace, "texturePropertyNull", () => textureProperty == null)) return FinishTrace(trace, false, "texturePropertyNull");
                    if (ReadObserved(trace, "texturePropertyType", () => textureProperty.propertyType) != SerializedPropertyType.ObjectReference) return FinishTrace(trace, false, "texturePropertyType");
                    ulong materialId = ReadObserved(trace, "materialId", () => UnityEngine.EntityId.ToULong(materialProperty.objectReferenceEntityIdValue));
                    ulong textureId = ReadObserved(trace, "textureId", () => UnityEngine.EntityId.ToULong(textureProperty.objectReferenceEntityIdValue));
                    ulong candidateId = ReadObserved(trace, "candidateId", () => UnityEngine.EntityId.ToULong(value.GetEntityId()));
                    if (!IsExactSerializedFontOwnership(materialId, textureId, candidateId)) return FinishTrace(trace, false, materialId == 0 ? "materialIdZero" : textureId == 0 ? "textureIdZero" : "textureIdMismatch");
                }
                var texture = (Texture2D)value;
                // 实证同一ToJson可令导入缓存从256²变为0²；元数据必须核这次完整序列化之后的实际同阶段状态。
                string json = ReadObserved(trace, "textureJson", () => EditorJsonUtility.ToJson(texture));
                bool zeroMetadata = ReadObserved(trace, "width", () => texture.width) == 0 && ReadObserved(trace, "height", () => texture.height) == 0 &&
                    ReadObserved(trace, "format", () => texture.format) == TextureFormat.Alpha8 && !ReadObserved(trace, "readable", () => texture.isReadable) &&
                    ReadObserved(trace, "mipmapCount", () => texture.mipmapCount) == 1 && ReadObserved(trace, "dimension", () => texture.dimension) == UnityEngine.Rendering.TextureDimension.Tex2D;
                string failedMetadata = zeroMetadata ? null : trace?.lastAttempt;
                // 原6项仍严格&&短路；第二标志同样取序列化后实际状态，不新增预热或第二ToJson。
                HideFlags finalFlags = ReadObserved(trace, "secondHideFlags", () => texture.hideFlags);
                bool classified = IsExcludedCacheClassification(false, true, finalFlags, path, true, true, zeroMetadata, json);
                return FinishTrace(trace, classified, classified ? null : !zeroMetadata ? failedMetadata ?? "zeroMetadata" : finalFlags != HideFlags.NotEditable ? "secondHideFlags" : "zeroJson");
            }
            catch (Exception error) { if (trace != null) trace.exceptionType = error.GetType().FullName; return FinishTrace(trace, false, "exception"); }
        }

        /// <summary>恰执行一次原读取，记录只使用返回值的纯派生数据；JSON只记完整SHA/长度，数组只记数量，不保存对象或委托。</summary>
        /// <typeparam name="T">原读取的准确返回类型。</typeparam>
        /// <param name="trace">可选本次纯记录。</param><param name="name">固定读取点名称。</param><param name="read">原调用点的唯一实际读取；未调用的短路点不会进入此方法。</param>
        /// <returns>原读取返回值，异常不包装或重试。</returns>
        private static T ReadObserved<T>(DecisionTrace trace, string name, Func<T> read)
        {
            if (trace != null) trace.lastAttempt = name;
            T value = read();
            if (trace != null)
            {
                object result = value;
                trace.observed[name] = name == "textureJson" && result is string json ? (JToken)new JObject { ["sha256"] = GenerateActionCommon.ComputeTextHash(json), ["bytes"] = System.Text.Encoding.UTF8.GetByteCount(json) } :
                    result is Array array ? new JValue(array.Length) : result is Type type ? new JValue(type.FullName) : result is Enum kind ? new JValue(kind.ToString()) :
                    result == null ? JValue.CreateNull() : result is string || result is bool || result is int || result is ulong ? new JValue(result) : new JValue("returned-wrapper-only");
            }
            return value;
        }

        /// <summary>只记原判定结果及首个拒绝阶段，不执行任何额外读取。</summary>
        /// <param name="trace">可选本次纯记录。</param><param name="classified">原分类实际结果。</param><param name="rejectedAt">首个拒绝条件；通过时为null。</param>
        /// <returns>原classified原样返回。</returns>
        private static bool FinishTrace(DecisionTrace trace, bool classified, string rejectedAt) { if (trace != null) { trace.classified = classified; trace.rejectedAt = rejectedAt; } return classified; }

        /// <summary>主 Font 的直接纹理序列化 EntityId 无损转为64位比较；Material 只核原 ID 非零，不要求其已加载。</summary>
        /// <param name="materialId">m_DefaultMaterial 原对象引用 ID，只读且不解引用。</param>
        /// <param name="textureId">m_Texture 原对象引用 ID，必须非零。</param>
        /// <param name="candidateId">现存候选 Texture2D 的真实实例 ID。</param>
        /// <returns>直接引用准确指向候选且原默认 Material 引用存在时为 true。</returns>
        internal static bool IsExactSerializedFontOwnership(ulong materialId, ulong textureId, ulong candidateId) =>
            materialId != 0 && textureId != 0 && textureId == candidateId;

        /// <summary>完整比较当前已审零纹理序列化形状；未知、缺失或非零内容均不豁免。</summary>
        /// <param name="json">准确 Texture2D 的 EditorJsonUtility 原输出，不能用用户构造摘要替代真实对象。</param>
        /// <returns>完整形状、版本、名称、所有零数据与只读设置均精确吻合时为 true。</returns>
        internal static bool IsExactZeroCacheJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 4096) return false;
            try
            {
                var settings = new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error };
                return JToken.DeepEquals(JObject.Parse(json, settings), JObject.Parse(ZeroTextureJson));
            }
            catch (Newtonsoft.Json.JsonException) { return false; }
        }

        /// <summary>纯分类要求全部准确元数据与实际归属成立，不按 native=false、隐藏标志或文件后缀单独放行。</summary>
        /// <param name="nativeAsset">候选实际 IsNativeAsset；true 始终保留。</param>
        /// <param name="foreignSubAsset">候选同时 IsForeignAsset 与 IsSubAsset。</param>
        /// <param name="hideFlags">候选原标志，只接受准确 NotEditable。</param>
        /// <param name="assetPath">候选真实逻辑路径，只接受由准确导入器拥有的 ttf/otf。</param>
        /// <param name="exactCleanImporter">实际 importer 恰为 TrueTypeFontImporter 且原 dirty=false。</param>
        /// <param name="exactLoadedOwnership">唯一已加载主 Font 的 m_Texture 实例 ID 直接指向现存候选；m_DefaultMaterial 只核非零 ID，不加载或取 getter。</param>
        /// <param name="zeroReadonlyMetadata">候选真实 width/height=0、Alpha8、不可读及一个 mip 的 native 只读元数据。</param>
        /// <param name="textureJson">候选完整真实 JSON。</param>
        /// <returns>仅准确生成缓存分支为 true；其余由原脏资产门继续保护。</returns>
        internal static bool IsExcludedCacheClassification(bool nativeAsset, bool foreignSubAsset, HideFlags hideFlags, string assetPath,
            bool exactCleanImporter, bool exactLoadedOwnership, bool zeroReadonlyMetadata, string textureJson)
        {
            return !nativeAsset && foreignSubAsset && hideFlags == HideFlags.NotEditable &&
                !string.IsNullOrEmpty(assetPath) && (assetPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || assetPath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)) &&
                exactCleanImporter && exactLoadedOwnership && zeroReadonlyMetadata && IsExactZeroCacheJson(textureJson);
        }
    }
}
