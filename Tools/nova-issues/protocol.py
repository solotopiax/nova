"""Validation for the ingestion and maintenance APIs."""

import re
import time


class InvalidInput(ValueError):
    pass


STATUSES = ("open", "triaged", "working", "awaiting_verification", "closed", "duplicate")
PRIORITIES = ("P0", "P1", "P2", "P3")
KEY = re.compile(r"[a-zA-Z0-9][a-zA-Z0-9_.-]{0,95}\Z")
UUID = re.compile(r"[a-f0-9]{32}\Z")
APPLICATION_IDENTIFIER = re.compile(r"[a-zA-Z0-9_-]+(?:\.[a-zA-Z0-9_-]+)+\Z")
PRIVATE_TEXT = re.compile(
    r"(?i)(\b(?:https?|ssh|file)://|\b[A-Z]:[\\/]|/(?:Users|home|private|tmp)/|"
    r"-----BEGIN [A-Z ]*PRIVATE KEY|\bbearer\s+\S+|"
    r"\b(?:api[_-]?key|access[_-]?token|password|secret)\s*[:=]\s*\S+)"
)


def obj(value, required, optional=()):
    if not isinstance(value, dict) or not set(required) <= value.keys() <= set(required) | set(optional):
        raise InvalidInput("请求字段不符合接口约定。")
    return value


def label(value, limit, *, empty=False):
    if not isinstance(value, str) or len(value) > limit or (not empty and not value.strip()):
        raise InvalidInput("文本缺失或超出长度限制。")
    if PRIVATE_TEXT.search(value) or any(ord(char) < 32 and char not in "\n\t" for char in value):
        raise InvalidInput("请移除本机路径、地址或凭据。")
    return value.strip()


def key(value, pattern=KEY):
    if not isinstance(value, str) or not pattern.fullmatch(value):
        raise InvalidInput("标识格式无效。")
    return value


def int_range(value, lower, upper):
    if type(value) is not int or not lower <= value <= upper:
        raise InvalidInput("数字不在允许的范围内。")
    return value


def enum(value, choices):
    if value not in choices:
        raise InvalidInput("字段值不受支持。")
    return value


def records(value, limit):
    if not isinstance(value, list) or len(value) > limit:
        raise InvalidInput("列表格式或长度无效。")
    return value


def validate_snapshot(data):
    obj(data, ("framework", "unity", "packages", "tools", "coverage"),
        ("platform", "channel", "mode", "enabled", "coordinate_source", "application_identifiers", "codex_hooks", "git_user_name"))
    for name in ("framework", "unity", "platform", "channel", "mode", "coordinate_source"):
        if name in data:
            label(data[name], 80, empty=name not in ("framework", "unity"))
    for package in records(data["packages"], 350):
        obj(package, ("id", "version", "source", "evidence"))
        key(package["id"])
        label(package["version"], 80)
        enum(package["source"], ("registry", "local", "embedded", "git", "builtin", "unknown"))
        enum(package["evidence"], ("registered", "resolved", "declared"))
    for entry in records(data.get("enabled", []), 100):
        key(entry)
    if "codex_hooks" in data:
        enum(data["codex_hooks"], ("complete", "pending", "incomplete"))
    if "git_user_name" in data:
        label(data["git_user_name"], 100)
        if re.search(r"[\x00-\x1f\x7f-\x9f]", data["git_user_name"]):
            raise InvalidInput("Git 名称必须是单行文本。")
    if "application_identifiers" in data:
        identifiers = data["application_identifiers"]
        if not isinstance(identifiers, dict) or len(identifiers) > 3:
            raise InvalidInput("应用包名列表无效。")
        for platform, identifier in identifiers.items():
            key(platform)
            if not isinstance(identifier, str) or len(identifier) > 255 or not APPLICATION_IDENTIFIER.fullmatch(identifier):
                raise InvalidInput("应用包名格式无效。")
    if not isinstance(data["tools"], dict) or not data["tools"].keys() <= {"codex", "unity-cli", "mcp", "pipeline"}:
        raise InvalidInput("工具清单无效。")
    for item in data["tools"].values():
        obj(item, ("state",), ("version",))
        key(item["state"])
        if "version" in item:
            label(item["version"], 80, empty=True)
    if not isinstance(data["coverage"], dict) or not data["coverage"].keys() <= {"codex", "editor", "agent"}:
        raise InvalidInput("采集覆盖范围无效。")
    for state in data["coverage"].values():
        key(state)


def validate_usage(data):
    """接收历史使用记录以及带来源的 Codex 和 Editor 计数。"""
    obj(data, ("module", "operation", "outcome", "framework"), ("milliseconds", "source", "entry"))
    key(data["module"])
    key(data["operation"])
    enum(data["outcome"], ("executed", "success", "partial", "blocked", "failed", "cancelled"))
    label(data["framework"], 80)
    if "milliseconds" in data:
        int_range(data["milliseconds"], 0, 86400000)
    if "source" in data:
        enum(data["source"], ("codex", "editor"))
    if "entry" in data:
        enum(data["entry"], ("action", "skill", "unity-cli", "nova-cli"))
    if data.get("source") == "codex" and "entry" not in data:
        raise InvalidInput("Codex 使用记录缺少调用入口。")


def validate_task(data):
    """仅接收 Codex 单轮活动的基础版本字段。"""
    obj(data, ("framework", "unity"))
    label(data["framework"], 80)
    label(data["unity"], 80)


def validate_hook_activity(data):
    """钩子运行记录复用信封的工作区和发生时间，数据只携带框架版本。"""
    obj(data, ("framework",))
    label(data["framework"], 80)


def validate_issue(data):
    obj(data, ("code", "area", "title", "summary", "expected", "actual", "priority",
               "framework", "evidence", "source"), ("workaround", "version", "steps"))
    for name in ("code", "area", "source"):
        key(data[name])
    label(data["title"], 150)
    for name in ("summary", "expected", "actual", "workaround", "steps"):
        if name in data:
            label(data[name], 1400, empty=name in ("workaround", "steps"))
    enum(data["priority"], PRIORITIES)
    label(data["framework"], 80)
    if "version" in data:
        label(data["version"], 80, empty=True)
    if not records(data["evidence"], 10):
        raise InvalidInput("上报 Issue 需要归因证据。")
    for entry in data["evidence"]:
        obj(entry, ("source", "detail"))
        key(entry["source"])
        label(entry["detail"], 600)


def validate_event(value):
    obj(value, ("version", "id", "workspace", "observed_at", "kind", "data"))
    int_range(value["version"], 1, 1)
    key(value["id"], UUID)
    key(value["workspace"], UUID)
    int_range(value["observed_at"], 1, int(time.time()) + 86400)
    validators = {"snapshot": validate_snapshot, "usage": validate_usage, "issue": validate_issue,
                  "task": validate_task, "hook_activity": validate_hook_activity}
    kind = enum(value["kind"], validators)
    validators[kind](value["data"])
    return value
