#!/usr/bin/env python3
"""按需查询源码、XML/Docs 导航和既有执行契约；全量检查使用显式 check。"""
from __future__ import annotations
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path
sys.dont_write_bytecode = True
import nova_skills
import nova_api_index


def _read(path: Path):
    """读取当前 JSON 配置，不从历史审查或备份恢复语义。"""
    return json.loads(path.read_text(encoding='utf-8'))


def _configuration(root: Path, agents_root: Path | None = None):
    """解析当前项目唯一真源及任务导航，不要求预先存在缓存。"""
    root = root.resolve()
    if agents_root is None:
        local = root/'Assets/Framework/Agents'
        manifest = root/'Packages/manifest.json'
        installed = manifest.is_file() and 'com.solotopia.nova.framework' in _read(manifest).get('dependencies', {})
        agents_root = nova_skills.resolve_agents_root(root) if installed or not local.is_dir() else local
    agents_root = agents_root.resolve()
    coverage = _read(agents_root/'coverage.json')
    return root, agents_root, coverage


def _context(root: Path, agents_root: Path | None = None):
    """解析唯一包真源并打开本地派生缓存；不隐式全库重建。"""
    root, agents_root, coverage = _configuration(root, agents_root)
    return root, agents_root, coverage, nova_api_index.open_index(root, agents_root)


def _development_checkout(root: Path, agents_root: Path) -> bool:
    """仅当前真源位于工程 Assets/Framework 时使用开发仓来源范围，不凭残留目录猜环境。"""
    return (root/'Assets/Framework/Agents').resolve()==agents_root.resolve()

def _resolved_package_root(root: Path, package: str) -> Path | None:
    """按 lock 精确定位扩展包；残留本地目录和不同版本缓存不能覆盖当前解析结果。"""
    lock=root/'Packages/packages-lock.json'
    entry=_read(lock).get('dependencies',{}).get(package,{}) if lock.is_file() else {}
    version=entry.get('version')
    if entry.get('source')=='embedded':
        candidate=root/'Packages'/package
        return candidate if candidate.is_dir() else None
    if entry.get('source')=='local' and isinstance(version,str) and version.startswith('file:'):
        candidate=nova_skills._resolve_file_dependency(root/'Packages/manifest.json',version)
        return candidate if candidate.is_dir() else None
    if isinstance(version,str) and entry.get('source')=='registry':
        matches=[]
        for candidate in (root/'Library/PackageCache').glob(package+'@*'):
            manifest=candidate/'package.json'
            if manifest.is_file():
                info=_read(manifest)
                if info.get('name')==package and info.get('version')==version:
                    matches.append(candidate)
        if len(matches)==1:return matches[0]
    return None

