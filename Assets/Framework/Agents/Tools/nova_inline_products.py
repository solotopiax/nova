#!/usr/bin/env python3
"""只读校验已有商品工作簿并生成 Config 面板列表值；保存复用已有 Action。"""

from __future__ import annotations

import argparse
from decimal import Decimal, InvalidOperation, localcontext
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import sys
import xml.etree.ElementTree as ET
import zipfile


IAP_HEADERS = ("TableId", "Name", "ProductID", "ThirdProductID", "ProductType", "SubGroupID", "Price", "Currency", "EditorNote")
IAP_TYPES = {"Consumable", "NonConsumable", "Subscription"}
CONTRACTS = {
    "iap": ("NovaFramework.SDK.IAP.Runtime.IAPPluginConfig", "m_Products.m_Items"),
    "wechat": ("NovaFramework.SDK.WeChatMiniGame.Runtime.WeChatMiniGamePluginConfig", "m_PurchaseList.m_Items"),
}
DENIED_PARTS = {".git", "library", "temp", "packages", "upmpackages", "vendor", "vendors", "thirdparty", "generated", "datatypes", "jsons", "node_modules", "obj", "build", "builds"}


class InlineProductsError(RuntimeError):
    """表示源不稳定、格式不明确或商品无效；错误不回显单元格数据。"""


def source_path(project_root: str | Path, source: str) -> Path:
    """冻结项目内已有 xlsx 源，拒绝包、生成物、符号链接和父目录跳转。"""
    root = Path(project_root).absolute()
    if not root.is_dir() or any(p.is_symlink() for p in (root, *root.parents)):
        raise InlineProductsError("项目根不存在或包含符号链接。")
    if not isinstance(source, str) or not source or "\\" in source or ":" in source:
        raise InlineProductsError("source 必须为规范项目相对路径。")
    parts = PurePosixPath(source).parts
    if PurePosixPath(source).is_absolute() or ".." in parts or source != "/".join(parts):
        raise InlineProductsError("source 不能跳出项目或使用非规范路径。")
    if any(p.casefold() in DENIED_PARTS for p in parts):
        raise InlineProductsError("包、厂商、生成物或临时目录不能作为商品源。")
    path = root
    for part in parts:
        path /= part
        if path.is_symlink():
            raise InlineProductsError("商品源路径包含符号链接。")
    if not path.is_file() or path.suffix.lower() != ".xlsx":
        raise InlineProductsError("仅支持已存在的无宏 .xlsx 商品源。")
    return path


def xml_part(archive: zipfile.ZipFile, name: str) -> ET.Element:
    """读取有大小上限的 XML 部件，不处理 DTD 或实体声明。"""
    raw = archive.read(name)
    if b"<!DOCTYPE" in raw.upper() or b"<!ENTITY" in raw.upper():
        raise InlineProductsError("工作簿 XML 含不支持的声明。")
    return ET.fromstring(raw)


