#!/usr/bin/env python3
"""只读检查并在本机提供已冻结的 Unity WebGL 导出，不构建或部署。"""

from __future__ import annotations

import argparse
import hashlib
import http.server
import json
import mimetypes
from pathlib import Path
import sys
import urllib.parse


def file_hash(path: Path) -> str:
    """分块计算文件摘要，不把文件正文放入检查结果。"""
    with path.open("rb") as stream:
        return stream_hash(stream)


def stream_hash(stream) -> str:
    """从当前文件流计算摘要；调用方负责恢复读取位置与关闭文件。"""
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


def inspect_build(build_root: str) -> dict:
    """核验唯一导出根、index.html 和 Build 下的 Wasm，冻结全部可服务文件；拒绝符号链接。"""
    candidate = Path(build_root).expanduser().absolute()
    if candidate.is_symlink():
        raise ValueError("导出根不能是符号链接。")
    root = candidate.resolve(strict=True)
    if not root.is_dir():
        raise ValueError("导出根必须是目录。")
    files = {}
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            raise ValueError("导出目录内不能包含符号链接。")
        if path.is_file():
            files[path.relative_to(root).as_posix()] = file_hash(path)
        elif not path.is_dir():
            raise ValueError("导出目录内只允许普通文件与目录。")
    wasm = [name for name in files if name.startswith("Build/") and
            "/" not in name[len("Build/"):] and name.endswith((".wasm", ".wasm.gz", ".wasm.br"))]
    if "index.html" not in files or not wasm:
        raise ValueError("导出根必须含 index.html 和 Build/*.wasm（或 .wasm.gz/.wasm.br）。")
    state = {"buildRoot": str(root), "files": files}
    content_hash = hashlib.sha256(json.dumps(state, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return {"buildRoot": str(root), "contentHash": content_hash, "fileCount": len(files), "wasm": wasm, "files": files}


def request_file(root: Path, request_path: str) -> Path:
    """解析一次 URL 路径并限定导出根，拒绝路径穿越、反斜杠、符号链接与目录列表。"""
    path = urllib.parse.unquote(urllib.parse.urlsplit(request_path).path, errors="strict")
    if not path.startswith("/") or path.startswith("//") or "\\" in path or "\x00" in path:
        raise ValueError("请求路径不受支持。")
    parts = path[1:].split("/")
    if any(part in (".", "..") for part in parts):
        raise ValueError("请求路径不受支持。")
    target = root.joinpath(*parts)
    for part in [target, *target.parents]:
        if part == root:
            break
        if part.is_symlink():
            raise ValueError("请求路径不受支持。")
    target.resolve(strict=True).relative_to(root)
    if target.is_dir():
        target = target / "index.html"
        if target.is_symlink():
            raise ValueError("请求路径不受支持。")
    if not target.is_file():
        raise FileNotFoundError()
    return target


def response_type(path: Path) -> tuple[str, str | None]:
    """沿用 Unity WebGL 压缩后缀的响应头规则；不解压、不修改构建文件。"""
    name = path.name.lower()
    encoding = "br" if name.endswith(".br") else "gzip" if name.endswith(".gz") else None
    if encoding:
        name = name.rsplit(".", 1)[0]
    content_type = {".wasm": "application/wasm", ".js": "application/javascript",
                    ".data": "application/octet-stream", ".json": "application/json"}.get(Path(name).suffix)
    return content_type or mimetypes.guess_type(name)[0] or "application/octet-stream", encoding


class WebGLHandler(http.server.SimpleHTTPRequestHandler):
    """只服务冻结清单中的普通文件，不提供目录列表、写入或端口接管。"""

    def log_message(self, format, *args):
        """不打印 URL、查询参数或客户端字段；命令只报告服务身份和退出状态。"""
        return

    def end_headers(self):
        """禁用本地验收缓存；仅在明确启用线程隔离时添加 COOP/COEP。"""
        self.send_header("Cache-Control", "no-store")
        self.send_header("Vary", "Accept-Encoding")
        if self.server.cross_origin_isolation:
            self.send_header("Cross-Origin-Opener-Policy", "same-origin")
            self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()

    def send_head(self):
        """打开请求文件后核对冻结摘要；漂移或越界时拒绝，不发送文件正文。"""
        stream = None
        try:
            path = request_file(self.server.build_root, self.path)
            expected = self.server.files.get(path.relative_to(self.server.build_root).as_posix())
            if expected is None:
                raise ValueError("文件不在冻结清单。")
            stream = path.open("rb")
            if stream_hash(stream) != expected:
                raise ValueError("导出文件已经变化，请重新 inspect。")
            length = stream.tell()
            stream.seek(0)
            content_type, encoding = response_type(path)
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(length))
            if encoding:
                self.send_header("Content-Encoding", encoding)
            self.end_headers()
            return stream
        except (OSError, ValueError):
            if stream:
                stream.close()
            self.send_error(403, "Request outside frozen build or build changed")
            return None


def make_server(snapshot: dict, port: int, cross_origin_isolation: bool = False):
    """仅绑定 127.0.0.1；占用端口直接失败，不停止已有服务。"""
    server = http.server.ThreadingHTTPServer(("127.0.0.1", port), WebGLHandler)
    server.build_root = Path(snapshot["buildRoot"])
    server.files = snapshot["files"]
    server.cross_origin_isolation = cross_origin_isolation
    return server


def main(argv: list[str] | None = None) -> int:
    """固定 inspect/serve CLI；serve 必须绑定 inspect 摘要，结束后关闭本轮服务，无临时文件。"""
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="operation", required=True)
    inspect_parser = sub.add_parser("inspect", help="只读核验导出并返回冻结摘要")
    inspect_parser.add_argument("--build-root", required=True)
    serve_parser = sub.add_parser("serve", help="在本机提供冻结导出，Ctrl+C 结束")
    serve_parser.add_argument("--build-root", required=True)
    serve_parser.add_argument("--expected-sha256", required=True)
    serve_parser.add_argument("--port", type=int, default=8088)
    serve_parser.add_argument("--cross-origin-isolation", action="store_true")
    args = parser.parse_args(argv)
    try:
        snapshot = inspect_build(args.build_root)
        if args.operation == "inspect":
            print(json.dumps(snapshot, ensure_ascii=False))
            return 0
        if not 1 <= args.port <= 65535 or args.expected_sha256 != snapshot["contentHash"]:
            raise ValueError("端口无效或导出已漂移，请重新 inspect 并冻结。")
        with make_server(snapshot, args.port, args.cross_origin_isolation) as server:
            print(json.dumps({"url": f"http://127.0.0.1:{args.port}/", "buildRoot": snapshot["buildRoot"],
                              "contentHash": snapshot["contentHash"], "crossOriginIsolation": args.cross_origin_isolation}), flush=True)
            try:
                server.serve_forever()
            except KeyboardInterrupt:
                pass
        return 0
    except (OSError, ValueError):
        print("WebGL 目录/冻结摘要无效或端口不可用；未修改导出、关闭其他服务或执行部署。", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