def _sample_source_context(root: Path, agents_root: Path, source: str) -> dict:
    """用已安装包的 samples 元数据与精确导入路径判定 Sample，不做同名目录宽扫。"""
    parts=source.split('/')
    prefix='/'.join(parts[:3])
    if len(parts)<4:return {'status':'applicable','path':None,'reason':'Sample 路径缺精确相对文件'}
    sample_name=parts[2]
    # 手工保持开发路径的副本也必须自带当前 Sample 的权威 manifest 身份。
    direct=root/prefix
    candidates=[direct] if direct.is_dir() else []
    package_roots=[agents_root.parent]
    lock=root/'Packages/packages-lock.json'
    dependencies=_read(lock).get('dependencies',{}) if lock.is_file() else {}
    for package in dependencies:
        if package.startswith('com.solotopia.nova.framework.'):
            package_root=_resolved_package_root(root,package)
            if package_root is not None:package_roots.append(package_root)
    for package_root in package_roots:
        manifest=package_root/'package.json'
        if not manifest.is_file():continue
        info=_read(manifest)
        display,version=info.get('displayName'),info.get('version')
        if not all(isinstance(value,str) and value and '/' not in value and '\\' not in value
                   and value not in {'.','..'} for value in (display,version)):
            continue
        for sample in info.get('samples',[]):
            if not isinstance(sample,dict) or sample.get('path')!='Samples~/'+sample_name or sample.get('displayName')!=sample_name:
                continue
            imported=root/'Assets/Samples'/display/version/sample_name
            if imported.is_dir():candidates.append(imported)
            # 仅检查同一已解析包 displayName 下的版本目录；旧版留存也不能被误标未导入。
            parent=root/'Assets/Samples'/display
            for old_version in parent.iterdir() if parent.is_dir() else []:
                old_sample=old_version/sample_name
                if old_version.name!=version and old_sample.is_dir():
                    return {'status':'applicable','path':None,'reason':'已导入旧版或并存版本 Sample；当前包索引不能验证旧来源'}
    candidates=list(dict.fromkeys(candidates))
    if not candidates:return {'status':'not_applicable','path':None,'reason':'当前消费项目未导入该 Sample；包内模板不代表已安装业务源'}
    if len(candidates)!=1:return {'status':'applicable','path':None,'reason':'多个精确 Sample 导入位置冲突，不能猜选'}
    sample_root=candidates[0]
    identity=sample_root/'Editor/SamplePathManifest.asset'
    if not identity.is_file():return {'status':'applicable','path':None,'reason':'已导入 Sample 缺权威 SamplePathManifest'}
    text=identity.read_text(encoding='utf-8-sig')
    roots=re.findall(r'^\s*m_DevSampleRoot:\s*(.*?)\s*$',text,re.MULTILINE)
    if len(roots)!=1 or roots[0].strip('\"\'')!=prefix:
        return {'status':'applicable','path':None,'reason':'已导入 Sample manifest 来源身份不匹配'}
    # 同名 SO 文本不能证明真实 Sample 资产；只核当前 Sample 精确脚本与其 MonoScript 绑定。
    script=sample_root/'Scripts/Editor/SamplePathManifest.cs'
    script_meta=sample_root/'Scripts/Editor/SamplePathManifest.cs.meta'
    if not script.is_file() or not script_meta.is_file():
        return {'status':'applicable','path':None,'reason':'已导入 Sample 缺精确 SamplePathManifest 脚本或 meta'}
    guids=re.findall(r'^guid:\s*([0-9a-fA-F]{32})\s*$',script_meta.read_text(encoding='utf-8-sig'),re.MULTILINE)
    bindings=re.findall(r'^\s*m_Script:\s*\{([^{}]*)\}\s*$',text,re.MULTILINE)
    fields=[re.fullmatch(r'\s*(fileID|guid|type)\s*:\s*([^,\s]+)\s*',value)
            for value in bindings[0].split(',')] if len(bindings)==1 else []
    binding={field.group(1):field.group(2) for field in fields if field is not None}
    if (len(guids)!=1 or len(fields)!=3 or any(field is None for field in fields)
            or set(binding)!={'fileID','guid','type'} or binding.get('fileID')!='11500000'
            or binding.get('type')!='3' or binding.get('guid','').lower()!=guids[0].lower()):
        return {'status':'applicable','path':None,'reason':'已导入 Sample manifest 未绑定精确 SamplePathManifest MonoScript'}
    candidate=sample_root/Path(*parts[3:])
    return {'status':'applicable','path':candidate if candidate.is_file() else None,
            'reason':'精确导入目录、SamplePathManifest 来源与脚本绑定已核对；仍需源码指纹与实际运行验收'}

def _source_applicability(root: Path, agents_root: Path, source: str) -> dict:
    """区分开发专属、未安装扩展与未导入 Sample；真实缺损不能变成不适用。"""
    if _development_checkout(root,agents_root):return {'status':'applicable','reason':'开发仓完整来源范围'}
    if source=='NovaSpark.cs' or source.startswith('.nova/') or source.startswith('.agents/skills/'):
        return {'status':'not_applicable','reason':'开发仓工具和规则不随 Framework 消费快照分发；投影也不是真源'}
    if source.startswith('Assets/Samples/'):
        return _sample_source_context(root,agents_root,source)
    if source.startswith('UPMPackages/'):
        parts=source.split('/')
        lock=root/'Packages/packages-lock.json'
        installed=len(parts)>2 and lock.is_file() and parts[1] in _read(lock).get('dependencies',{})
        return {'status':'applicable' if installed else 'not_applicable',
                'reason':'lock 已解析扩展，缺文件属于损坏' if installed else '扩展未安装，仅提供随包导航'}
    return {'status':'applicable','reason':'消费项目或已安装 Framework 权威来源，缺文件必须阻断'}

