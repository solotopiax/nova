"""官方配表克隆验证：真实模板、原压缩记录、业务类型与漂移门；不打开 Unity。"""

import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import nova_excel_template as excel
import nova_table_source as table
import openpyxl


class ExcelTemplateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="nova-excel-template-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.plan_path = table.STATE + "template-plan.json"
        self.receipt_path = table.STATE + "template-receipt.json"

    def request(self, kind="main-demo-sound", count=2):
        fields = excel.inspect_template(kind)["fields"]
        defaults = {"string": "业务值", "int": 3, "long": 2**63 - 1,
                    "float": 0.28, "double": 0.28, "bool": True}
        rows = [{**{item["name"]: defaults[item["type"]] for item in fields}, "Name": "Review_" + str(index)} for index in range(count)]
        return {"template": kind, "target": "Assets/Game/Excels/Test.xlsx", "targetHash": None,
                "sourceRoots": ["Assets/Game/Excels"], "generatedRoots": ["Assets/Game/Jsons"],
                "sheetName": "业务Sound", "title": "项目声音表", "rows": rows}

    def planned(self, request):
        plan = excel.plan_clone(self.root, request)
        table._atomic_write(self.root, self.plan_path, table._canonical(plan), state=True, replace=False)
        return plan

    def execute(self, plan):
        return excel.execute_plan(self.root, self.plan_path, plan["planId"], self.receipt_path)

    def test_real_templates_keep_styles_notes_metadata_and_exact_zip_records(self):
        """四个真实白名单分别克隆，内容类型正确，styles/备注页/批注/VML等压缩记录原样保留。"""
        for kind in excel.ROW_TEMPLATES:
            with self.subTest(template=kind), tempfile.TemporaryDirectory(prefix="nova-excel-case-") as directory:
                root = Path(directory).resolve()
                request = self.request(kind)
                plan = excel.plan_clone(root, request)
                table._atomic_write(root, self.plan_path, table._canonical(plan), state=True, replace=False)
                self.assertEqual(excel.execute_plan(root, self.plan_path, plan["planId"], self.receipt_path)["status"], "success")
                self.assertEqual(excel.verify_receipt(root, self.receipt_path)["status"], "success")
                template = excel._template(kind)
                output = (root / request["target"]).read_bytes()
                with zipfile.ZipFile(io.BytesIO(template["raw"])) as old, zipfile.ZipFile(io.BytesIO(output)) as new:
                    self.assertEqual(old.namelist(), new.namelist())
                    old_records = table._zip_records(template["raw"], old)[0]
                    new_records = table._zip_records(output, new)[0]
                    changed = {template["part"], "xl/workbook.xml"}
                    self.assertEqual({name for name in old_records if old_records[name] != new_records[name]}, changed)
                    for name in set(old_records) - changed:
                        self.assertEqual(old_records[name], new_records[name], name)
                book = openpyxl.load_workbook(io.BytesIO(output), read_only=True)
                try:
                    self.assertIn("#备注", book.sheetnames)
                    sheet = book[request["sheetName"]]
                    self.assertEqual(sheet["B1"].value, request["title"])
                    for offset, row in enumerate(request["rows"]):
                        for index, field in enumerate(template["fields"], 2):
                            self.assertEqual(sheet.cell(offset + 5, index).value, row[field["name"]])
                    self.assertTrue(all(cell.value is None for row in sheet.iter_rows(min_row=7) for cell in row))
                finally:
                    book.close()
                self.assertFalse((root / table.STATE / "write-lock.json").exists())
                self.assertEqual(list((root / request["target"]).parent.glob(".nova-table-source-*")), [])

    def test_appended_rows_inherit_model_row_and_cell_style_without_stale_sample_data(self):
        """超过原93物理行后新增业务行仍继承第5行，旧示例值不会漏入新表。"""
        request = self.request(count=100)
        self.execute(self.planned(request))
        template = excel._template(request["template"])
        model = ET.fromstring(template["text"]).find("{*}sheetData/{*}row[@r='5']")
        expected_attrs = {key: value for key, value in model.attrib.items() if key != "r"}
        expected_styles = {cell.get("r")[:-1]: cell.get("s") for cell in model}
        with zipfile.ZipFile(self.root / request["target"]) as archive:
            tree = ET.fromstring(archive.read(template["part"]))
        for row in tree.findall("{*}sheetData/{*}row"):
            number = int(row.get("r"))
            if 5 <= number <= 104:
                self.assertEqual({key: value for key, value in row.attrib.items() if key != "r"}, expected_attrs)
                for cell in row:
                    self.assertEqual(cell.get("s"), expected_styles[''.join(c for c in cell.get("r") if c.isalpha())])
        self.assertEqual(tree.find("{*}dimension").get("ref"), "A1:I104")
        self.assertEqual(excel.verify_receipt(self.root, self.receipt_path)["status"], "success")

    def test_existing_target_requires_exact_hash_and_plan_is_once_consumed(self):
        request = self.request()
        target = self.root / request["target"]
        target.parent.mkdir(parents=True)
        target.write_bytes(b"existing user workbook")
        with self.assertRaises(table.TableSourceError):
            excel.plan_clone(self.root, request)
        request["targetHash"] = table._hash(target.read_bytes())
        plan = self.planned(request)
        target.write_bytes(b"concurrent user change")
        with self.assertRaises(table.TableSourceError):
            self.execute(plan)
        self.assertFalse((self.root / self.receipt_path).exists())
        target.write_bytes(b"existing user workbook")
        self.assertEqual(self.execute(plan)["status"], "success")
        with self.assertRaises(table.TableSourceError):
            self.execute(plan)

    def test_reject_schema_changes_wrong_types_formulas_duplicate_names_and_bad_paths(self):
        variants = []
        base = self.request()
        variants += [{**base, "template": "../../arbitrary.xlsx"}, {**base, "unknown": True},
                     {**base, "target": "Packages/Test.xlsx"}, {**base, "target": "Assets/Game/Jsons/Test.xlsx"},
                     {**base, "sheetName": "#备注"}, {**base, "sheetName": "Invalid/Name"}]
        for row in [{**base["rows"][0], "Name": ""}, {**base["rows"][0], "Loop": 1},
                    {**base["rows"][0], "Desc": " =SUM(A1)"}, {**base["rows"][0], "TypeName": "SoundMP3"},
                    {key: value for key, value in base["rows"][0].items() if key != "Volume"}]:
            variants.append({**base, "rows": [row]})
        variants.append({**base, "rows": [base["rows"][0], base["rows"][0]]})
        for request in variants:
            with self.subTest(request=request), self.assertRaises(table.TableSourceError):
                excel.plan_clone(self.root, request)
        target = self.root / "Assets/Game/Excels"
        target.parent.mkdir(parents=True)
        target.symlink_to(self.root)
        with self.assertRaises(table.TableSourceError):
            excel.plan_clone(self.root, base)

    def test_template_drift_output_drift_and_incomplete_receipt_are_not_success(self):
        request = self.request()
        plan = self.planned(request)
        with patch.object(excel, "_template_path", return_value=self.root / "missing-template.xlsx"):
            with self.assertRaises(FileNotFoundError):
                self.execute(plan)
        self.assertFalse((self.root / self.receipt_path).exists())
        self.assertEqual(self.execute(plan)["status"], "success")
        target = self.root / request["target"]
        target.write_bytes(target.read_bytes() + b"drift")
        self.assertEqual(excel.verify_receipt(self.root, self.receipt_path)["status"], "partial")

    def test_template_hash_drift_is_refused_before_consuming_plan(self):
        request = self.request()
        plan = self.planned(request)
        template = excel._template(request["template"])
        changed = self.root / "template.xlsx"
        with zipfile.ZipFile(io.BytesIO(template["raw"])) as archive:
            core = archive.read("docProps/core.xml")
        changed.write_bytes(table._replace_raw_zip_members(template["raw"], {"docProps/core.xml": core.replace(b"</", b" </", 1)}))
        with patch.object(excel, "_template_path", return_value=changed):
            with self.assertRaises(table.TableSourceError):
                self.execute(plan)
        self.assertFalse((self.root / self.receipt_path).exists())
        self.assertFalse(table._read_state(self.root, self.plan_path)["executionStarted"])

    def test_failed_target_write_returns_partial_with_consumed_plan_and_readonly_verify(self):
        request = self.request()
        plan = self.planned(request)
        original_write = table._atomic_write
        def fail_target(root, relative, data, **options):
            if relative == request["target"]:
                raise OSError("simulated target failure")
            return original_write(root, relative, data, **options)
        with patch.object(table, "_atomic_write", side_effect=fail_target):
            self.assertEqual(self.execute(plan)["status"], "partial")
        self.assertFalse((self.root / request["target"]).exists())
        self.assertEqual(excel.verify_receipt(self.root, self.receipt_path)["status"], "partial")
        with self.assertRaises(table.TableSourceError):
            self.execute(plan)
        self.assertFalse((self.root / table.STATE / "write-lock.json").exists())

    def test_cli_plan_uses_stdin_without_echoing_business_values(self):
        request = self.request()
        output = io.StringIO()
        with patch("sys.stdin", io.StringIO(json.dumps(request))), contextlib.redirect_stdout(output):
            code = excel.main(["--project-root", str(self.root), "plan", "--request", "-", "--plan", self.plan_path])
        self.assertEqual(code, 0)
        self.assertNotIn("业务值", output.getvalue())
        self.assertEqual(json.loads(output.getvalue())["status"], "success")

    def copy_request(self, kind="localization-text"):
        """copy 只声明物料身份与目标，不让业务字段或Sheet命名混入原字节复制。"""
        return {"mode": "copy", "template": kind, "target": "Assets/Game/Excels/Prepared.xlsx",
                "targetHash": None, "sourceRoots": ["Assets/Game/Excels"], "generatedRoots": ["Assets/Game/Jsons"]}

    def test_copy_all_eleven_official_workbooks_and_two_samples_are_byte_identical(self):
        """全部11个官方xlsx及原两份MainDemo均执行/验证，复制结果逐字节一致。"""
        official = {name for kind, name in excel.TEMPLATES.items() if not kind.startswith("main-demo-")}
        framework = TOOLS.parents[1]
        self.assertEqual(official, {path.name for path in (framework / "Templates").glob("*.xlsx")})
        self.assertEqual(len(official), 11)
        for kind in excel.TEMPLATES:
            with self.subTest(template=kind), tempfile.TemporaryDirectory(prefix="nova-excel-copy-case-") as directory:
                root = Path(directory).resolve()
                request = self.copy_request(kind)
                probe = excel.inspect_template(kind, "copy")
                self.assertEqual(probe["mode"], "copy")
                self.assertNotIn("fields", probe)
                self.assertNotIn("sheetName", probe)
                original = excel._template_path(kind).read_bytes()
                self.assertEqual(probe["templateHash"], table._hash(original))
                with zipfile.ZipFile(io.BytesIO(original)) as archive:
                    self.assertEqual(probe["sheetNames"], [item.get("name") for item in ET.fromstring(archive.read("xl/workbook.xml")).findall("{*}sheets/{*}sheet")])
                plan = excel.plan_clone(root, request)
                self.assertEqual(plan["payload"]["outputHash"], plan["payload"]["templateHash"])
                table._atomic_write(root, self.plan_path, table._canonical(plan), state=True, replace=False)
                result = excel.execute_plan(root, self.plan_path, plan["planId"], self.receipt_path)
                self.assertEqual(result["status"], "success")
                self.assertEqual(result["mode"], "copy")
                self.assertNotIn("rowCount", result)
                self.assertNotIn("sheetName", result)
                self.assertEqual((root / request["target"]).read_bytes(), original)
                self.assertEqual(excel.verify_receipt(root, self.receipt_path)["status"], "success")
                with self.assertRaises(table.TableSourceError):
                    excel.execute_plan(root, self.plan_path, plan["planId"], self.receipt_path)
                self.assertFalse((root / table.STATE / "write-lock.json").exists())
                self.assertEqual(list((root / request["target"]).parent.glob(".nova-table-source-*")), [])

    def test_copy_preserves_localization_two_sheets_merges_and_hostkey_debug_release(self):
        """copy 不经过单业务Sheet、Name主键或禁止合并的rows门。"""
        localization = excel.inspect_template("localization-text", "copy")
        self.assertEqual(len(localization["sheetNames"]), 2)
        with zipfile.ZipFile(excel._template_path("localization-text")) as archive:
            for index in (1, 2):
                self.assertTrue(ET.fromstring(archive.read("xl/worksheets/sheet" + str(index) + ".xml")).findall("{*}mergeCells/{*}mergeCell"))
        hostkeys = excel.inspect_template("network-hostkeys", "copy")
        self.assertEqual(hostkeys["sheetNames"], ["NetworkHostKeysTemplate-Debug", "NetworkHostKeysTemplate-Release"])
        for kind in set(excel.TEMPLATES) - set(excel.ROW_TEMPLATES):
            with self.subTest(template=kind), self.assertRaises(table.TableSourceError):
                excel.inspect_template(kind)

    def test_copy_rejects_mixed_requests_and_rows_cannot_expand_to_other_templates(self):
        base = self.copy_request()
        invalid = [{**base, "rows": []}, {**base, "sheetName": "Changed"}, {**base, "title": "Changed"},
                   {**base, "mode": "rows"}, {**base, "mode": None}, {**base, "mode": []},
                   {**base, "mode": "COPY"}, {key: value for key, value in base.items() if key != "mode"},
                   {**base, "template": "../../outside.xlsx"},
                   {**self.request(), "template": "table-list"}, {**self.request(), "mode": "unexpected"}]
        for request in invalid:
            with self.subTest(request=request), self.assertRaises(table.TableSourceError):
                excel.plan_clone(self.root, request)
        self.assertFalse((self.root / base["target"]).exists())

    def test_copy_target_and_template_drift_are_blocked_before_receipt_or_consumption(self):
        request = self.copy_request()
        target = self.root / request["target"]
        target.parent.mkdir(parents=True)
        target.write_bytes(b"original existing workbook")
        with self.assertRaises(table.TableSourceError):
            excel.plan_clone(self.root, request)
        request["targetHash"] = table._hash(target.read_bytes())
        plan = self.planned(request)
        target.write_bytes(b"concurrent writer")
        with self.assertRaises(table.TableSourceError):
            self.execute(plan)
        target.write_bytes(b"original existing workbook")
        changed = self.root / "changed-template.xlsx"
        changed.write_bytes(excel._template_path(request["template"]).read_bytes() + b"changed-template")
        with patch.object(excel, "_template_path", return_value=changed):
            with self.assertRaises(table.TableSourceError):
                self.execute(plan)
        self.assertFalse((self.root / self.receipt_path).exists())
        self.assertFalse(table._read_state(self.root, self.plan_path)["executionStarted"])
        self.assertEqual(self.execute(plan)["status"], "success")
        target.write_bytes(target.read_bytes() + b"changed-output")
        self.assertEqual(excel.verify_receipt(self.root, self.receipt_path)["status"], "partial")

    def test_mode_or_rows_tampering_cannot_consume_a_plan(self):
        request = self.copy_request()
        plan = self.planned(request)
        stored = table._read_state(self.root, self.plan_path)
        stored["payload"]["request"]["mode"] = "rows"
        table._atomic_write(self.root, self.plan_path, table._canonical(stored), state=True)
        with self.assertRaises(table.TableSourceError):
            self.execute(plan)
        # 即使重算PlanId，严格请求白名单仍阻止把copy偷换成rows、或混入业务字段。
        stored["planId"] = table._hash(table._canonical(stored["payload"]))
        table._atomic_write(self.root, self.plan_path, table._canonical(stored), state=True)
        with self.assertRaises(table.TableSourceError):
            self.execute(stored)
        stored["payload"]["request"]["mode"] = "copy"
        stored["payload"]["request"]["rows"] = self.request()["rows"]
        stored["planId"] = table._hash(table._canonical(stored["payload"]))
        table._atomic_write(self.root, self.plan_path, table._canonical(stored), state=True)
        with self.assertRaises(table.TableSourceError):
            self.execute(stored)
        self.assertFalse((self.root / self.receipt_path).exists())
        self.assertFalse(table._read_state(self.root, self.plan_path)["executionStarted"])

    def test_explicit_rows_matches_legacy_request_and_cli_copy_inspection_has_no_data_claim(self):
        request = self.request()
        legacy = excel.plan_clone(self.root, request)
        explicit = excel.plan_clone(self.root, {**request, "mode": "rows"})
        self.assertNotIn("mode", legacy["payload"]["request"])
        self.assertEqual(legacy["version"], 1)
        self.assertEqual(legacy["payload"]["outputHash"], explicit["payload"]["outputHash"])
        self.assertEqual(excel._summary(legacy)["mode"], "rows")
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = excel.main(["--project-root", str(self.root), "inspect", "--mode", "copy", "--template", "localization-text"])
        self.assertEqual(code, 0)
        result = json.loads(output.getvalue())
        self.assertEqual(result["mode"], "copy")
        self.assertEqual(len(result["sheetNames"]), 2)
        self.assertNotIn("rowCount", result)


if __name__ == "__main__":
    unittest.main()
