#!/usr/bin/env python3
"""保存白名单声明快照，并以项目内 SQLite 缓存提供不执行源码的按需查询。"""
from __future__ import annotations

from contextlib import contextmanager
import fnmatch
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sqlite3
import tempfile
from functools import lru_cache


SCHEMA_VERSION = 1
CACHE_VERSION = 1
_KINDS = {'function': 'functions', 'field': 'fields', 'file': 'files', 'input': 'inputs'}
_COUNTS = {'function': 'functions', 'field': 'fields', 'file': 'sourceFiles', 'input': 'capabilityInputs'}
_COMMON = {'source', 'sourceHash', 'family', 'kind', 'docRefs'}
_FUNCTION = _COMMON | {'id', 'name', 'signature', 'namespace', 'type', 'visibility', 'line',
    'endLine', 'startChar', 'endChar', 'sourceFileHash', 'parameters', 'returns', 'attributes', 'hasBody'}
_FIELD = _COMMON | {'sourceFileHash', 'line', 'owner', 'name', 'type', 'attributes', 'serialized'}
_ALLOWED = {'function': _FUNCTION, 'field': _FIELD, 'file': _COMMON, 'input': _COMMON}
_DDL = (
    'CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)',
    'CREATE TABLE records (position INTEGER PRIMARY KEY, kind TEXT NOT NULL, row_key TEXT NOT NULL, '
    'source TEXT NOT NULL, family TEXT NOT NULL, search TEXT NOT NULL, data TEXT NOT NULL)',
    'CREATE UNIQUE INDEX records_identity ON records(kind, row_key)',
    'CREATE INDEX records_family ON records(kind, family, position)',
    'CREATE INDEX records_source ON records(source, kind)',
)


class IndexFormatError(ValueError):
    """表示快照结构或缓存元数据不符合封闭白名单契约。"""


def _json(value) -> str:
    """使用固定顺序编码，避免时间戳、平台或字典插入顺序影响快照。"""
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False)


def _path(value: str) -> str:
    """仅接受可移植的仓内相对路径，拒绝越界、URI、Windows 驱动器及路径歧义。"""
    if (not isinstance(value, str) or not value or '\\' in value or ':' in value
            or any(ord(c) < 32 for c in value) or '#' in value or '?' in value
            or value.startswith('/') or any(p in {'', '.', '..'} for p in value.split('/'))
            or PurePosixPath(value).is_absolute()):
        raise IndexFormatError('索引路径必须是无越界的相对路径：' + repr(value))
    return value


def _text(value, label: str, empty: bool = True) -> None:
    """校验声明字符串；正文可包含换行，但不允许 NUL 字符进入缓存。"""
    if not isinstance(value, str) or '\0' in value or (not empty and not value):
        raise IndexFormatError(label + ' 必须是字符串')


def _hash(value, label: str) -> None:
    """只接收完整小写 SHA256，不接受截断指纹或占位字符串。"""
    if not isinstance(value, str) or re.fullmatch(r'[0-9a-f]{64}', value) is None:
        raise IndexFormatError(label + ' 必须是完整 SHA256')


def _integer(value, label: str, minimum: int = 0) -> None:
    """排除布尔值并检查位置与计数字段的整数下限。"""
    if type(value) is not int or value < minimum:
        raise IndexFormatError(label + ' 必须是大于等于 ' + str(minimum) + ' 的整数')


def _doc_refs(refs) -> list[dict]:
    """规范化文档路径与可选锚点，任何额外载荷均拒绝。"""
    if not isinstance(refs, list):
        raise IndexFormatError('docRefs 必须是列表')
    result = []
    for ref in refs:
        if not isinstance(ref, dict) or set(ref) - {'path', 'anchor'} or 'path' not in ref:
            raise IndexFormatError('docRefs 仅允许 path 和 anchor')
        item = {'path': _path(ref['path'])}
        if 'anchor' in ref:
            _text(ref['anchor'], 'anchor')
            item['anchor'] = ref['anchor']
        result.append(item)
    return sorted({_json(item): item for item in result}.values(), key=_json)


