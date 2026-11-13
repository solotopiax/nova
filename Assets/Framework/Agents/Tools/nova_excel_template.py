#!/usr/bin/env python3
"""复制随包官方 Excel 物料，或按 UI/Sound 模板填业务行，保留原有工作簿风格。"""

from __future__ import annotations

import argparse
import io
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from xml.sax.saxutils import quoteattr
import zipfile

import nova_table_source as table


VERSION = 1
MAX_CELLS = 8192
ROW_TEMPLATES = {"ui": "UITemplate.xlsx", "sound": "SoundTemplate.xlsx",
                 "main-demo-ui": "UIs/UIs.xlsx", "main-demo-sound": "Sounds/Sounds.xlsx"}
TEMPLATES = {**ROW_TEMPLATES,
             "table-list": "TableListTemplate.xlsx", "table-map": "TableMapTemplate.xlsx",
             "table-one": "TableOneTemplate.xlsx", "localization-text": "LocalizationTextTemplate.xlsx",
             "localization-font": "LocalizationFontTemplate.xlsx", "network-hostkeys": "NetworkHostKeysTemplate.xlsx",
             "network-cmds": "NetworkCmdsTemplate.xlsx", "vibration-emphasis": "VibrateEmphasisTemplate.xlsx",
             "vibration-custom": "VibrateCustomTemplate.xlsx"}
REQUEST_KEYS = {"template", "target", "targetHash", "sourceRoots", "generatedRoots", "sheetName", "rows", "title"}
COPY_REQUEST_KEYS = {"mode", "template", "target", "targetHash", "sourceRoots", "generatedRoots"}


def _template_path(kind: str) -> Path:
    """只读脚本所在 Framework 的白名单模板，不接受任意来源路径或外部 URL。"""
    if not isinstance(kind, str) or kind not in TEMPLATES:
        raise table.TableSourceError("template 必须是随包官方白名单中的精确 ID。")
    framework = Path(__file__).resolve().parents[2]
    if kind.startswith("main-demo-"):
        sample = framework / "Samples~/MainDemo/Excels"
        # 开发仓的正式 Sample 尚未由发布器复制进 Framework/Samples~。
        if not sample.is_dir() and framework.parent.name == "Assets":
            sample = framework.parent / "Samples/MainDemo/Excels"
        path = sample / TEMPLATES[kind]
    else:
        path = framework / "Templates" / TEMPLATES[kind]
    if any(item.is_symlink() for item in (path, *path.parents)) or not path.is_file():
        raise table.TableSourceError("官方模板不存在或路径包含符号链接。")
    return path


def _address(match) -> str:
    """从物理单元格读取唯一地址，不通过行号猜业务字段。"""
    found = re.search(r'\br\s*=\s*([\'"])([A-Z]+[1-9][0-9]*)\1', match.group("attrs"))
    if not found:
        raise table.TableSourceError("模板物理单元格地址无效。")
    return found.group(2)


def _value(cell, strings: list[str]):
    """读取共享字符串或内联字符串；模板只使用显式标量字段。"""
    value = cell.find("{*}v")
    if cell.get("t") == "s":
        return strings[int(value.text)]
    if cell.get("t") == "inlineStr":
        return "".join(cell.find("{*}is").itertext())
    return value.text if value is not None else None


def _copy_template(kind: str) -> dict:
    """只读完整工作簿及 Sheet 身份；不解释业务字段、数据行、合并或公式。"""
    raw = _template_path(kind).read_bytes()
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        if len(archive.namelist()) != len(set(archive.namelist())):
            raise table.TableSourceError("官方模板包含重复 ZIP 成员。")
        workbook = ET.fromstring(archive.read("xl/workbook.xml"))
        sheets = [item.get("name") for item in workbook.findall("{*}sheets/{*}sheet")]
        if not sheets or any(not isinstance(name, str) or not name for name in sheets) or len(sheets) != len(set(sheets)):
            raise table.TableSourceError("官方模板的 Sheet 名称缺失或重复。")
    return {"raw": raw, "sheetNames": sheets}


def _mode(mode: str) -> str:
    """模式必须显式有效；缺省由调用方沿用原 rows 契约，不推断请求意图。"""
    if not isinstance(mode, str) or mode not in {"rows", "copy"}:
        raise table.TableSourceError("mode 仅支持 rows 或 copy。")
    return mode