def workbook_rows(raw: bytes, sheet_name: str) -> tuple[dict[int, dict[int, str]], set[tuple[int, int]]]:
    """只读显式指定页，保留数字文本精度，记录公式位置而不使用缓存结果。"""
    try:
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            names = archive.namelist()
            if len(names) != len(set(names)) or any("vba" in n.casefold() or n.casefold().endswith(".bin") or n.startswith("xl/externalLinks/") for n in names):
                raise InlineProductsError("工作簿含宏、外链或重复 ZIP 部件。")
            if any(i.file_size > 16 * 1024 * 1024 for i in archive.infolist()) or sum(i.file_size for i in archive.infolist()) > 64 * 1024 * 1024:
                raise InlineProductsError("工作簿超过只读转换大小上限。")
            book = xml_part(archive, "xl/workbook.xml")
            rels = xml_part(archive, "xl/_rels/workbook.xml.rels")
            matching = [s for s in book.findall("{*}sheets/{*}sheet") if s.attrib.get("name") == sheet_name]
            if len(matching) != 1:
                raise InlineProductsError("指定工作表不存在或不唯一。")
            identity = next(v for k, v in matching[0].attrib.items() if k.endswith("}id"))
            target = next(r.attrib["Target"] for r in rels if r.attrib.get("Id") == identity and r.attrib.get("TargetMode") != "External")
            part = target.lstrip("/") if target.startswith("/") else "xl/" + target
            if ".." in PurePosixPath(part).parts or not part.startswith("xl/worksheets/"):
                raise InlineProductsError("工作表关系不是有效内置页。")
            strings = []
            if "xl/sharedStrings.xml" in names:
                strings = ["".join(n.itertext()) if not n.findall(".//{*}t") else "".join(t.text or "" for t in n.findall(".//{*}t")) for n in xml_part(archive, "xl/sharedStrings.xml").findall("{*}si")]
            rows, formulas = {}, set()
            cells = xml_part(archive, part).findall("{*}sheetData/{*}row/{*}c")
            if len(cells) > 100000:
                raise InlineProductsError("工作表超过只读转换单元格上限。")
            for cell in cells:
                match = re.fullmatch(r"([A-Z]{1,3})([1-9][0-9]*)", cell.attrib.get("r", ""))
                if not match:
                    raise InlineProductsError("工作表单元格坐标无效。")
                column = 0
                for char in match[1]:
                    column = column * 26 + ord(char) - 64
                row = int(match[2])
                if column > 16384 or row > 16384 or column in rows.get(row, {}):
                    raise InlineProductsError("工作表坐标重复或超过转换上限。")
                if cell.find("{*}f") is not None:
                    formulas.add((row, column))
                kind = cell.attrib.get("t", "n")
                value = cell.findtext("{*}v", "")
                if kind == "s":
                    index = int(value)
                    if not 0 <= index < len(strings):
                        raise InlineProductsError("工作表共享字符串索引无效。")
                    value = strings[index]
                elif kind == "inlineStr":
                    value = "".join(n.text or "" for n in cell.findall("{*}is//{*}t"))
                elif kind not in ("n", "str"):
                    # 不让布尔或错误格被隐式解释为商品数字。
                    value = "\x00unsupported-cell"
                rows.setdefault(row, {})[column] = value
            return rows, formulas
    except InlineProductsError:
        raise
    except Exception as exc:
        raise InlineProductsError("商品工作簿或工作表结构无法解析。") from exc


def cell_text(rows: dict, formulas: set, row: int, column: int) -> str:
    """取得已选商品字段的规范文本，拒绝公式、控制字符及明显凭据材料。"""
    if (row, column) in formulas:
        raise InlineProductsError("商品字段包含公式，不能使用缓存值导入。")
    value = rows.get(row, {}).get(column, "").strip()
    if len(value) > 4096 or any(ord(c) < 32 and c not in "\t\n\r" for c in value):
        raise InlineProductsError("商品字段文本类型、长度或控制字符无效。")
    if re.search(r"-----BEGIN .*PRIVATE KEY|^Bearer\s|^eyJ[\w-]+\.[\w-]+\.", value):
        raise InlineProductsError("凭据材料不能进入商品转换结果。")
    return value


def integer(value: str, minimum: int, maximum: int, label: str) -> int:
    """按真实字段范围校验整数，不使用浮点中转或截断。"""
    if not re.fullmatch(r"[+-]?[0-9]+", value) or not minimum <= int(value) <= maximum:
        raise InlineProductsError(label + " 类型或范围无效。")
    return int(value)


def price_decimal(value: str) -> Decimal:
    """校验显式十进制价格，拒绝千分位、指数、非有限值及负值。"""
    try:
        if not re.fullmatch(r"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)", value):
            raise InvalidOperation
        result = Decimal(value)
        if result < 0 or result > Decimal("79228162514264337593543950335"):
            raise InvalidOperation
        return result
    except InvalidOperation as exc:
        raise InlineProductsError("商品价格不是范围内的非负十进制文本。") from exc


