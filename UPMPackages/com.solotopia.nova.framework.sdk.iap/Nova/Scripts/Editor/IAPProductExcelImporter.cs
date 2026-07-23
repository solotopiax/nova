/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductExcelImporter.cs
 * author:    Codex
 * created:   2026/7/23
 * descrip:   IAP 商品 Excel 导入工具，负责解析 Products Sheet 并写入 IAPProductList 序列化数据
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Editor;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// IAP 商品 Excel 导入工具。
    /// 该类只服务 Editor 配置导入，不改变运行时 IAP 商品表读取链路。
    /// </summary>
    public static class IAPProductExcelImporter
    {
        /// <summary>
        /// IAP 商品 Excel 的历史固定 Sheet 名称；导入时优先读取该 Sheet，缺失时读取第一个 Sheet。
        /// </summary>
        public const string c_SheetName = "Products";

        /// <summary>
        /// 商品表 ID 最小有效值；0 在支付流程中作为无效 ID 使用。
        /// </summary>
        public const long c_MinTableId = 1L;

        /// <summary>
        /// 商品表 ID 最大有效值；移动端透传 GUID 只保留 8 位十六进制槽位。
        /// </summary>
        public const long c_MaxTableId = 4294967295L;

        /// <summary>
        /// IAP 商品 Excel 模板的默认文件名。
        /// </summary>
        public const string c_DefaultWorkbookName = "IAPProducts.xlsx";

        /// <summary>
        /// IAP 商品 Excel 模板源文件名。
        /// </summary>
        private const string c_TemplateWorkbookName = "IAPProductsTemplate.xlsx";

        /// <summary>
        /// 本地 UPM 工作区中的 IAP 商品模板相对工程根目录路径。
        /// </summary>
        private const string c_UpmTemplateRelativePath = "UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Templates/IAPProductsTemplate.xlsx";

        /// <summary>
        /// 已发布 Package 中的 IAP 商品模板相对工程根目录路径。
        /// </summary>
        private const string c_PackageTemplateRelativePath = "Packages/com.solotopia.nova.framework.sdk.iap/Nova/Templates/IAPProductsTemplate.xlsx";

        /// <summary>
        /// Products Sheet 的固定表头。
        /// </summary>
        private static readonly string[] s_Headers =
        {
            "TableId",
            "Name",
            "ProductID",
            "ThirdProductID",
            "ProductType",
            "SubGroupID",
            "Price",
            "Currency",
            "EditorNote"
        };

        /// <summary>
        /// 从 Excel 文件读取并解析 IAP 商品导入数据。
        /// </summary>
        /// <param name="filePath">Excel 文件绝对路径。</param>
        /// <returns>导入解析结果。</returns>
        public static IAPProductExcelImportResult ReadFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return IAPProductExcelImportResult.Failed("Excel 文件路径为空。");
            }

            if (!EditorUtil.Excel.IsExcelFile(filePath))
            {
                return IAPProductExcelImportResult.Failed($"文件不是有效 Excel 格式：{filePath}");
            }

            try
            {
                List<string> sheetNames = EditorUtil.Excel.GetSheetNames(filePath);
                string sheetName = ResolveReadableSheetName(sheetNames);
                List<IReadOnlyList<string>> rows = EditorUtil.Excel.ReadSheet(filePath, sheetName);
                return ParseRows(rows);
            }
            catch (Exception ex)
            {
                return IAPProductExcelImportResult.Failed($"读取 Excel 失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 根据工作簿 Sheet 列表选择本次导入要读取的 Sheet。
        /// 优先读取历史固定名 Products；不存在时回退读取第一个 Sheet，兼容模板或业务方重命名 Sheet 的情况。
        /// </summary>
        /// <param name="sheetNames">工作簿中的 Sheet 名称列表，顺序保持 Excel 原始顺序。</param>
        /// <returns>本次导入应读取的 Sheet 名称。</returns>
        public static string ResolveReadableSheetName(IReadOnlyList<string> sheetNames)
        {
            if (sheetNames == null || sheetNames.Count == 0)
            {
                throw new KeyNotFoundException("Excel 文件中不存在可读取的 Sheet。");
            }

            for (int i = 0; i < sheetNames.Count; i++)
            {
                string sheetName = sheetNames[i];
                if (string.Equals(sheetName, c_SheetName, StringComparison.Ordinal))
                {
                    return sheetName;
                }
            }

            return sheetNames[0];
        }

        /// <summary>
        /// 解析 Products Sheet 的行列数据。
        /// 第一行必须是固定表头，空行会被忽略，其余行按字段规则校验。
        /// </summary>
        /// <param name="rows">Products Sheet 行列数据。</param>
        /// <returns>导入解析结果。</returns>
        public static IAPProductExcelImportResult ParseRows(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var products = new List<IAPProductExcelProduct>();
            var errors = new List<string>();
            var tableIds = new HashSet<long>();

            if (rows == null || rows.Count == 0)
            {
                return IAPProductExcelImportResult.Failed("Products Sheet 为空。");
            }

            ProductSheetLayout layout = ResolveLayout(rows, errors);
            if (errors.Count > 0)
            {
                return new IAPProductExcelImportResult(products, errors);
            }

            for (int i = layout.DataStartRowIndex; i < rows.Count; i++)
            {
                IReadOnlyList<string> row = rows[i];
                if (IsEmptyRow(row) || IsMetadataRow(row))
                {
                    continue;
                }

                int excelRow = i + 1;
                ParseRow(row, excelRow, layout.ColumnOffset, products, errors, tableIds);
            }

            return new IAPProductExcelImportResult(products, errors);
        }

        /// <summary>
        /// 将解析后的商品数据全量覆盖写入 m_Items 数组属性。
        /// </summary>
        /// <param name="itemsProp">IAPProductList.m_Items 数组属性。</param>
        /// <param name="products">解析后的商品数据。</param>
        public static void ApplyProducts(SerializedProperty itemsProp, IReadOnlyList<IAPProductExcelProduct> products)
        {
            if (itemsProp == null || !itemsProp.isArray)
            {
                throw new ArgumentException("IAP 商品列表属性无效。");
            }

            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            Undo.RecordObjects(itemsProp.serializedObject.targetObjects, "Import IAP SKU Excel");
            itemsProp.ClearArray();

            for (int i = 0; i < products.Count; i++)
            {
                IAPProductExcelProduct product = products[i];
                itemsProp.InsertArrayElementAtIndex(i);
                SerializedProperty entryProp = itemsProp.GetArrayElementAtIndex(i);
                WriteProduct(entryProp, product);
            }

            itemsProp.serializedObject.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in itemsProp.serializedObject.targetObjects)
            {
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// 将包内模板导出到用户选择的目标路径。
        /// </summary>
        /// <param name="destPath">目标 Excel 文件绝对路径。</param>
        public static void ExportTemplate(string destPath)
        {
            if (string.IsNullOrEmpty(destPath))
            {
                return;
            }

            string sourcePath = FindTemplatePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new InvalidOperationException($"未找到 IAP SKU 模板：{c_TemplateWorkbookName}");
            }

            string destDirectory = Util.SysIO.Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDirectory))
            {
                Util.SysIO.Directory.CreateIfNotExist(destDirectory);
            }

            System.IO.File.Copy(sourcePath, destPath, overwrite: true);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 查找包内 IAP SKU 模板的绝对路径。
        /// </summary>
        /// <returns>模板绝对路径；未找到时返回空字符串。</returns>
        public static string FindTemplatePath()
        {
            string projectRoot = Util.SysIO.Path.GetDirectoryName(Application.dataPath);
            string upmPath = Util.SysIO.Path.Combine(projectRoot, c_UpmTemplateRelativePath);
            if (Util.SysIO.File.Exists(upmPath))
            {
                return upmPath;
            }

            string packagePath = Util.SysIO.Path.Combine(projectRoot, c_PackageTemplateRelativePath);
            if (Util.SysIO.File.Exists(packagePath))
            {
                return packagePath;
            }

            return string.Empty;
        }

        /// <summary>
        /// 校验 Products Sheet 表头是否与固定模板一致。
        /// </summary>
        /// <param name="headerRow">表头行。</param>
        /// <param name="errors">错误收集列表。</param>
        private static ProductSheetLayout ResolveLayout(IReadOnlyList<IReadOnlyList<string>> rows, List<string> errors)
        {
            if (ValidateHeader(rows[0], 0))
            {
                return new ProductSheetLayout(0, 1);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                IReadOnlyList<string> row = rows[i];
                if (string.Equals(GetCell(row, 0), "##var", StringComparison.Ordinal))
                {
                    if (!ValidateHeader(row, 1))
                    {
                        errors.Add("##var 行字段必须依次为 TableId、Name、ProductID、ThirdProductID、ProductType、SubGroupID、Price、Currency、EditorNote。");
                        return default;
                    }

                    return new ProductSheetLayout(1, i + 1);
                }
            }

            ValidateHeader(rows[0], 0, errors);
            return default;
        }

        /// <summary>
        /// 校验 Products Sheet 表头是否与固定模板一致。
        /// </summary>
        /// <param name="headerRow">表头行。</param>
        /// <param name="columnOffset">字段开始列偏移。</param>
        /// <returns>表头匹配时返回 true。</returns>
        private static bool ValidateHeader(IReadOnlyList<string> headerRow, int columnOffset)
        {
            if (headerRow == null)
            {
                return false;
            }

            for (int i = 0; i < s_Headers.Length; i++)
            {
                string actual = GetCell(headerRow, i + columnOffset);
                if (!string.Equals(actual, s_Headers[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 校验 Products Sheet 表头是否与固定模板一致。
        /// </summary>
        /// <param name="headerRow">表头行。</param>
        /// <param name="columnOffset">字段开始列偏移。</param>
        /// <param name="errors">错误收集列表。</param>
        private static void ValidateHeader(IReadOnlyList<string> headerRow, int columnOffset, List<string> errors)
        {
            if (headerRow == null)
            {
                errors.Add("Products Sheet 缺少表头行。");
                return;
            }

            for (int i = 0; i < s_Headers.Length; i++)
            {
                string actual = GetCell(headerRow, i + columnOffset);
                if (!string.Equals(actual, s_Headers[i], StringComparison.Ordinal))
                {
                    errors.Add($"表头第 {i + 1} 列应为 {s_Headers[i]}，实际为 {actual}。");
                }
            }
        }

        /// <summary>
        /// 解析并校验单行商品数据。
        /// </summary>
        /// <param name="row">Excel 行数据。</param>
        /// <param name="excelRow">Excel 行号。</param>
        /// <param name="products">解析成功的商品列表。</param>
        /// <param name="errors">错误收集列表。</param>
        /// <param name="tableIds">已出现的 TableId 集合。</param>
        private static void ParseRow(
            IReadOnlyList<string> row,
            int excelRow,
            int columnOffset,
            List<IAPProductExcelProduct> products,
            List<string> errors,
            HashSet<long> tableIds)
        {
            int errorCountBeforeRow = errors.Count;
            string tableIdText = GetCell(row, columnOffset);
            string productTypeText = GetCell(row, columnOffset + 4);
            string subGroupIdText = GetCell(row, columnOffset + 5);

            if (!long.TryParse(tableIdText, out long tableId) || tableId < c_MinTableId || tableId > c_MaxTableId)
            {
                errors.Add($"第 {excelRow} 行 TableId 必须是 {c_MinTableId}~{c_MaxTableId} 范围内的整数。");
            }
            else if (!tableIds.Add(tableId))
            {
                errors.Add($"第 {excelRow} 行 TableId 重复：{tableId}。");
            }

            IAPProductType productType = default;
            if (!IsProductTypeName(productTypeText) || !Enum.TryParse(productTypeText, false, out productType))
            {
                errors.Add($"第 {excelRow} 行 ProductType 必须是 IAPProductType 枚举名。");
            }

            int subGroupId = 0;
            if (!string.IsNullOrEmpty(subGroupIdText) && !int.TryParse(subGroupIdText, out subGroupId))
            {
                errors.Add($"第 {excelRow} 行 SubGroupID 必须是整数。");
            }

            if (errors.Count > errorCountBeforeRow)
            {
                return;
            }

            products.Add(new IAPProductExcelProduct(
                tableId,
                GetCell(row, columnOffset + 1),
                GetCell(row, columnOffset + 2),
                GetCell(row, columnOffset + 3),
                productType,
                subGroupId,
                GetCell(row, columnOffset + 6),
                GetCell(row, columnOffset + 7),
                GetCell(row, columnOffset + 8)));
        }

        /// <summary>
        /// 判断一行是否为空行。
        /// </summary>
        /// <param name="row">Excel 行数据。</param>
        /// <returns>所有列为空时返回 true。</returns>
        private static bool IsEmptyRow(IReadOnlyList<string> row)
        {
            if (row == null)
            {
                return true;
            }

            for (int i = 0; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断一行是否为模板元信息行。
        /// </summary>
        /// <param name="row">Excel 行数据。</param>
        /// <returns>A 列以 ## 开头时返回 true。</returns>
        private static bool IsMetadataRow(IReadOnlyList<string> row)
        {
            string firstCell = GetCell(row, 0);
            return firstCell.StartsWith("##", StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断文本是否为 IAPProductType 的枚举名。
        /// 数字值不会被视为有效枚举名，避免 Excel 绕过模板下拉约束。
        /// </summary>
        /// <param name="value">ProductType 单元格文本。</param>
        /// <returns>文本精确匹配枚举名时返回 true。</returns>
        private static bool IsProductTypeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] names = Enum.GetNames(typeof(IAPProductType));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 安全读取并裁剪单元格字符串。
        /// </summary>
        /// <param name="row">Excel 行数据。</param>
        /// <param name="index">列下标。</param>
        /// <returns>单元格文本；越界时返回空字符串。</returns>
        private static string GetCell(IReadOnlyList<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count)
            {
                return string.Empty;
            }

            return (row[index] ?? string.Empty).Trim();
        }

        /// <summary>
        /// 将一条导入商品写入序列化条目。
        /// </summary>
        /// <param name="entryProp">IAPProductEntry 序列化属性。</param>
        /// <param name="product">导入商品数据。</param>
        private static void WriteProduct(SerializedProperty entryProp, IAPProductExcelProduct product)
        {
            entryProp.FindPropertyRelative("m_TableId").longValue = product.TableId;
            entryProp.FindPropertyRelative("m_Name").stringValue = product.Name;
            entryProp.FindPropertyRelative("m_ProductID").stringValue = product.ProductID;
            entryProp.FindPropertyRelative("m_ThirdProductID").stringValue = product.ThirdProductID;
            entryProp.FindPropertyRelative("m_ProductType").enumValueIndex = (int)product.ProductType;
            entryProp.FindPropertyRelative("m_SubGroupID").intValue = product.SubGroupID;
            entryProp.FindPropertyRelative("m_Price").stringValue = product.Price;
            entryProp.FindPropertyRelative("m_Currency").stringValue = product.Currency;
            entryProp.FindPropertyRelative("m_EditorNote").stringValue = product.EditorNote;
        }

        /// <summary>
        /// Products Sheet 的解析布局信息。
        /// </summary>
        private readonly struct ProductSheetLayout
        {
            /// <summary>
            /// 创建 Products Sheet 解析布局信息。
            /// </summary>
            /// <param name="columnOffset">字段开始列偏移。</param>
            /// <param name="dataStartRowIndex">数据起始行下标。</param>
            public ProductSheetLayout(int columnOffset, int dataStartRowIndex)
            {
                ColumnOffset = columnOffset;
                DataStartRowIndex = dataStartRowIndex;
            }

            /// <summary>
            /// 字段开始列偏移。
            /// </summary>
            public int ColumnOffset { get; }

            /// <summary>
            /// 数据起始行下标。
            /// </summary>
            public int DataStartRowIndex { get; }
        }
    }

    /// <summary>
    /// IAP 商品 Excel 单行解析结果。
    /// </summary>
    public sealed class IAPProductExcelProduct
    {
        /// <summary>
        /// 创建一条 IAP 商品 Excel 单行解析结果。
        /// </summary>
        /// <param name="tableId">商品表 ID。</param>
        /// <param name="name">商品名称。</param>
        /// <param name="productID">平台商品 ID。</param>
        /// <param name="thirdProductID">第三方支付商品 ID。</param>
        /// <param name="productType">商品类型。</param>
        /// <param name="subGroupID">订阅组 ID。</param>
        /// <param name="price">价格文本。</param>
        /// <param name="currency">货币码。</param>
        /// <param name="editorNote">编辑器备注。</param>
        public IAPProductExcelProduct(
            long tableId,
            string name,
            string productID,
            string thirdProductID,
            IAPProductType productType,
            int subGroupID,
            string price,
            string currency,
            string editorNote)
        {
            TableId = tableId;
            Name = name;
            ProductID = productID;
            ThirdProductID = thirdProductID;
            ProductType = productType;
            SubGroupID = subGroupID;
            Price = price;
            Currency = currency;
            EditorNote = editorNote;
        }

        /// <summary>
        /// 商品表 ID。
        /// </summary>
        public long TableId { get; }

        /// <summary>
        /// 商品名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 平台商品 ID。
        /// </summary>
        public string ProductID { get; }

        /// <summary>
        /// 第三方支付商品 ID。
        /// </summary>
        public string ThirdProductID { get; }

        /// <summary>
        /// 商品类型。
        /// </summary>
        public IAPProductType ProductType { get; }

        /// <summary>
        /// 订阅组 ID。
        /// </summary>
        public int SubGroupID { get; }

        /// <summary>
        /// 价格文本。
        /// </summary>
        public string Price { get; }

        /// <summary>
        /// 货币码。
        /// </summary>
        public string Currency { get; }

        /// <summary>
        /// 编辑器备注。
        /// </summary>
        public string EditorNote { get; }
    }

    /// <summary>
    /// IAP 商品 Excel 导入解析结果。
    /// </summary>
    public sealed class IAPProductExcelImportResult
    {
        /// <summary>
        /// 创建 IAP 商品 Excel 导入解析结果。
        /// </summary>
        /// <param name="products">解析成功的商品列表。</param>
        /// <param name="errors">导入错误列表。</param>
        public IAPProductExcelImportResult(
            IReadOnlyList<IAPProductExcelProduct> products,
            IReadOnlyList<string> errors)
        {
            Products = products ?? Array.Empty<IAPProductExcelProduct>();
            Errors = errors ?? Array.Empty<string>();
        }

        /// <summary>
        /// 是否解析成功。
        /// </summary>
        public bool Success => Errors.Count == 0;

        /// <summary>
        /// 解析成功的商品列表。
        /// </summary>
        public IReadOnlyList<IAPProductExcelProduct> Products { get; }

        /// <summary>
        /// 导入错误列表。
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        /// <param name="error">错误内容。</param>
        /// <returns>失败结果。</returns>
        public static IAPProductExcelImportResult Failed(string error)
        {
            return new IAPProductExcelImportResult(
                Array.Empty<IAPProductExcelProduct>(),
                new[] { error });
        }
    }
}