def _template(kind: str) -> dict:
    """核对官方表的四行元数据、数据样式行及唯一业务 Sheet。"""
    if not isinstance(kind, str) or kind not in ROW_TEMPLATES:
        raise table.TableSourceError("rows 仅支持 ui、sound、main-demo-ui、main-demo-sound；其他官方表仅可用 copy 准备物料。")
    raw = _template_path(kind).read_bytes()
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or any(name.lower().endswith(".bin") for name in names):
            raise table.TableSourceError("模板包含重复成员或不支持的二进制扩展。")
        table._zip_records(raw, archive)
        workbook = ET.fromstring(archive.read("xl/workbook.xml"))
        sheets = workbook.findall("{*}sheets/{*}sheet")
        data_sheets = [item for item in sheets if not item.get("name", "").startswith("#")]
        if len(data_sheets) != 1:
            raise table.TableSourceError("官方模板必须有唯一业务 Sheet。")
        sheet = data_sheets[0]
        relationships = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
        identity = next(v for k, v in sheet.attrib.items() if k.endswith("}id"))
        targets = [item.get("Target") for item in relationships if item.get("Id") == identity and item.get("TargetMode") != "External"]
        if len(targets) != 1:
            raise table.TableSourceError("模板 Sheet 关系不唯一。")
        part = targets[0].lstrip("/") if targets[0].startswith("/") else "xl/" + targets[0]
        text = archive.read(part).decode("utf-8")
        tree = ET.fromstring(text)
        if tree.findall(".//{*}f") or tree.findall("{*}mergeCells/{*}mergeCell"):
            raise table.TableSourceError("业务模板的公式或合并结构不在克隆范围。")
        strings = ["".join(item.itertext()) for item in ET.fromstring(archive.read("xl/sharedStrings.xml"))] if "xl/sharedStrings.xml" in names else []
        cells = {cell.get("r"): _value(cell, strings) for cell in tree.findall("{*}sheetData/{*}row/{*}c")}
        if [cells.get("A" + str(row)) for row in range(1, 5)] != ["##comment", "##var", "##type", "##comment"]:
            raise table.TableSourceError("模板不是已支持的四行自描述表。")
        fields, index = [], 1
        while cells.get(table._column_name(index) + "2"):
            column = table._column_name(index)
            name, kind_value = cells[column + "2"], cells.get(column + "3")
            if not isinstance(name, str) or table._private(name) or kind_value not in table.TYPES:
                raise table.TableSourceError("模板包含不支持或敏感字段。")
            fields.append({"name": name, "type": kind_value, "column": column})
            index += 1
        if not fields or fields[0]["name"] != "Name" or len({item["name"] for item in fields}) != len(fields):
            raise table.TableSourceError("模板字段不唯一或缺少 Name 主键。")
        # 仅清理冻结字段内的旧业务值；其他非空业务区域必须先设计专用入口。
        allowed = {item["column"] for item in fields}
        if any(value is not None and int(re.search(r"[0-9]+$", address).group()) >= 5 and re.sub(r"[0-9]+$", "", address) not in allowed for address, value in cells.items()):
            raise table.TableSourceError("模板数据区含字段以外的内容，不能安全清空。")
        return {"raw": raw, "text": text, "part": part, "sheetName": sheet.get("name"),
                "sheetNames": [item.get("name") for item in sheets], "fields": fields}


def inspect_template(kind: str, mode: str = "rows") -> dict:
    """只读发现模板；copy 仅报整书身份，rows 另报完整字段，不回显示例业务数据。"""
    if _mode(mode) == "copy":
        template = _copy_template(kind)
        return {"status": "success", "mode": "copy", "template": kind,
                "templateHash": table._hash(template["raw"]), "sheetNames": template["sheetNames"]}
    template = _template(kind)
    return {"status": "success", "mode": "rows", "template": kind, "templateHash": table._hash(template["raw"]),
            "sheetName": template["sheetName"], "fields": template["fields"]}


def _relocate_cell(match, row: int, value, kind: str) -> str:
    """只改变地址与值，复制模板原物理格属性（包括样式）。"""
    text = match.group()
    address = re.sub(r"[0-9]+$", str(row), _address(match))
    text = re.sub(r'\br\s*=\s*([\'"])[A-Z]+[0-9]+\1', 'r="' + address + '"', text, count=1)
    return table._cell_xml(table.CELL_XML.fullmatch(text), value, kind)