def iap_entries(rows: dict, formulas: set) -> tuple[list[dict], list[int]]:
    """按 IAP 固定九列或 ##var 模板生成完整条目，保持价格与商品 ID 为字符串。"""
    candidates = []
    for row in sorted(rows):
        offset = 1 if rows[row].get(1, "").strip() == "##var" else 0
        if row != 1 and not offset:
            continue
        if all(cell_text(rows, formulas, row, i + 1 + offset) == h for i, h in enumerate(IAP_HEADERS)):
            candidates.append((row, offset))
    if len(candidates) != 1:
        raise InlineProductsError("IAP 固定九列表头不存在或不唯一。")
    header, offset = candidates[0]
    entries, source_rows, ids = [], [], set()
    for row in sorted(rows):
        if row <= header or not any(v.strip() for v in rows[row].values()) or rows[row].get(1, "").strip().startswith("##"):
            continue
        values = [cell_text(rows, formulas, row, i + 1 + offset) for i in range(9)]
        table_id = integer(values[0], 1, 4294967295, "IAP TableId")
        if table_id in ids or values[4] not in IAP_TYPES:
            raise InlineProductsError("IAP 商品 ID 重复或 ProductType 不是精确枚举名。")
        if values[6]:
            price_decimal(values[6])
        ids.add(table_id)
        entries.append(dict(zip(("m_TableId", "m_Name", "m_ProductID", "m_ThirdProductID", "m_ProductType", "m_SubGroupID", "m_Price", "m_Currency", "m_EditorNote"), (table_id, *values[1:5], integer(values[5], -2147483648, 2147483647, "SubGroupID") if values[5] else 0, *values[6:]))))
        source_rows.append(row)
    return entries, source_rows


def wechat_entries(rows: dict, formulas: set, coins_per_yuan: int | None) -> tuple[list[dict], list[int]]:
    """按微信模板或 Solar 显式 Kind 列读取，校验分单位与游戏币兑换比例。"""
    candidates = [r for r in sorted(rows) if any(v.strip() in ("TableId", "ID") for v in rows[r].values())]
    if len(candidates) != 1:
        raise InlineProductsError("微信购买表表头不存在或不唯一。")
    header = candidates[0]
    columns = {}
    for column in sorted(rows[header]):
        value = cell_text(rows, formulas, header, column)
        if not value or value == "#":
            continue
        if value.casefold() in columns:
            raise InlineProductsError("微信购买表字段名重复。")
        columns[value.casefold()] = column
    if "kind" not in columns or not any(k in columns for k in ("quantity", "count")) or not any(k in columns for k in ("pricefen", "price")):
        raise InlineProductsError("微信购买表缺少 Kind、数量或价格字段。")

    def value(row: int, *names: str) -> str:
        """按公开导入器的别名顺序读取已知商品字段，不公开其它列。"""
        return next((cell_text(rows, formulas, row, columns[n.casefold()]) for n in names if n.casefold() in columns), "")

    entries, source_rows, ids = [], [], set()
    for row in sorted(rows):
        if row <= header or not any(v.strip() for v in rows[row].values()) or rows[row].get(1, "").strip().startswith("#"):
            continue
        table_id = integer(value(row, "TableId", "ID"), 1, 2147483647, "微信 TableId")
        kinds = {"gamecurrency": "GameCurrency", "gameitem": "GameItem", "1": "GameCurrency", "2": "GameItem"}
        kind = kinds.get(value(row, "Kind").casefold())
        if table_id in ids or kind is None:
            raise InlineProductsError("微信购买 ID 重复或 Kind 无效。")
        quantity = integer(value(row, "Quantity", "Count"), 1, 2147483647, "微信数量")
        if "pricefen" in columns:
            price = integer(value(row, "PriceFen"), 1, 2147483647, "PriceFen")
        else:
            if value(row, "Currency") not in ("", "RMB", "CNY"):
                raise InlineProductsError("微信元价格必须使用人民币。")
            yuan = price_decimal(value(row, "Price"))
            # Decimal 默认上下文会截掉超长尾数；保留全部有效位后再判断是否整数分。
            with localcontext() as context:
                context.prec = max(32, len(yuan.as_tuple().digits) + 3)
                cents = yuan * 100
            if cents != cents.to_integral_value() or not 0 < cents <= 2147483647:
                raise InlineProductsError("微信元价格必须精确到正整数分。")
            price = int(cents)
        product_id = value(row, "ProductId", "ProductID")
        if kind == "GameItem" and not product_id:
            raise InlineProductsError("微信道具商品 ID 未配置。")
        if kind == "GameCurrency" and (type(coins_per_yuan) is not int or not 0 < coins_per_yuan <= 2147483647 or price * coins_per_yuan != quantity * 100):
            raise InlineProductsError("游戏币兑换比例未冻结或与价格、数量不一致。")
        ids.add(table_id)
        entries.append({"m_TableId": table_id, "m_Name": value(row, "Name"), "m_Kind": kind, "m_ProductId": product_id, "m_PriceFen": price, "m_Quantity": quantity, "m_EditorNote": value(row, "EditorNote", "Desc")})
        source_rows.append(row)
    return entries, source_rows