def _validate(kind: str, data: dict) -> None:
    """逐层验证白名单声明；禁止将正文、初始化值或审查记录藏入扩展字段。"""
    if kind not in _ALLOWED or not isinstance(data, dict):
        raise IndexFormatError('未知索引记录类别')
    required = _ALLOWED[kind] - {'startChar', 'endChar'}
    if set(data) - _ALLOWED[kind] or required - set(data):
        raise IndexFormatError(kind + ' 字段不符合白名单：' + ','.join(sorted(set(data) ^ required)))
    _path(data['source'])
    _hash(data['sourceHash'], 'sourceHash')
    for name in ('family', 'kind'):
        _text(data[name], name, empty=False)
    _doc_refs(data['docRefs'])
    if kind in {'function', 'field'}:
        _hash(data['sourceFileHash'], 'sourceFileHash')
        _integer(data['line'], 'line', 1)
        for name in ('name', 'type'):
            _text(data[name], name)
        if not isinstance(data['attributes'], list) or not all(isinstance(x, str) and '\0' not in x for x in data['attributes']):
            raise IndexFormatError('attributes 必须是字符串列表')
    if kind == 'field':
        _text(data['owner'], 'owner')
        if type(data['serialized']) is not bool:
            raise IndexFormatError('serialized 必须是布尔值')
    if kind == 'function':
        for name in ('id', 'signature', 'namespace', 'visibility', 'returns'):
            _text(data[name], name)
        if not data['id'].startswith(data['source'] + '#'):
            raise IndexFormatError('函数身份必须属于声明 source')
        _integer(data['endLine'], 'endLine', data['line'])
        if ('startChar' in data) != ('endChar' in data):
            raise IndexFormatError('startChar/endChar 必须成对存在')
        if 'startChar' in data:
            _integer(data['startChar'], 'startChar')
            _integer(data['endChar'], 'endChar', data['startChar'])
        if type(data['hasBody']) is not bool or not isinstance(data['parameters'], list):
            raise IndexFormatError('hasBody 或 parameters 类型错误')
        for parameter in data['parameters']:
            if not isinstance(parameter, dict) or set(parameter) != {'name', 'type', 'default', 'modifiers'}:
                raise IndexFormatError('parameter 仅允许 name/type/default/modifiers')
            for name in ('name', 'type'):
                _text(parameter[name], 'parameter.' + name)
            if parameter['default'] is not None:
                _text(parameter['default'], 'parameter.default')
            if not isinstance(parameter['modifiers'], list) or not all(isinstance(x, str) for x in parameter['modifiers']):
                raise IndexFormatError('parameter.modifiers 必须是字符串列表')


def _manifest(data: dict) -> dict:
    """验证固定版本和精确计数；扩展清单字段不被默默接受。"""
    expected = {'kind', 'schemaVersion', *_COUNTS.values()}
    if not isinstance(data, dict) or set(data) != expected or data.get('kind') != 'manifest':
        raise IndexFormatError('索引首行必须是白名单 manifest')
    if type(data['schemaVersion']) is not int or data['schemaVersion'] != SCHEMA_VERSION:
        raise IndexFormatError('不支持的索引 schemaVersion')
    for name in _COUNTS.values():
        _integer(data[name], name)
    return data


def _decode(text: str):
    """拒绝重复 JSON 属性，防止不同消费者解释出不同的白名单内容。"""
    def pairs(items):
        """构造对象时检查重复属性名。"""
        result = {}
        for key, value in items:
            if key in result:
                raise IndexFormatError('重复 JSON 属性：' + key)
            result[key] = value
        return result
    try:
        return json.loads(text, object_pairs_hook=pairs)
    except (ValueError, RecursionError) as exc:
        raise IndexFormatError('无效索引 JSON：' + str(exc)) from exc