def _render(template: dict, request: dict) -> bytes:
    """原 ZIP 克隆：仅重编码业务 Sheet 和必要的 Sheet 名，旧业务数据清空保留格式。"""
    text, fields = template["text"], template["fields"]
    rows = list(table.ROW_XML.finditer(text))
    row_map = {int(re.search(r'\br\s*=\s*([\'"])([0-9]+)\1', item.group("attrs")).group(2)): item for item in rows}
    if 5 not in row_map or len(row_map) != len(rows):
        raise table.TableSourceError("模板缺少唯一第5行数据样式。")
    model = row_map[5]
    model_cells = {_address(item): item for item in table.CELL_XML.finditer(model.group("body") or "")}
    if any(item["column"] + "5" not in model_cells for item in fields):
        raise table.TableSourceError("模板第5行缺少字段物理格，不能继承样式。")
    updates = {}
    for number, row in row_map.items():
        if number < 5:
            continue
        body = row.group("body") or ""
        # 保留所有空格、行属性及格式，仅去除模板示例业务值与类型。
        def clear(match):
            attrs = re.sub(r'\s+t\s*=\s*([\'"]).*?\1', "", match.group("attrs"))
            return "<" + match.group("name") + attrs + "/>"
        body = table.CELL_XML.sub(clear, body)
        updates[number] = (row.start(), row.end(), "<" + row.group("name") + row.group("attrs") + ">" + body + "</" + row.group("name") + ">")
    assigned = {}
    for offset, values in enumerate(request["rows"]):
        number = offset + 5
        body = model.group("body") or ""
        by_address = {item["column"] + "5": item for item in fields}
        def fill(match):
            field = by_address.get(_address(match))
            if field:
                return _relocate_cell(match, number, values[field["name"]], field["type"])
            return re.sub(r'\br\s*=\s*([\'"])([A-Z]+)[0-9]+\1', lambda item: 'r="' + item.group(2) + str(number) + '"', match.group(), count=1)
        body = table.CELL_XML.sub(fill, body)
        attrs = re.sub(r'\br\s*=\s*([\'"])[0-9]+\1', 'r="' + str(number) + '"', model.group("attrs"), count=1)
        assigned[number] = "<" + model.group("name") + attrs + ">" + body + "</" + model.group("name") + ">"
    replacements = [(start, end, assigned.pop(number, replacement)) for number, (start, end, replacement) in updates.items()]
    for start, end, replacement in sorted(replacements, reverse=True):
        text = text[:start] + replacement + text[end:]
    if assigned:
        closing = list(re.finditer(r'</(?:[\w.-]+:)?sheetData\s*>', text))
        if len(closing) != 1:
            raise table.TableSourceError("模板缺少唯一 Sheet 数据区。")
        text = text[:closing[0].start()] + "".join(assigned[row] for row in sorted(assigned)) + text[closing[0].start():]
    if "title" in request:
        text = table.CELL_XML.sub(lambda match: table._cell_xml(match, request["title"], "string") if _address(match) == "B1" else match.group(), text)
    dimension = list(re.finditer(r'<(?:[\w.-]+:)?dimension\b[^>]*/\s*>', text))
    if len(dimension) != 1:
        raise table.TableSourceError("模板缺少唯一 dimension。")
    current = dimension[0]
    reference = re.search(r'\bref\s*=\s*([\'"])([A-Z]+)[0-9]+:([A-Z]+)([0-9]+)\1', current.group())
    if not reference:
        raise table.TableSourceError("模板 dimension 不是支持的矩形范围。")
    last = max(int(reference.group(4)), len(request["rows"]) + 4)
    updated = re.sub(r'\bref\s*=\s*([\'"]).*?\1', 'ref="A1:' + reference.group(3) + str(last) + '"', current.group())
    text = text[:current.start()] + updated + text[current.end():]
    ET.fromstring(text)
    changes = {template["part"]: text.encode("utf-8")}
    if request["sheetName"] != template["sheetName"]:
        with zipfile.ZipFile(io.BytesIO(template["raw"])) as archive:
            workbook = archive.read("xl/workbook.xml").decode("utf-8")
        pattern = r'(<(?:[\w.-]+:)?sheet\b[^>]*\bname\s*=\s*)([\'"])' + re.escape(template["sheetName"]) + r'\2'
        workbook, count = re.subn(pattern, lambda item: item.group(1) + quoteattr(request["sheetName"]), workbook)
        if count != 1:
            raise table.TableSourceError("模板业务 Sheet 名无法唯一改写。")
        changes["xl/workbook.xml"] = workbook.encode("utf-8")
    return table._replace_raw_zip_members(template["raw"], changes)


