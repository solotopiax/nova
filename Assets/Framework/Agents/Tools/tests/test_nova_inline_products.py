"""验证商品转换的真实字段、金额精度、整源冻结与数据最小化，不写 Unity 资产。"""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET
import zipfile

import openpyxl

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import nova_inline_products as products


class InlineProductsTests(unittest.TestCase):
    def setUp(self):
        """每例使用隔离项目目录，不触及真实表源或配置。"""
        self.temp = tempfile.TemporaryDirectory(prefix="nova-inline-products-test-")
        self.root = Path(self.temp.name).resolve()
        self.addCleanup(self.temp.cleanup)

    def workbook(self, rows, *, sheet="Products", other=None):
        """生成当前测试专属工作簿并返回实际整文件摘要。"""
        path = self.root / "Assets/Data/products.xlsx"
        path.parent.mkdir(parents=True, exist_ok=True)
        book = openpyxl.Workbook()
        page = book.active
        page.title = sheet
        for row in rows:
            page.append(row)
        if other:
            book.create_sheet("Unrelated").append(other)
        book.save(path)
        book.close()
        return path, hashlib.sha256(path.read_bytes()).hexdigest()

    def prepare(self, path, digest, *, sheet="Products", family="iap", coins=None):
        """调用真实只读转换入口，输入始终使用项目相对源身份。"""
        return products.prepare(self.root, path.relative_to(self.root).as_posix(), digest, sheet, family, coins_per_yuan=coins)

    def test_iap_full_mapping_preserves_ids_price_and_source(self):
        """九列完整映射保持字面价格和商品 ID，真实文件字节完全不变。"""
        path, digest = self.workbook([products.IAP_HEADERS, [4294967295, "月卡", "000123", "tp-7", "Subscription", 12, "0.9900", "USD", "配置备注"]])
        before = path.read_bytes()
        result = self.prepare(path, digest)
        entry = json.loads(result["field"]["valueJson"])[0]
        self.assertEqual(entry, {"m_TableId": 4294967295, "m_Name": "月卡", "m_ProductID": "000123", "m_ThirdProductID": "tp-7", "m_ProductType": "Subscription", "m_SubGroupID": 12, "m_Price": "0.9900", "m_Currency": "USD", "m_EditorNote": "配置备注"})
        self.assertEqual(result["configType"], "NovaFramework.SDK.IAP.Runtime.IAPPluginConfig")
        self.assertEqual(result["field"]["path"], "m_Products.m_Items")
        self.assertEqual(result["sourceRows"], [2])
        self.assertEqual(before, path.read_bytes())
        self.assertEqual(result["stage"], "prepared-only")

    def test_iap_luban_metadata_and_exact_type(self):
        """##var 模板元数据跳过，ProductType 必须为当前精确枚举名称。"""
        path, digest = self.workbook([["##var", *products.IAP_HEADERS], ["##type", "long"], ["", 1, "金币", "sku", "", "Consumable", "", "", "", ""]])
        self.assertEqual(self.prepare(path, digest)["sourceRows"], [3])
        path, digest = self.workbook([products.IAP_HEADERS, [1, "", "", "", "consumable", 0, "", "", ""]])
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest)

    def test_duplicate_id_and_bad_range_block_entire_list(self):
        """重复 ID 或超界时整体阻断，不输出部分成功商品。"""
        row = [1, "", "", "", "Consumable", 0, "1", "CNY", ""]
        for rows in ([row, row], [[4294967296, *row[1:]]]):
            path, digest = self.workbook([products.IAP_HEADERS, *rows])
            with self.assertRaises(products.InlineProductsError):
                self.prepare(path, digest)

    def test_formula_rejected_but_unrelated_data_never_output(self):
        """商品公式不使用缓存；其它页的凭据不进入商品结果。"""
        row = [1, "金币", "sku", "", "Consumable", 0, "1", "CNY", ""]
        path, digest = self.workbook([products.IAP_HEADERS, row], other=["Secret", "never-copy-secret"])
        result = self.prepare(path, digest)
        self.assertNotIn("never-copy-secret", json.dumps(result))
        row[6] = "=1+1"
        path, digest = self.workbook([products.IAP_HEADERS, row])
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest)

    def test_source_hash_drift_and_wrong_sheet_block(self):
        """整源漂移或指定页缺失不能回退选择首张工作表。"""
        path, digest = self.workbook([products.IAP_HEADERS, [1, "", "", "", "Consumable", 0, "", "", ""]])
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, "0" * 64)
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest, sheet="Missing")

    def test_empty_list_is_not_implicit_delete(self):
        """仅表头不会被当作清空商品列表的已授权批量导入。"""
        path, digest = self.workbook([products.IAP_HEADERS])
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest)

    def test_wechat_template_and_currency_rate(self):
        """币档金额为总价，人民币分与兑换比例一致才生成完整配置字段。"""
        headers = ["TableId", "Name", "Kind", "ProductId", "PriceFen", "Quantity", "EditorNote"]
        path, digest = self.workbook([headers, [1, "币档", "GameCurrency", "", 600, 60, ""]], sheet="Purchases")
        result = self.prepare(path, digest, sheet="Purchases", family="wechat", coins=10)
        entry = json.loads(result["field"]["valueJson"])[0]
        self.assertEqual(entry["m_Kind"], "GameCurrency")
        self.assertEqual(entry["m_ProductId"], "")
        self.assertEqual(result["field"]["path"], "m_PurchaseList.m_Items")
        for rate in (None, 0, 11):
            with self.assertRaises(products.InlineProductsError):
                self.prepare(path, digest, sheet="Purchases", family="wechat", coins=rate)

    def test_wechat_solar_yuan_exactness_and_aliases(self):
        """Solar ID/Count/Desc 别名与数值 Kind 可复用，Price 元精确转分。"""
        headers = ["#", "ID", "Name", "Kind", "ProductID", "Price", "Currency", "Count", "Desc"]
        row = ["", 2, "道具", "2", "0007", "1.23", "RMB", 3, "说明"]
        path, digest = self.workbook([headers, row], sheet="China_WX")
        entry = json.loads(self.prepare(path, digest, sheet="China_WX", family="wechat")["field"]["valueJson"])[0]
        self.assertEqual((entry["m_PriceFen"], entry["m_Quantity"], entry["m_ProductId"]), (123, 3, "0007"))
        for invalid in ("1.231", "1.230000000000000000000000000000000000000000000000001", "NaN", "-1"):
            row[5] = invalid
            path, digest = self.workbook([headers, row], sheet="China_WX")
            with self.assertRaises(products.InlineProductsError):
                self.prepare(path, digest, sheet="China_WX", family="wechat")

    def test_wechat_missing_kind_or_item_id_and_duplicate_header_block(self):
        """不从商品名称猜 Kind；道具缺 ID 或大小写重复列时拒绝。"""
        for headers, row in ([['TableId', 'Name', 'PriceFen', 'Quantity'], [1, '道具', 100, 1]], [['TableId', 'Kind', 'PriceFen', 'Quantity'], [1, 'GameItem', 100, 1]], [['TableId', 'Kind', 'PriceFen', 'Quantity', 'quantity'], [1, 'GameCurrency', 100, 10, 10]]):
            path, digest = self.workbook([headers, row], sheet="Purchases")
            with self.assertRaises(products.InlineProductsError):
                self.prepare(path, digest, sheet="Purchases", family="wechat", coins=10)

    def test_secret_value_error_is_redacted(self):
        """明显凭据误填到商品字段时整体阻断且错误不含原值。"""
        secret = "Bearer sensitive-example"
        path, digest = self.workbook([products.IAP_HEADERS, [1, secret, "", "", "Consumable", 0, "", "", ""]])
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = products.main(["--project-root", str(self.root), "--source", path.relative_to(self.root).as_posix(), "--source-hash", digest, "--sheet", "Products", "--family", "iap"])
        self.assertEqual(code, 1)
        self.assertNotIn(secret, output.getvalue())
        self.assertEqual(json.loads(output.getvalue())["status"], "blocked")

    def test_macro_duplicate_zip_symlink_and_package_source_block(self):
        """宏、重复 ZIP 部件、链接及包内工作簿不进入项目商品转换。"""
        path, digest = self.workbook([products.IAP_HEADERS, [1, "", "", "", "Consumable", 0, "", "", ""]])
        with zipfile.ZipFile(path, "a") as archive:
            archive.writestr("xl/vbaProject.bin", b"macro")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest)
        link = self.root / "linked.xlsx"
        link.symlink_to(path)
        for source in ("linked.xlsx", "../products.xlsx", "Packages/products.xlsx"):
            with self.assertRaises(products.InlineProductsError):
                products.prepare(self.root, source, digest, "Products", "iap")

    def test_field_json_limit_blocks_instead_of_partial_replace(self):
        """超现有字段 JSON 容量整体阻断，不能拆列表导致覆盖丢行。"""
        row = [1, "x" * 4000, "sku", "", "Consumable", 0, "1", "CNY", ""]
        path, digest = self.workbook([products.IAP_HEADERS, *[[i + 1, *row[1:]] for i in range(10)]])
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest)

    def test_gateway_utf8_limit_blocks_multibyte_list_without_partial_output(self):
        """中文列表字符数未超旧限制但UTF8超16KiB时整体阻断，源不变且不拆覆盖。"""
        row = [1, "中" * 2000, "sku", "", "Consumable", 0, "1", "CNY", ""]
        path, digest = self.workbook([products.IAP_HEADERS, *[[i + 1, *row[1:]] for i in range(3)]])
        before = path.read_bytes()
        with self.assertRaises(products.InlineProductsError):
            self.prepare(path, digest)
        self.assertEqual(path.read_bytes(), before)
        path, digest = self.workbook([products.IAP_HEADERS, row])
        value = self.prepare(path, digest)["field"]["valueJson"]
        self.assertLessEqual(len(value.encode("utf-8")), 16 * 1024)

    def test_invalid_shared_string_indices_block(self):
        """共享字符串负索引和越界不能借用其它文本形成合法商品表。"""
        path, _ = self.workbook([products.IAP_HEADERS, [1, "", "", "", "Consumable", 0, "", "", ""]])
        raw = path.read_bytes()
        namespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
        for index in ("-1", "1"):
            output = io.BytesIO()
            with zipfile.ZipFile(io.BytesIO(raw)) as original, zipfile.ZipFile(output, "w") as changed:
                for item in original.infolist():
                    content = original.read(item.filename)
                    if item.filename == "xl/worksheets/sheet1.xml":
                        sheet = ET.fromstring(content)
                        cell = next(c for c in sheet.findall("{*}sheetData/{*}row/{*}c") if c.attrib["r"] == "A1")
                        cell.clear()
                        cell.attrib.update(r="A1", t="s")
                        ET.SubElement(cell, "{" + namespace + "}v").text = index
                        content = ET.tostring(sheet)
                    changed.writestr(item, content)
                changed.writestr("xl/sharedStrings.xml", '<sst xmlns="' + namespace + '"><si><t>TableId</t></si></sst>')
            with self.assertRaises(products.InlineProductsError):
                products.workbook_rows(output.getvalue(), "Products")

    def test_io_failure_is_structured_and_redacted(self):
        """源读取失败返回固定 blocked，不输出路径、底层错误或凭据原文。"""
        output = io.StringIO()
        with mock.patch.object(products, "prepare", side_effect=OSError("secret-source-and-path")), contextlib.redirect_stdout(output):
            code = products.main(["--project-root", str(self.root), "--source", "Assets/Data/products.xlsx", "--source-hash", "0" * 64, "--sheet", "Products", "--family", "iap"])
        self.assertEqual(code, 1)
        result = json.loads(output.getvalue())
        self.assertEqual(result["status"], "blocked")
        self.assertNotIn("secret-source-and-path", output.getvalue())


if __name__ == "__main__":
    unittest.main()