def _stream(path: Path):
    """逐行读取并校验快照；最后核对计数、唯一身份和文件指纹关系。"""
    counts = dict.fromkeys(_COUNTS.values(), 0)
    identities = set()
    files = {}
    declarations = {}
    try:
        with path.open('r', encoding='utf-8') as reader:
            header = _manifest(_decode(reader.readline()))
            yield 'manifest', header
            for number, line in enumerate(reader, 2):
                envelope = _decode(line)
                if not isinstance(envelope, dict) or set(envelope) != {'kind', 'data'}:
                    raise IndexFormatError('索引第 ' + str(number) + ' 行必须仅包含 kind/data')
                kind, data = envelope['kind'], envelope['data']
                _validate(kind, data)
                identity = (kind, _key(kind, data))
                if identity in identities:
                    raise IndexFormatError('重复索引身份：' + identity[1])
                identities.add(identity)
                counts[_COUNTS[kind]] += 1
                if kind == 'file':
                    files[data['source']] = data['sourceHash']
                elif kind in {'function', 'field'}:
                    previous = declarations.setdefault(data['source'], data['sourceFileHash'])
                    if previous != data['sourceFileHash']:
                        raise IndexFormatError('同一声明文件出现不同 sourceFileHash')
                yield kind, data
    except (UnicodeError, TypeError) as exc:
        raise IndexFormatError('索引编码或结构无效：' + str(exc)) from exc
    if any(counts[name] != header[name] for name in counts):
        raise IndexFormatError('manifest 计数与实际记录不一致')
    if any(files.get(source) != digest for source, digest in declarations.items()):
        raise IndexFormatError('声明 sourceFileHash 与 file 记录不一致或文件记录缺失')


def _key(kind: str, data: dict) -> str:
    """函数与文件使用稳定身份；条件字段变体使用声明内容摘要区分。"""
    if kind == 'function':
        return data['id']
    if kind in {'file', 'input'}:
        return data['source']
    return data['source'] + '#' + hashlib.sha256(_json(data).encode()).hexdigest()


def _references(record: dict, coverage: dict) -> list[dict]:
    """复用已有功能族和路径映射生成文档导航，不复制文档正文或推断语义。"""
    result = []
    for feature in coverage.get('features', []):
        if feature.get('id') != record.get('family') and not any(
                fnmatch.fnmatchcase(record['source'], pattern) for pattern in feature.get('sources', [])):
            continue
        for value in feature.get('docs', []):
            if isinstance(value, str):
                path, _, anchor = value.partition('#')
                result.append({'path': path, **({'anchor': anchor} if anchor else {})})
    return _doc_refs(result)


@lru_cache(maxsize=16)
def _documentation_paths(root: str) -> tuple[str, ...]:
    """构建期间枚举当前磁盘文档名；日常缓存查询不会调用或扫描文档树。"""
    base = Path(root)
    directories = [base / 'Assets/Framework/Docs']
    for packages in ('UPMPackages', 'Packages'):
        for package in sorted((base / packages).glob('com.solotopia.nova.framework*')):
            # 从实际目录项读取大小写，避免大小写不敏感磁盘上的 DOCS 别名污染路径。
            for parent in (package, package / 'Nova'):
                if parent.is_dir():
                    directories.extend(child for child in parent.iterdir()
                        if child.is_dir() and child.name in {'Docs', 'Doc', 'DOCS'})
    return tuple(sorted({path.relative_to(base).as_posix() for directory in directories
        if directory.is_dir() for path in directory.rglob('*.md') if path.is_file()}))


@lru_cache(maxsize=32)
def _documentation_map(root: str, prefix: str) -> tuple[dict, dict]:
    """为每个包一次构建大小写及同名导航表，避免逐函数重复遍历文档列表。"""
    by_case, by_name = {}, {}
    for path in _documentation_paths(root):
        if path.startswith(prefix):
            by_case[path.casefold()] = path
            by_name.setdefault(PurePosixPath(path).stem.casefold(), []).append(path)
    return by_case, by_name


@lru_cache(maxsize=256)
def _directory_names(directory: str, modified: int, changed: int) -> dict[str, str]:
    """按目录修改与变更时间缓存真实大小写，新增文档不会永久沿用缺失结果。"""
    path = Path(directory)
    return {child.name.casefold(): child.name for child in path.iterdir()} if path.is_dir() else {}