def _build(project: Path, request: dict, *, check_target: bool = True) -> tuple[dict, bytes]:
    """冻结模式、写集及模板/目标摘要；copy 原字节复制，rows 不补猜或扩展 Schema。"""
    if not isinstance(request, dict):
        raise table.TableSourceError("模板克隆请求必须是对象。")
    mode = _mode(request.get("mode", "rows"))
    if mode == "copy":
        table._keys(request, COPY_REQUEST_KEYS, COPY_REQUEST_KEYS, "模板物料复制请求")
    else:
        table._keys(request, REQUEST_KEYS | {"mode"}, REQUEST_KEYS - {"title"}, "模板克隆请求")
    target = table._relative_path(project, request["target"], exists=False)
    if not request["target"].startswith("Assets/") or target.suffix != ".xlsx":
        raise table.TableSourceError("目标必须是 Assets 内的 .xlsx 表源。")
    table._boundaries(project, request["target"], request["sourceRoots"], request["generatedRoots"])
    before = request["targetHash"]
    if before is not None and (not isinstance(before, str) or not re.fullmatch(r"[a-f0-9]{64}", before)):
        raise table.TableSourceError("targetHash 必须是现有整文件 SHA256；新建明确传 null。")
    if check_target and (target.exists() != (before is not None) or target.exists() and (not target.is_file() or table._hash(target.read_bytes()) != before)):
        raise table.TableSourceError("目标存在性或摘要已漂移，未执行模板克隆。")
    if mode == "copy":
        template = _copy_template(request["template"])
        digest = table._hash(template["raw"])
        return {"projectRoot": str(project), "request": request, "templateHash": digest, "outputHash": digest}, template["raw"]
    template = _template(request["template"])
    name = request["sheetName"]
    if not isinstance(name, str) or not name or len(name) > 31 or re.search(r"[\\/*?:\[\]\x00-\x1f]", name) or name.startswith(("#", "'")) or name.endswith("'") or name.casefold() in {item.casefold() for item in template["sheetNames"] if item != template["sheetName"]}:
        raise table.TableSourceError("Sheet 名无效或与备注页冲突。")
    if "title" in request:
        table._type_value(request["title"], "string")
    rows, fields = request["rows"], template["fields"]
    if not isinstance(rows, list) or not 1 <= len(rows) or len(rows) * len(fields) > MAX_CELLS:
        raise table.TableSourceError("rows 必须非空且业务格总数不超过8192。")
    names = set()
    for row in rows:
        table._keys(row, {item["name"] for item in fields}, {item["name"] for item in fields}, "业务行")
        for field in fields:
            value = table._type_value(row[field["name"]], field["type"])
            if isinstance(value, str) and value.lstrip().startswith(("=", "+", "-", "@")):
                raise table.TableSourceError("业务文本不接受公式前缀。")
        if not row["Name"] or row["Name"] in names:
            raise table.TableSourceError("Name 必须非空且每行唯一。")
        names.add(row["Name"])
    result = _render(template, request)
    return {"projectRoot": str(project), "request": request, "templateHash": table._hash(template["raw"]), "outputHash": table._hash(result)}, result


def plan_clone(root: str | Path, request: dict) -> dict:
    """只读准备完整模板克隆计划；持久 Plan 由 CLI 写入既有 TableSource 状态根。"""
    payload, _ = _build(table._project_root(root), request)
    return {"version": VERSION, "payload": payload, "planId": table._hash(table._canonical(payload)), "executionStarted": False}


def _validated(project: Path, plan: dict) -> dict:
    table._keys(plan, {"version", "payload", "planId", "executionStarted"}, {"version", "payload", "planId", "executionStarted"}, "模板计划")
    table._keys(plan["payload"], {"projectRoot", "request", "templateHash", "outputHash"}, {"projectRoot", "request", "templateHash", "outputHash"}, "模板计划内容")
    if plan["version"] != VERSION or type(plan["executionStarted"]) is not bool or plan["payload"]["projectRoot"] != str(project) or plan["planId"] != table._hash(table._canonical(plan["payload"])):
        raise table.TableSourceError("计划版本、工程身份或摘要无效。")
    return plan["payload"]


def _summary(plan: dict) -> dict:
    """仅输出冻结身份、写入目标和行数，不在普通摘要回显业务值。"""
    payload = plan["payload"]
    request = payload["request"]
    mode = request.get("mode", "rows")
    summary = {"planId": plan["planId"], "mode": mode, "template": request["template"], "templateHash": payload["templateHash"],
               "target": request["target"], "targetHash": request["targetHash"], "outputHash": payload["outputHash"]}
    if mode == "rows":
        summary.update({"rowCount": len(request["rows"]), "sheetName": request["sheetName"]})
    return summary