def prepare(project_root: str | Path, source: str, source_hash: str, sheet: str, family: str, *, coins_per_yuan: int | None = None) -> dict:
    """核对整源摘要后输出商品列表值；不写工作簿、Config 或任意生成物。"""
    if family not in CONTRACTS or not isinstance(sheet, str) or not sheet or not isinstance(source_hash, str) or not re.fullmatch(r"[0-9a-f]{64}", source_hash):
        raise InlineProductsError("family、sheet 或冻结的 sourceHash 无效。")
    if family == "iap" and coins_per_yuan is not None:
        raise InlineProductsError("IAP 转换不接受微信兑换比例。")
    path = source_path(project_root, source)
    if path.stat().st_size > 32 * 1024 * 1024:
        raise InlineProductsError("商品源超过大小上限。")
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != source_hash:
        raise InlineProductsError("商品源已漂移，请重新读取并冻结。")
    rows, formulas = workbook_rows(raw, sheet)
    entries, source_rows = iap_entries(rows, formulas) if family == "iap" else wechat_entries(rows, formulas, coins_per_yuan)
    if not 0 < len(entries) <= 1024:
        raise InlineProductsError("商品完整替换要求 1 至 1024 项；清空列表不是批量导入。")
    value_json = json.dumps(entries, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    if len(value_json) > 32768 or len(value_json.encode("utf-8")) > 16 * 1024:
        raise InlineProductsError("商品列表超过现有 Config 字段 JSON 上限，不能拆批造成覆盖丢失。")
    if source_path(project_root, source) != path or hashlib.sha256(path.read_bytes()).hexdigest() != source_hash:
        raise InlineProductsError("转换期间商品源发生变化。")
    config_type, property_path = CONTRACTS[family]
    return {"status": "success", "stage": "prepared-only", "source": source, "sourceHash": source_hash, "sheet": sheet, "family": family, "rowCount": len(entries), "sourceRows": source_rows, "configType": config_type, "field": {"path": property_path, "valueJson": value_json}, "writerAction": "nova.project.config.patch-panels", "requiresLiveInspectAndPlan": True}


def main(argv: list[str] | None = None) -> int:
    """执行只读商品转换；输入错误返回结构化 blocked，不输出解析器原始数据。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--source-hash", required=True)
    parser.add_argument("--sheet", required=True)
    parser.add_argument("--family", choices=tuple(CONTRACTS), required=True)
    parser.add_argument("--coins-per-yuan", type=int)
    args = parser.parse_args(argv)
    try:
        result = prepare(args.project_root, args.source, args.source_hash, args.sheet, args.family, coins_per_yuan=args.coins_per_yuan)
    except InlineProductsError as exc:
        result = {"status": "blocked", "reason": str(exc)}
    except (OSError, ValueError, TypeError):
        result = {"status": "blocked", "reason": "商品源读取或输入类型无效，请重新核对已有源。"}
    print(json.dumps(result, ensure_ascii=False, allow_nan=False))
    return 0 if result["status"] == "success" else 1


if __name__ == "__main__":
    sys.exit(main())