def _actual_document(root: Path, relative: str) -> str | None:
    """核验显式文档存在性并逐段规范大小写，不宽扫其它包或开发目录。"""
    current = root
    parts = []
    for part in _path(relative).split('/'):
        try:
            revision = current.stat()
        except OSError:
            return None
        actual = _directory_names(str(current), revision.st_mtime_ns, revision.st_ctime_ns).get(part.casefold())
        if actual is None:
            return None
        parts.append(actual)
        current = current / actual
    return '/'.join(parts) if current.is_file() else None


def resolve_doc_refs(record: dict, root: Path, coverage: dict | None = None) -> list[dict]:
    """优先实际领域入口，再解析同包镜像、唯一同名及显式后备导航，不表示已审。"""
    source = _path(record['source'])
    root = Path(root).resolve()
    parts = source.split('/')
    preferred = []
    explicit_domain = False
    if len(parts) > 3 and parts[:2] == ['Assets', 'Samples']:
        preferred = ['/'.join(parts[:3]) + '/' + name for name in ('README.md', 'DEMO-INDEX.md', 'DEMOINDEX.md')]
        explicit_domain = True
    elif len(parts) > 3 and parts[:2] == ['.agents', 'skills']:
        if parts[2] != '_shared':
            preferred = ['/'.join(parts[:3]) + '/SKILL.md']
        explicit_domain = True
    elif len(parts) > 5 and parts[:4] == ['Assets', 'Framework', 'Agents', 'Skills']:
        preferred = ['/'.join(parts[:5]) + '/SKILL.md']
        explicit_domain = True
    elif parts[:4] == ['Assets', 'Framework', 'Agents', 'Tools']:
        preferred = ['Assets/Framework/Agents/INDEX.md']
        explicit_domain = True
    for candidate in preferred:
        actual = _actual_document(root, candidate)
        if actual:
            return [{'path': actual}]
    if explicit_domain:
        result = []
        for ref in _references(record, coverage or {}):
            actual = _actual_document(root, ref['path'])
            if actual:
                result.append({**ref, 'path': actual})
        return _doc_refs(result)
    if parts[:2] == ['Assets', 'Framework']:
        prefix = 'Assets/Framework/'
    elif len(parts) > 2 and parts[0] in {'UPMPackages', 'Packages'}:
        prefix = '/'.join(parts[:2]) + '/'
    else:
        prefix = 'Assets/Framework/'
    by_case, by_name = _documentation_map(str(root), prefix)
    # 完整文件名镜像优先；类型名用于后续 partial 文件去后缀，避免跨包同名串线。
    mirror = str(PurePosixPath(source).with_suffix('.md')).replace('/Scripts/', '/Docs/')
    matched = by_case.get(mirror.casefold())
    if matched:
        return [{'path': matched}]
    stem = PurePosixPath(source).stem
    owner = record.get('owner') or record.get('type') or ''
    type_name = owner.split('.')[-1].split('<')[0]
    names = [stem]
    if type_name and (stem == type_name or stem.startswith(type_name + '.')):
        names.append(type_name)
    for name in names:
        matches = by_name.get(name.casefold(), [])
        if len(matches) == 1:
            return [{'path': matches[0]}]
    result = []
    for ref in _references(record, coverage or {}):
        # 后备引用也以实际文件大小写为准，并保持当前包归属。
        actual = by_case.get(ref['path'].casefold())
        if actual:
            result.append({**ref, 'path': actual})
    return _doc_refs(result)