def _installed_source(root: Path, source: str, agents_root: Path | None = None) -> bool:
    """保留已安装判断入口，统一按明确来源适用性处理消费与开发范围。"""
    return _source_applicability(root,agents_root or root/'Assets/Framework/Agents',source)['status']=='applicable'

def _entry_context(agents_root: Path,functions: list[dict]) -> dict:
    """复用真实 Handler 注册与 AST 特性，不维护第二份可执行白名单。"""
    registered=nova_skills._discover_registered_agent_actions(agents_root.parent)
    exposed=nova_skills._discover_exposed_agent_actions(agents_root.parent)
    pipify={match.group(1) for f in functions for attr in f.get('attributes',[])
            for match in re.finditer(r'\bPipifyStep(?:Attribute)?\s*\(\s*"([^"]+)"',attr)}
    cli=set();contracts={}
    for path in (agents_root/'Skills').glob('*/references/contract.json'):
        contract=_read(path);contracts[path.parents[1].name]=contract
        cli.update(a['entry'] for a in contract.get('actionAdapters',[]) if a.get('kind')=='cli')
    return {'actions':registered & exposed,'pipify':pipify,'functions':{f['id'] for f in functions},'cli':cli,'contracts':contracts}

def _navigation_adapter_errors(adapters,entries: dict,skills: list[str]) -> list[str]:
    """功能族只继承已核验 Skill 契约入口，不建立独立执行协议。"""
    if not isinstance(adapters,list):return ['功能族 Adapter 必须为数组']
    issues=[]
    for a in adapters:
        if not isinstance(a,dict) or a.get('kind') not in nova_skills.ACTION_ADAPTER_KINDS or not isinstance(a.get('entry'),str) or not a['entry'].strip() or not isinstance(a.get('when'),str) or not a['when'].strip():
            issues.append('功能族 Adapter 无效');continue
        if a['kind']=='agent-action' and a['entry'] not in entries['actions']:issues.append('功能族 Action 不存在或未暴露：'+a['entry'])
        if a['kind']=='cli' and a['entry'] not in entries['cli']:issues.append('功能族 CLI 不属于既有 Skill 契约：'+a['entry'])
        if 'contracts' in entries:
            allowed={(x['kind'],x['entry']) for sid in skills if isinstance(sid,str) for x in entries['contracts'].get(sid,{}).get('actionAdapters',[])}
            if (a['kind'],a['entry']) not in allowed:issues.append('功能族入口不属于关联 Skill 契约：'+a['entry'])
    return issues

def _source_path(root: Path, agents_root: Path, source: str) -> Path | None:
    """只解析当前项目或有效包内真实来源，拒绝路径跳转及越过权威根的符号链接。"""
    if (not isinstance(source, str) or not source or source.startswith('/') or '\\' in source
            or '..' in source.split('/') or re.match(r'^[A-Za-z]:', source)):
        return None

    def contained(candidate: Path, base: Path) -> Path | None:
        """包根可来自显式本地依赖，最终文件仍必须在该根内部。"""
        try:
            candidate.resolve().relative_to(base.resolve())
        except (ValueError, OSError):
            return None
        return candidate if candidate.is_file() else None

    development = _development_checkout(root, agents_root)
    if development:
        return contained(root/source, root)
    if source.startswith('Assets/Samples/'):
        context = _sample_source_context(root, agents_root, source)
        candidate = context.get('path')
        # 精确 Sample 身份已经验证；导入目录不能经链接逃出消费项目。
        return contained(candidate, root) if candidate else None
    if _source_applicability(root, agents_root, source)['status'] == 'not_applicable':
        return None
    if source.startswith('Assets/Framework/'):
        return contained(agents_root.parent/source.removeprefix('Assets/Framework/'), agents_root.parent)
    if source.startswith('UPMPackages/'):
        parts = source.split('/', 2)
        if len(parts) != 3 or not all(parts):
            return None
        package_root = _resolved_package_root(root, parts[1])
        return contained(package_root/parts[2], package_root) if package_root else None
    return contained(root/source, root)