def execute_plan(root: str | Path, plan_path: str, token: str, receipt_path: str) -> dict:
    """与既有表源编辑共用单写者锁；一次消费、写前重核，失败不自动重放或回滚。"""
    project = table._project_root(root)
    with table._write_lock(project):
        if plan_path == receipt_path:
            raise table.TableSourceError("Plan 与 Receipt 必须不同。")
        plan = table._read_state(project, plan_path)
        payload = _validated(project, plan)
        if plan["executionStarted"] or token != plan["planId"]:
            raise table.TableSourceError("计划已消费或确认 token 不匹配。")
        current, result = _build(project, payload["request"])
        if current != payload:
            raise table.TableSourceError("模板或输入已漂移，未执行计划。")
        receipt = {"version": VERSION, "plan": plan, "completed": False}
        table._atomic_write(project, receipt_path, table._canonical(receipt), state=True, replace=False)
        try:
            plan["executionStarted"] = True
            table._atomic_write(project, plan_path, table._canonical(plan), state=True)
            request = payload["request"]
            table._atomic_write(project, request["target"], result, state=False,
                                replace=request["targetHash"] is not None, expected_hash=request["targetHash"])
            receipt["completed"] = True
            table._atomic_write(project, receipt_path, table._canonical(receipt), state=True)
        except (OSError, table.TableSourceError):
            return {"status": "partial", **_summary(plan), "receipt": receipt_path, "reason": "写入已开始；先只读 verify，不重放 Execute。"}
        return {"status": "success", **_summary(plan), "receipt": receipt_path}


def verify_receipt(root: str | Path, receipt_path: str) -> dict:
    """只读重建预期字节并核对整表；样式、备注和非目标压缩记录随预期一并验证。"""
    project = table._project_root(root)
    receipt = table._read_state(project, receipt_path)
    table._keys(receipt, {"version", "plan", "completed"}, {"version", "plan", "completed"}, "模板收据")
    plan = receipt["plan"]
    payload = _validated(project, plan)
    issues = []
    try:
        current, _ = _build(project, payload["request"], check_target=False)
        target = table._relative_path(project, payload["request"]["target"])
        if current != payload or table._hash(target.read_bytes()) != payload["outputHash"]:
            issues.append("template_or_output_changed")
    except (table.TableSourceError, OSError):
        issues.append("template_or_output_unavailable")
    if receipt["version"] != VERSION or receipt["completed"] is not True or plan["executionStarted"] is not True:
        issues.append("completion_evidence_missing")
    return {"status": "partial" if issues else "success", **_summary(plan), "receipt": receipt_path, "issues": issues}


def main(argv: list[str] | None = None) -> int:
    """固定 inspect/plan/execute/verify CLI；业务值仅保存在本地计划，不回显普通结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", required=True)
    commands = parser.add_subparsers(dest="operation", required=True)
    inspect = commands.add_parser("inspect")
    inspect.add_argument("--template", required=True, choices=TEMPLATES)
    inspect.add_argument("--mode", choices=("rows", "copy"), default="rows")
    plan = commands.add_parser("plan")
    plan.add_argument("--request", required=True)
    plan.add_argument("--plan", required=True)
    execute = commands.add_parser("execute")
    execute.add_argument("--plan", required=True)
    execute.add_argument("--confirmation-token", required=True)
    execute.add_argument("--receipt", required=True)
    verify = commands.add_parser("verify")
    verify.add_argument("--receipt", required=True)
    args = parser.parse_args(argv)
    try:
        project = table._project_root(args.project_root)
        if args.operation == "inspect":
            result = inspect_template(args.template, args.mode)
        elif args.operation == "plan":
            if args.request == "-":
                request = json.loads(sys.stdin.read())
            else:
                request = table._read_state(project, args.request) if args.request.startswith(table.STATE) else json.loads(table._relative_path(project, args.request).read_text("utf-8"))
            planned = plan_clone(project, request)
            table._atomic_write(project, args.plan, table._canonical(planned), state=True, replace=False)
            result = {"status": "success", **_summary(planned), "plan": args.plan}
        elif args.operation == "execute":
            result = execute_plan(project, args.plan, args.confirmation_token, args.receipt)
        else:
            result = verify_receipt(project, args.receipt)
    except table.TableSourceError as exc:
        result = {"status": "blocked", "reason": str(exc)}
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile, ET.ParseError):
        result = {"status": "blocked", "reason": "文件、JSON 或模板结构无效；未回显业务值。"}
    print(json.dumps(result, ensure_ascii=False, allow_nan=False))
    return 0 if result["status"] == "success" else 3 if result["status"] == "partial" else 2


if __name__ == "__main__":
    raise SystemExit(main())