def write_snapshot(index: dict, output: Path, coverage: dict | None = None, docs_resolver=None) -> dict:
    """将 AST 结果投影为确定性白名单快照；原子替换且失败时保留既有快照。"""
    rows = []
    field_keys = set()
    file_hashes = {item['source']: item['sourceHash'] for item in index.get('files', [])}
    for kind, collection in _KINDS.items():
        for original in index.get(collection, []):
            data = {key: value for key, value in original.items() if key in _ALLOWED[kind]}
            data.setdefault('family', 'framework.support')
            data.setdefault('kind', {'file': 'source', 'input': 'configuration'}.get(kind, kind))
            data['docRefs'] = _doc_refs(docs_resolver(original) if docs_resolver else _references(original, coverage or {}))
            if kind in {'function', 'field'}:
                data.setdefault('sourceFileHash', file_hashes.get(data['source']))
                data.setdefault('sourceHash', data['sourceFileHash'])
                data.setdefault('attributes', [])
                data.setdefault('type', '')
            if kind == 'function':
                for key in ('namespace', 'visibility', 'returns', 'signature'):
                    data.setdefault(key, '')
                data['parameters'] = [{key: p.get(key, [] if key == 'modifiers' else None if key == 'default' else '')
                    for key in ('name', 'type', 'default', 'modifiers')} for p in original.get('parameters', [])]
                data.setdefault('hasBody', True)
            elif kind == 'field':
                data.setdefault('owner', '')
                data.setdefault('serialized', False)
            _validate(kind, data)
            if kind == 'field':
                # 条件字段仅初始化表达式不同时，白名单声明相同，合并后不泄露初始化值。
                identity = _key(kind, data)
                if identity in field_keys:
                    continue
                field_keys.add(identity)
            rows.append((kind, data))
    rows.sort(key=lambda row: (list(_KINDS).index(row[0]), _key(*row)))
    manifest = {'kind': 'manifest', 'schemaVersion': SCHEMA_VERSION,
        **{count: sum(kind == category for kind, _ in rows) for category, count in _COUNTS.items()}}
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile('w', encoding='utf-8', newline='\n', prefix='.' + output.name + '-',
                suffix='.tmp', dir=output.parent, delete=False) as writer:
            temporary = Path(writer.name)
            writer.write(_json(manifest) + '\n')
            for kind, data in rows:
                writer.write(_json({'kind': kind, 'data': data}) + '\n')
            writer.flush()
            os.fsync(writer.fileno())
        # 写入前执行完整交叉校验，重复身份和错误指纹不得替换已有产物。
        for _ in _stream(temporary):
            pass
        os.replace(temporary, output)
        return {name: manifest[name] for name in _COUNTS.values()}
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def read_snapshot(path: Path) -> dict:
    """完整装载仅供构建或完整检查使用；日常查询通过 SQLite 定向取行。"""
    result = {name: [] for name in _KINDS.values()}
    for kind, data in _stream(Path(path)):
        if kind == 'manifest':
            result['manifest'] = data
        else:
            result[_KINDS[kind]].append(data)
    return result


def _identity(snapshot: Path) -> dict:
    """只用路径及文件元数据判断重建，热查询不读取整个快照计算摘要。"""
    stat = snapshot.stat()
    return {'path': str(snapshot), 'size': stat.st_size, 'mtime_ns': stat.st_mtime_ns, 'ctime_ns': stat.st_ctime_ns}


@contextmanager
def _locked(path: Path):
    """使用内核文件锁串行化跨进程缓存重建，进程退出会自动释放。"""
    with path.open('a+b') as handle:
        if os.name == 'nt':
            import msvcrt
            handle.seek(0, os.SEEK_END)
            if handle.tell() == 0:
                handle.write(b'\0')
                handle.flush()
            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
        try:
            yield
        finally:
            if os.name == 'nt':
                handle.seek(0)
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


def _connect(path: Path) -> sqlite3.Connection:
    """只读打开本地缓存，关闭不需要的可信 schema 和内存映射。"""
    connection = sqlite3.connect(path.as_uri() + '?mode=ro', uri=True)
    try:
        connection.execute('PRAGMA trusted_schema=OFF')
        connection.execute('PRAGMA query_only=ON')
        connection.execute('PRAGMA cache_size=-4096')
        connection.execute('PRAGMA mmap_size=0')
    except sqlite3.Error:
        # 损坏库可能在 PRAGMA 阶段失败，此时调用方还未取得连接，必须由本层关闭。
        connection.close()
        raise
    return connection