def _function_body(path: Path,node: dict) -> str:
    """按 C# UTF-16 span 或 Python 规范换行片段核验准确正文，不沿用错误偏移。"""
    with path.open(encoding='utf-8-sig',newline='') as stream:
        source=stream.read()
    if 'startChar' in node and 'endChar' in node:
        encoded=source.encode('utf-16-le')
        body=encoded[node['startChar']*2:node['endChar']*2].decode('utf-16-le')
        if hashlib.sha256(body.encode('utf-8')).hexdigest()!=node['sourceHash']:
            raise ValueError('函数 span 与正文指纹不一致：'+node['id'])
        return body
    source = source.replace('\r\n', '\n').replace('\r', '\n')
    body = ''.join(source.splitlines(keepends=True)[node['line']-1:node['endLine']])
    if hashlib.sha256(body.encode('utf-8')).hexdigest() != node['sourceHash']:
        raise ValueError('函数行范围与正文指纹不一致：' + node['id'])
    return body

def _validation_summary(validation: dict) -> dict:
    """只展示当前任务验收要求与临时保留策略，不累计历史验收进度。"""
    result={key:validation[key] for key in ('status','required','retention','boundary') if key in validation}
    result['counts']={'required':len(validation.get('required',[]))}
    return result


def _source_state(root: Path, agents_root: Path, node: dict) -> str:
    """只核命中来源的当前字节；指纹用于保护位置，不代表语义或运行已验证。"""
    source = node['source']
    if _source_applicability(root, agents_root, source)['status'] == 'not_applicable':
        return 'not_applicable'
    path = _source_path(root, agents_root, source)
    if path is None:
        return 'unavailable'
    expected = node.get('sourceFileHash', node.get('sourceHash'))
    return 'current' if hashlib.sha256(path.read_bytes()).hexdigest() == expected else 'stale'


def _navigation(node: dict, cov: dict) -> dict:
    """返回定位信息和文档引用；不把导航存在转换为已审、有效配置或可执行权限。"""
    family = next((item for item in cov.get('features', []) if item['id'] == node.get('family')), {})
    return dict(node, skills=family.get('skills', []), semanticAuthority='source-xml-docs-contract',
                semanticReview='not-performed', effectiveConfiguration='unknown; read actual project state')


def _task_report(root: Path, agents_root: Path, cov: dict, index, tasks: list[dict]) -> dict:
    """复用任务契约校验；仅精读本次选中任务的函数，保留原有执行与最低证据门。"""
    states = {}
    for task in tasks:
        for key in task.get('functions', []):
            node = index.get_function(key)
            if node is not None:
                states[key] = _source_state(root, agents_root, node)
            elif _source_applicability(root, agents_root, key.split('#', 1)[0])['status'] == 'not_applicable':
                states[key] = 'not_applicable'
    selected = dict(cov, userTasks=tasks, features=[])
    # Entry 集合来自真实 Handler/ExposurePolicy；不由说明文案生成可执行入口。
    entries = _entry_context(agents_root, [])
    report = nova_skills.execution_coverage_report(root, agents_root, selected,
        function_states=states, available_entries=entries)
    for row, task in zip(report['items'], tasks):
        applicable = any(states.get(key) != 'not_applicable' for key in task.get('functions', []))
        row['applicability'] = 'applicable' if applicable else 'not_applicable'
        if not applicable:
            row['validation']['status'] = 'not_applicable'
    return report


def _contains_query(node: dict, query: str | None) -> bool:
    """导航筛选只匹配当前记录，不展开全部函数和源码。"""
    return not query or query.casefold() in json.dumps(node, ensure_ascii=False).casefold()


def _task_summary(row: dict) -> dict:
    """保留任务执行边界与本次验收要求，省去重复的完整契约。"""
    result = {key: row[key] for key in ('id', 'goal', 'skill', 'sourceStatus', 'semanticReview',
        'entryStatus', 'executionStatus', 'mode', 'applicability', 'liveAvailability', 'gaps') if key in row}
    result['validation'] = _validation_summary(row['validation'])
    return result


def inventory(root: Path, agents_root: Path | None, kind: str, feature: str | None,
              query: str | None, limit: int, offset: int, detail: str = 'summary') -> dict:
    """先筛选分页再读命中来源；查询一个函数不遍历全索引、Docs 或契约。"""
    root, agents_root, cov, index = _context(root, agents_root)
    with index:
        if kind in {'features', 'tasks'}:
            key = 'features' if kind == 'features' else 'userTasks'
            candidates = cov.get(key, [])
            family = next((x for x in cov.get('features', []) if x['id'] == feature), {})
            candidates = [x for x in candidates if
                (not feature or (x['id'] == feature if kind == 'features' else x['id'] in family.get('taskRefs', [])))
                and _contains_query(x, query)]
            page = candidates[offset:offset + limit]
            if kind == 'tasks':
                report = _task_report(root, agents_root, cov, index, page)
                page = [_task_summary(x) for x in report['items']] if detail == 'summary' else report['items']
            elif detail == 'summary':
                page = [{k: x[k] for k in ('id', 'docs', 'skills', 'taskScope', 'taskRefs', 'limitations') if k in x} for x in page]
            more = offset + limit < len(candidates)
        else:
            # 多取一条用于分页，避免为了 total 扫描所有来源文件。
            row_kind = 'function' if kind == 'entries' else kind
            page = index.rows(row_kind, query=query, feature=feature, limit=limit + 1, offset=offset)
            more, page = len(page) > limit, page[:limit]
            states = {}
            output = []
            for node in page:
                if node['source'] not in states:
                    states[node['source']] = _source_state(root, agents_root, node)
                output.append(_navigation(dict(node, sourceStatus=states[node['source']]), cov))
            page = output
        return {'status': 'success', 'kind': kind, 'items': page, 'offset': offset,
                'nextOffset': offset + limit if more else None, 'detail': detail,
                'note': '导航和静态声明不证明运行通过；执行仍须当前 Skill 与 live Action 契约'}


def describe(root: Path, agents_root: Path | None, key: str, detail: str = 'summary') -> dict:
    """精确展开单个函数；只有文件与 span 指纹均匹配时才返回正文和附近 XML。"""
    root, agents_root, cov, index = _context(root, agents_root)
    with index:
        family = next((x for x in cov.get('features', []) if x['id'] == key), None)
        if family:
            return {'status': 'success', 'feature': family,
                    'semanticAuthority': 'source-xml-docs-contract', 'semanticReview': 'not-performed'}
        task = next((x for x in cov.get('userTasks', []) if x['id'] == key), None)
        if task:
            report = _task_report(root, agents_root, cov, index, [task])
            if not report['items']:
                return {'status': 'blocked', 'taskId': key, 'errors': report['errors']}
            return {'status': 'success' if not report['errors'] else 'blocked',
                    'task': _task_summary(report['items'][0]) if detail == 'summary' else report['items'][0],
                    'errors': report['errors']}
        node = index.get_function(key)
        if node is None:
            matches = index.find_functions(key, limit=21)
            if len(matches) != 1:
                return {'status': 'ambiguous' if matches else 'not_found', 'key': key,
                        'candidates': [x['id'] for x in matches[:20]]}
            node = matches[0]
        state = _source_state(root, agents_root, node)
        result = {'status': 'success' if state == 'current' else state,
                  'function': _navigation(dict(node, sourceStatus=state), cov), 'sourceBody': None,
                  'execution': 'use current Skill; Action requires live describe/plan/execute/verify'}
        if state != 'current':
            result['message'] = ('当前位置不可沿用；开发仓显式重建索引，消费项目核对实际已安装包；'
                                 '指纹变化不产生历史审查续签任务')
        elif detail == 'full':
            path = _source_path(root, agents_root, node['source'])
            result['sourceBody'] = _function_body(path, node)
            # XML 注释留在源码唯一真源，按命中函数附近读取，不复制进永久快照。
            lines = path.read_text(encoding='utf-8-sig').splitlines()
            start = node['line'] - 1
            while start > 0 and (lines[start - 1].lstrip().startswith('///') or
                                  lines[start - 1].lstrip().startswith('[')):
                start -= 1
            result['sourceDocumentation'] = '\n'.join(lines[start:node['line'] - 1])
        return result


class _CheckSnapshot:
    """显式重建检查的内存视图，不建立可遗留的 SQLite 或工程快照。"""

    def __init__(self, data: dict):
        """只接收已经过便携格式校验的临时快照。"""
        self.data = data
        self.counts = {key: data['manifest'][key] for key in
                       ('functions', 'fields', 'sourceFiles', 'capabilityInputs')}

    def all_rows(self, kind: str) -> list[dict]:
        """完整检查显式读取当前类别的声明。"""
        return self.data[{'function': 'functions', 'field': 'fields', 'file': 'files', 'input': 'inputs'}[kind]]

    def __enter__(self):
        """与只读缓存视图使用同一检查路径。"""
        return self

    def __exit__(self, *_):
        """本视图不持有文件句柄，临时文件由构建范围负责清理。"""
        return None