def _cached(path: Path, identity: dict):
    """检查完整缓存 schema、元数据及记录数；损坏时返回空以便锁内重建。"""
    connection = None
    try:
        connection = _connect(path)
        objects = connection.execute("SELECT sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'").fetchall()
        if {row[0] for row in objects} != set(_DDL) or len(objects) != len(_DDL):
            raise IndexFormatError('缓存 schema 不完整')
        meta = dict(connection.execute('SELECT key,value FROM meta'))
        if set(meta) != {'cacheVersion', 'snapshot', 'manifest', 'complete'} or meta['complete'] != 'true':
            raise IndexFormatError('缓存元数据不完整')
        if meta['cacheVersion'] != str(CACHE_VERSION) or _decode(meta['snapshot']) != identity:
            raise IndexFormatError('缓存已过期')
        manifest = _manifest(_decode(meta['manifest']))
        counts = dict(connection.execute('SELECT kind, count(*) FROM records GROUP BY kind'))
        if set(counts) - set(_KINDS) or any(counts.get(kind, 0) != manifest[name] for kind, name in _COUNTS.items()):
            raise IndexFormatError('缓存记录不完整')
        return connection, manifest
    except (sqlite3.Error, OSError, ValueError):
        if connection is not None:
            connection.close()
        return None


def _build(snapshot: Path, path: Path, identity: dict) -> None:
    """逐行构建临时库，校验快照未并发变化后才原子替换正式缓存。"""
    descriptor, name = tempfile.mkstemp(prefix='.index-', suffix='.sqlite', dir=path.parent)
    os.close(descriptor)
    temporary = Path(name)
    connection = None
    try:
        connection = sqlite3.connect(temporary)
        connection.execute('PRAGMA journal_mode=OFF')
        connection.execute('PRAGMA synchronous=FULL')
        for statement in _DDL:
            connection.execute(statement)
        manifest = None
        for position, (kind, data) in enumerate(_stream(snapshot)):
            if kind == 'manifest':
                manifest = data
                continue
            search = '\n'.join(str(data.get(key, '')) for key in (
                'id', 'source', 'name', 'signature', 'namespace', 'type', 'owner', 'kind')).casefold()
            connection.execute('INSERT INTO records VALUES (?,?,?,?,?,?,?)', (
                position, kind, _key(kind, data), data['source'], data['family'], search, _json(data)))
        if _identity(snapshot) != identity:
            raise IndexFormatError('快照在缓存构建期间发生变化，请重试')
        connection.executemany('INSERT INTO meta VALUES (?,?)', [
            ('cacheVersion', str(CACHE_VERSION)), ('snapshot', _json(identity)),
            ('manifest', _json(manifest)), ('complete', 'true')])
        connection.commit()
        connection.close()
        connection = None
        with temporary.open('rb') as reader:
            os.fsync(reader.fileno())
        os.replace(temporary, path)
    finally:
        if connection is not None:
            connection.close()
        temporary.unlink(missing_ok=True)