def check(root: Path, agents_root: Path | None = None, *, _snapshot_data: dict | None = None) -> dict:
    """显式全量检查导航、源码/输入指纹和执行契约；不颁发人工语义或运行已审状态。"""
    if _snapshot_data is None:
        root, agents_root, cov, index = _context(root, agents_root)
    else:
        root, agents_root, cov = _configuration(root, agents_root)
        index = _CheckSnapshot(_snapshot_data)
    errors, warnings = [], []
    with index:
        functions, files = index.all_rows('function'), index.all_rows('file')
        fields, inputs = index.all_rows('field'), index.all_rows('input')
        if cov.get('schemaVersion') != 2:
            errors.append('coverage schemaVersion 必须为 2')
        forbidden = {'reviewFiles', 'inputReviewFiles', 'reviewPolicy', 'reviewResolution'} & set(cov)
        if forbidden:
            errors.append('coverage 仍引用已退出的人工知识账本：' + ','.join(sorted(forbidden)))
        families = cov.get('features', [])
        family_ids = {x['id'] for x in families}
        if len(family_ids) != len(families):
            errors.append('功能族 ID 重复')
        known = {x['source']: x for x in files}
        freshness = {source: _source_state(root, agents_root, node) for source, node in known.items()}
        for kind, records in (('源码', files), ('能力输入', inputs)):
            for node in records:
                state = freshness.get(node['source']) if kind == '源码' else _source_state(root, agents_root, node)
                if state in {'stale', 'unavailable'}:
                    errors.append(kind + '索引需刷新：' + node['source'])
        for node in functions + fields:
            if node.get('family') not in family_ids:
                errors.append('声明缺功能族：' + str(node.get('id', node.get('source'))))
            if node.get('sourceFileHash') != known.get(node['source'], {}).get('sourceHash'):
                errors.append('声明与文件指纹不一致：' + node['source'])
            # 只有 current 来源才可按 span 验证，陈旧偏移不得用来提取代码。
            if 'id' in node and freshness.get(node['source']) == 'current':
                try:
                    _function_body(_source_path(root, agents_root, node['source']), node)
                except ValueError as error:
                    errors.append(str(error))
            for reference in node.get('docRefs', []):
                path = reference['path']
                if _source_path(root, agents_root, path) is None and _installed_source(root, path, agents_root):
                    errors.append('声明文档不可达：' + path)
        catalog = nova_skills.load_catalog(agents_root)
        skill_ids = {x['id'] for x in catalog['skills']}
        entries = _entry_context(agents_root, functions)
        mapped_skills = set()
        for family in families:
            for name in ('docs', 'skills', 'limitations'):
                if not isinstance(family.get(name), list) or not family[name]:
                    errors.append('功能族缺 ' + name + '：' + family['id'])
            for path in family.get('docs', []):
                if _source_path(root, agents_root, path) is None and _installed_source(root, path, agents_root):
                    errors.append('功能族文档不可达：' + path)
            for skill in family.get('skills', []):
                mapped_skills.add(skill)
                if skill not in skill_ids:
                    errors.append('功能族未知 Skill：' + skill)
            errors.extend(family['id'] + ':' + issue for issue in
                _navigation_adapter_errors(family.get('adapters'), entries, family.get('skills', [])))
        if skill_ids - mapped_skills:
            errors.append('Skill 无功能归属：' + ','.join(sorted(skill_ids - mapped_skills)))
        if _development_checkout(root, agents_root):
            from nova_function_index import source_files, input_files
            for label, actual, indexed in (
                ('源码', source_files(root), set(known)),
                ('输入', input_files(root), {x['source'] for x in inputs})):
                current = {p.relative_to(root).as_posix() for p in actual}
                if current - indexed:
                    errors.append('新增' + label + '未索引：' + ','.join(sorted(current - indexed)))
                if indexed - current:
                    errors.append('已删除' + label + '仍在索引：' + ','.join(sorted(indexed - current)))
        errors.extend(nova_skills.validate_agents_root(agents_root))
        states = {node['id']: freshness.get(node['source'], 'unavailable') for node in functions}
        # 未分发的扩展/开发来源可明确不适用；Framework 自身缺函数仍是错误。
        for task in cov.get('userTasks', []):
            for key in task.get('functions', []):
                if key not in states and not _installed_source(root, key.split('#', 1)[0], agents_root):
                    states[key] = 'not_applicable'
        execution = nova_skills.execution_coverage_report(root, agents_root, cov,
            function_states=states, available_entries=entries)
        errors.extend(execution['errors'])
        warnings.extend(execution['warnings'])
        return {'valid': not errors, 'errors': errors, 'warnings': warnings,
                'summary': dict(index.counts, featureFamilies=len(families), skills=len(skill_ids),
                    semanticAuthority='source-xml-docs-contract', semanticReview='not-performed',
                    executionCoverage=execution['summary']), 'executionCoverage': execution['items']}