class Index:
    """只查询声明与文档定位信息，不读取 Knowledge，也不自动扫描或执行源码。"""

    def __init__(self, connection, manifest: dict, snapshot_path: Path, cache_path: Path):
        """持有一个已验证的只读缓存连接。"""
        self._connection = connection
        self.manifest = manifest
        self.counts = {name: manifest[name] for name in _COUNTS.values()}
        self.snapshot_path = snapshot_path
        self.cache_path = cache_path

    def _kind(self, kind: str) -> str:
        """接受单数记录种类或已有复数查询名称。"""
        aliases = {value: key for key, value in _KINDS.items()}
        kind = aliases.get(kind, kind)
        if kind not in _KINDS:
            raise ValueError('未知索引类别：' + str(kind))
        return kind

    def _record(self, kind: str, encoded: str) -> dict:
        """返回前复核单条缓存记录，缓存局部篡改不能绕过路径与载荷白名单。"""
        record = _decode(encoded)
        _validate(kind, record)
        return record

    def rows(self, kind: str, query: str | None = None, feature: str | None = None,
             limit: int = 25, offset: int = 0) -> list[dict]:
        """使用参数化 SQL 按类别、关键字与功能族分页；不载入其它记录。"""
        _integer(limit, 'limit')
        _integer(offset, 'offset')
        if limit > 1000:
            raise ValueError('分页 limit 不得超过 1000；完整检查使用 all_rows')
        predicates = ['kind=?']
        parameters = [self._kind(kind)]
        if feature:
            _text(feature, 'feature')
            predicates.append('family=?')
            parameters.append(feature)
        if query:
            _text(query, 'query')
            predicates.append('instr(search,?)>0')
            parameters.append(query.casefold())
        parameters += [limit, offset]
        sql = 'SELECT data FROM records WHERE ' + ' AND '.join(predicates) + ' ORDER BY position LIMIT ? OFFSET ?'
        return [self._record(parameters[0], row[0]) for row in self._connection.execute(sql, parameters)]

    def find_functions(self, query: str | None = None, feature: str | None = None,
                       limit: int = 25, offset: int = 0) -> list[dict]:
        """按需搜索函数声明，保留每个重载及分支身份。"""
        return self.rows('function', query, feature, limit, offset)

    def get_function(self, identity: str) -> dict | None:
        """通过唯一函数身份查找一条声明，不进行模糊匹配。"""
        _text(identity, '函数身份')
        row = self._connection.execute('SELECT data FROM records WHERE kind=? AND row_key=?', ('function', identity)).fetchone()
        return self._record('function', row[0]) if row else None

    def get_file(self, source: str) -> dict | None:
        """取得一个源文件指纹，来源必须是合法仓内路径。"""
        row = self._connection.execute('SELECT data FROM records WHERE kind=? AND row_key=?', ('file', _path(source))).fetchone()
        return self._record('file', row[0]) if row else None

    def all_rows(self, kind: str) -> list[dict]:
        """仅为完整检查显式装载某一类别；普通查询应使用分页入口。"""
        kind = self._kind(kind)
        return [self._record(kind, row[0]) for row in self._connection.execute(
            'SELECT data FROM records WHERE kind=? ORDER BY position', (kind,))]

    def close(self) -> None:
        """关闭连接；允许上下文退出或调用方重复清理。"""
        self._connection.close()

    def __enter__(self):
        """允许查询连接在 with 块中自动清理。"""
        return self

    def __exit__(self, *_):
        """退出查询范围时释放本进程文件句柄。"""
        self.close()


def open_index(root: Path, agents_root: Path, snapshot: Path | None = None) -> Index:
    """优先读取随包快照，否则读取项目本地快照；缺失时要求显式执行 index。"""
    root = Path(root).resolve()
    if snapshot is None:
        portable = Path(agents_root) / 'api-index.jsonl'
        snapshot = portable if portable.is_file() else root / 'Library/Nova/AgentIndex/api-index.jsonl'
    snapshot = Path(snapshot).resolve()
    if not snapshot.is_file():
        raise FileNotFoundError('未找到 API 定位快照；请先显式执行 nova_capabilities.py index，不会在查询时扫描全仓。')
    base = root / 'Library/Nova/AgentIndex'
    cache = base / hashlib.sha256(str(snapshot).encode()).hexdigest()[:24]
    if not cache.resolve().is_relative_to(root):
        raise IndexFormatError('缓存路径不得通过符号链接越出项目目录')
    cache.mkdir(parents=True, exist_ok=True)
    path = cache / 'index.sqlite'
    current = _identity(snapshot)
    result = _cached(path, current)
    if result is None:
        with _locked(cache / 'index.lock'):
            current = _identity(snapshot)
            result = _cached(path, current)
            if result is None:
                _build(snapshot, path, current)
                result = _cached(path, current)
                if result is None:
                    raise IndexFormatError('缓存构建后未通过完整性检查')
    return Index(*result, snapshot, path)