def check_rebuilt(root: Path, agents_root: Path | None = None) -> dict:
    """开发/发布显式从当前源检查，首次 Git 检出无需先保存 Library 索引。"""
    import tempfile
    from nova_function_index import build_index
    root, agents_root, coverage = _configuration(root, agents_root)
    if not _development_checkout(root, agents_root):
        raise ValueError('check --rebuild 仅供框架开发仓；消费项目读取已安装便携索引')
    parsed = build_index(root)
    if parsed.get('parseIssues'):
        raise ValueError('AST 解析存在问题，未执行后续发布检查：' + str(parsed['parseIssues']))
    with tempfile.TemporaryDirectory(prefix='nova-navigation-check-') as directory:
        snapshot = Path(directory)/'api-index.jsonl'
        nova_api_index.write_snapshot(parsed, snapshot, coverage,
            docs_resolver=lambda record: nova_api_index.resolve_doc_refs(record, root, coverage))
        data = nova_api_index.read_snapshot(snapshot)
    # 私有完整 AST 不进入缓存；这里只把已校验的结构白名单用于当前全量检查。
    del parsed
    return check(root, agents_root, _snapshot_data=data)


def main(argv=None) -> int:
    """固定查询入口；index 只写派生缓存，不执行目标函数。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path.cwd())
    parser.add_argument('--agents-root', type=Path)
    sub = parser.add_subparsers(dest='command', required=True)
    inv = sub.add_parser('inventory')
    inv.add_argument('--kind', choices=['functions', 'fields', 'files', 'inputs', 'entries', 'features', 'tasks'], default='features')
    inv.add_argument('--feature'); inv.add_argument('--query')
    inv.add_argument('--limit', type=int, default=25); inv.add_argument('--offset', type=int, default=0)
    desc = sub.add_parser('describe'); desc.add_argument('key')
    chk = sub.add_parser('check')
    chk.add_argument('--rebuild', action='store_true', help='开发/发布显式解析当前源，仅使用自动清理的临时快照')
    build = sub.add_parser('index')
    for command in (inv, desc):
        command.add_argument('--detail', choices=['summary', 'full'], default='summary')
    for command in (inv, desc, chk, build):
        command.add_argument('--json', action='store_true')
    args = parser.parse_args(argv)
    try:
        if args.command == 'index':
            from nova_function_index import build_navigation
            result = build_navigation(args.root.resolve())
        elif args.command == 'check':
            result = check_rebuilt(args.root, args.agents_root) if args.rebuild else check(args.root, args.agents_root)
        elif args.command == 'describe':
            result = describe(args.root, args.agents_root, args.key, args.detail)
        else:
            if not 1 <= args.limit <= 200 or args.offset < 0:
                raise ValueError('分页 limit 需 1..200，offset >= 0')
            result = inventory(args.root, args.agents_root, args.kind, args.feature, args.query,
                               args.limit, args.offset, args.detail)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1 if result.get('valid') is False else 0
    except (ValueError, KeyError, TypeError, OSError, RuntimeError, subprocess.CalledProcessError,
            nova_skills.NovaSkillsError) as error:
        print(json.dumps({'valid': False, 'errors': [str(error)], 'warnings': [], 'summary': {}}, ensure_ascii=False))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
