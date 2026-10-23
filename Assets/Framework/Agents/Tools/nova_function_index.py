#!/usr/bin/env python3
"""使用 AST 建立逐函数/重载、字段和输入指纹索引；只解析声明，不推断运行时语义。"""
from __future__ import annotations
import argparse
import ast
import hashlib
import json
import os
import re
import signal
import subprocess
import time
from pathlib import Path

_AST_TIMEOUT_SECONDS = 120.0

EXCLUDED = {'Proto', 'Protos', 'Generated', 'GeneratedCode', 'bin', 'obj', '__pycache__',
            'Plugins', 'Resources', 'GameDlls'}
INPUT_EXCLUDED = {'Agents', 'Minds', 'Vendor', 'Vendors', 'ThirdParty', 'Third-Party',
                  'Proto', 'Protos', 'Generated', 'GeneratedCode', 'bin', 'obj', '__pycache__', 'GameDlls'}
PYTHON_TOOL_EXCLUDED = EXCLUDED | {'test', 'tests', 'Tests', 'fixtures', 'Fixtures',
                                  'templates', 'Templates', 'Vendor', 'Vendors', 'ThirdParty',
                                  'Third-Party', 'node_modules', '.venv', 'venv'}
OWNED_SOURCE_EXCLUDED = (EXCLUDED - {'Plugins'}) | {'test', 'tests', 'Tests', 'Vendor', 'Vendors',
                                                 'ThirdParty', 'Third-Party', 'node_modules'}
# 一般扫描外的自有输入按已核实的包与路径登记，不凭文件名猜归属或扩展厂商扫描。
OWNED_EXTERNAL_INPUTS = {
    'com.solotopia.nova.framework.sdk.max': (
        'Nova/Scripts/Editor/BuildProcessor/MaxIOSPodOwnership.rb',
    ),
    'com.solotopia.nova.framework.sdk.wechat.minigame': (
        'ThirdParty/GravityEngine-5.0.45/GravityEngine.WeChat.Runtime.asmdef',
    ),
}
# 非 C#/Python 的本地桥接仅作为权威源码输入登记，尚不声称已有逐函数 AST 覆盖。
OWNED_NATIVE_INPUTS = (
    'Assets/Framework/Plugins/Android/NovaNativeReview.androidlib/src/main/java/com/solotopia/nova/nativebridge/NovaNativeReviewBridge.java',
    'Assets/Framework/Plugins/Android/NovaNativeAppInstaller.androidlib/src/main/java/com/solotopia/nova/nativebridge/NovaNativeAppInstallerBridge.java',
    'Assets/Framework/Plugins/iOS/NovaNativeInAppReview.swift',
    'Assets/Framework/Plugins/iOS/NovaNativeNotification.mm',
    'Assets/Framework/Scripts/Runtime/Core/Util/Util.SysIO/Plugins/WebGL/WebGLExtensions.jslib',
    'Assets/Framework/Scripts/Runtime/Modules/Network/Managers/WebSocketManager/WebSocket/Plugins/WebGL/WebSocket.jslib',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Plugins/Android/NovaThirdPayBillingBridge.java',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Plugins/Android/ThirdPayAuthTab.androidlib/src/main/java/com/solotopia/nova/thirdpay/NovaThirdPayAuthTabActivity.java',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Plugins/iOS/NovaThirdPayStorefrontRegion.mm',
    'UPMPackages/com.solotopia.nova.framework.sdk.wechat.minigame/Nova/Plugins/NovaDataNexusBridge.jslib',
    'UPMPackages/com.solotopia.nova.framework.sdk.wechat.minigame/Nova/Plugins/NovaWechatAssetEnvironment.jslib',
)
# 只有已核实生产者的清单模板、依赖声明和 Android 构建输入可获得扫描外归属。
OWNED_BUILD_INPUTS = (
    'Assets/Framework/Scripts/Editor/BuildProcessor/Android/UnityManifest.xml',
    'Assets/Framework/Plugins/Android/NovaNativeReview.androidlib/AndroidManifest.xml',
    'Assets/Framework/Plugins/Android/NovaNativeReview.androidlib/consumer-rules.pro',
    'Assets/Framework/Plugins/Android/NovaNativeReview.androidlib/build.gradle',
    'Assets/Framework/Plugins/Android/NovaNativeAppInstaller.androidlib/AndroidManifest.xml',
    'Assets/Framework/Plugins/Android/NovaNativeAppInstaller.androidlib/consumer-rules.pro',
    'Assets/Framework/Plugins/Android/NovaNativeAppInstaller.androidlib/build.gradle',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Scripts/Editor/Dependencies/ThirdPayDependencies.xml',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Plugins/Android/ThirdPayAuthTab.androidlib/AndroidManifest.xml',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Plugins/Android/ThirdPayAuthTab.androidlib/consumer-rules.pro',
    'UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Plugins/Android/ThirdPayAuthTab.androidlib/build.gradle',
    'UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Plugins/Android/Firebase.androidlib/AndroidManifest.xml',
    'UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Plugins/Android/Firebase.androidlib/build.gradle',
    'UPMPackages/com.solotopia.nova.framework.sdk.googlesignin/Nova/Scripts/Editor/Dependencies/GoogleSignInDependencies.xml',
    'Assets/Samples/FirebaseDemo/Plugins/iOS/GoogleService-Info.plist',
    'Assets/Samples/FirebaseDemo/Plugins/Android/google-services.json',
    # 本地化源编辑 CLI 以该随包枚举冻结允许的语言名称；保留精确依赖，不扩扫 C# 输入。
    'Assets/Framework/Scripts/Runtime/Core/Definitions/Language.cs',
)


def source_files(root: Path, discovery: dict | None = None) -> list[Path]:
    """枚举自有 C#/Python 源及生产工具；开发 Skill 与消费投影分开，测试和生成物不作功能源。"""
    bases = [root / 'Assets/Framework/Scripts', root / 'Assets/Framework/Plugins']
    bases += [p / 'Nova' for p in sorted((root / 'UPMPackages').glob('com.solotopia.nova.framework.*'))]
    files = []
    for base in bases:
        if not base.exists():
            continue
        for path in base.rglob('*'):
            if path.suffix not in {'.cs', '.py'} or any(x in EXCLUDED for x in path.relative_to(base).parts[:-1]):
                continue
            files.append(path)
    # SDK 的 Plugins 是自有接口目录，不能由通用厂商目录排除规则吞掉。
    sdk_contracts = root / 'Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins'
    contracts = [path for path in sdk_contracts.rglob('*.cs') if path.is_file() and not any(
        part in OWNED_SOURCE_EXCLUDED for part in path.relative_to(sdk_contracts).parts[:-1])]
    files += contracts
    # Sample 复制模板也是当前生产源；仅按实际生成标记、测试或明确目录边界排除。
    samples = root / 'Assets/Samples'
    sample_paths = []
    sample_exclusions = []
    for path in sorted(samples.rglob('*.cs')):
        if not path.is_file():
            continue
        directories = path.relative_to(samples).parts[:-1]
        if any(part in {'test', 'tests', 'Tests'} for part in directories) or path.name.startswith('test_'):
            reason = 'test'
        elif re.search(r'(?m)^\s*//+\s*<(?:auto-generated|nova-generated)\b', path.read_text(encoding='utf-8')):
            reason = 'generated'
        elif any(part in OWNED_SOURCE_EXCLUDED for part in directories):
            reason = 'excluded-directory'
        else:
            sample_paths.append(path)
            continue
        sample_exclusions.append({'source': path.relative_to(root).as_posix(), 'reason': reason})
    files += sample_paths
    bootstrap = root / 'NovaSpark.cs'
    if bootstrap.is_file():
        files.append(bootstrap)
    # Tools 的顶层 Python 是随包真源；不递归审查账本、测试、C# 解析器模板或 Skill 投影。
    files += [path for path in (root / 'Assets/Framework/Agents/Tools').glob('*.py')
              if path.is_file() and not path.name.startswith('test_')]
    # 消费 Skill 的生产脚本随包分发，纳入函数审查；根目录 nova-project 投影仍不读取。
    consumer_skills = root / 'Assets/Framework/Agents/Skills'
    for skill in sorted(consumer_skills.glob('nova-project-*')):
        base = skill / 'scripts'
        if not (skill / 'SKILL.md').is_file() or not base.is_dir():
            continue
        files += [path for path in base.rglob('*.py')
                  if path.is_file() and not path.name.startswith('test_') and not any(
                      part in PYTHON_TOOL_EXCLUDED for part in path.relative_to(base).parts[:-1])]
    skill_root = root / '.agents/skills'
    python_bases = [path / 'scripts' for path in sorted(skill_root.glob('nova-*'))
                    if path.is_dir() and not path.name.startswith('nova-project-')
                    and (path / 'SKILL.md').is_file()]
    python_bases.append(skill_root / '_shared')
    for base in python_bases:
        if not base.is_dir():
            continue
        for path in base.rglob('*.py'):
            if not path.is_file() or path.name.startswith('test_') or any(
                    part in PYTHON_TOOL_EXCLUDED for part in path.relative_to(base).parts[:-1]):
                continue
            files.append(path)
    result = sorted(set(files))
    if discovery is not None:
        discovery.update({'sdkContractSources': len(contracts), 'bootstrapSources': int(bootstrap.is_file()),
            'samples': {'candidates': len(sample_paths) + len(sample_exclusions), 'production': len(sample_paths),
                'excludedCounts': {reason: sum(item['reason'] == reason for item in sample_exclusions)
                                   for reason in ('generated', 'test', 'excluded-directory')},
                'exclusions': sample_exclusions}})
    return result


def family_for(source: str) -> str:
    """按真实源码归属提供导航族；函数身份仍独立保存。"""
    parts = source.split('/')
    if source == 'NovaSpark.cs':
        return 'development.bootstrap'
    if source.startswith('Assets/Framework/Plugins/'):
        return 'runtime.Native'
    if source.startswith('Assets/Framework/Protos/'):
        return 'runtime.Network'
    if source.startswith('Assets/Framework/Templates/Luban/'):
        return 'editor.tool.EditorUtil.Luban'
    if parts[:2] == ['Assets', 'Samples'] and len(parts) > 2:
        return 'sample.' + parts[2]
    if source == 'Assets/Framework/Agents/INDEX.md' or (
            parts[:3] == ['Assets', 'Framework', 'Agents'] and len(parts) > 3
            and parts[3] in {'Tools', 'Skills'}):
        return 'framework.agents.tools'
    if parts[:2] == ['.agents', 'skills'] and len(parts) > 2:
        return 'development.shared' if parts[2] == '_shared' else 'development.skill.' + parts[2]
    if parts[0] == 'UPMPackages':
        return 'package.' + parts[1].removeprefix('com.solotopia.nova.framework.')
    if '/Runtime/Modules/' in source:
        return 'runtime.' + source.split('/Runtime/Modules/')[1].split('/')[0]
    if '/Runtime/Core/' in source:
        return 'core.' + source.split('/Runtime/Core/')[1].split('/')[0]
    if '/Runtime/Utils/' in source:
        return 'runtime.utils'
    for folder, prefix in [('EditorUtil', 'editor.tool.'), ('Inspectors', 'editor.inspector.'),
                           ('Windows', 'editor.window.'), ('DataPipeline', 'editor.pipeline.')]:
        marker = '/Editor/' + folder + '/'
        if marker in source:
            item = source.split(marker)[1].split('/')[0]
            return prefix + (item if not item.endswith('.cs') else folder)
    if '/Editor/' in source:
        return 'editor.' + source.split('/Editor/')[1].split('/')[0].removesuffix('.cs')
    return 'framework.support'


def input_files(root: Path, discovery: dict | None = None) -> list[Path]:
    """枚举自有契约及明确登记的扫描外输入；其余厂商内容和审查投影仍排除。"""
    framework = root / 'Assets/Framework'
    packages = sorted(path for path in (root / 'UPMPackages').glob('com.solotopia.nova.framework.*') if path.is_dir())
    # 从目录项取得真实大小写，避免 macOS 把同一 Docs 目录再次登记为 DOCS。
    bases = [framework] + [path for package in packages for path in package.iterdir()
                           if path.is_dir() and path.name in {'Nova', 'Docs', 'Doc', 'DOCS', 'Tracks'}]
    files = []
    for base in bases:
        if not base.is_dir():
            continue
        for path in base.rglob('*'):
            if not path.is_file():
                continue
            relative = path.relative_to(base)
            directories = relative.parts[:-1]
            if any(part in INPUT_EXCLUDED for part in directories):
                continue
            if any(directories[index:index + 3] == ('Docs', 'Onboarding', 'Reviews')
                   for index in range(len(directories))):
                continue
            suffix = path.suffix.lower()
            owned_directories = path.relative_to(root).parts[:-1]
            documentation = suffix == '.md' and any(part in {'Docs', 'Doc', 'DOCS'} for part in owned_directories)
            table = suffix in {'.xlsx', '.xls', '.csv', '.json'} and any(part in {'Templates', 'Tracks'} for part in owned_directories)
            configuration = suffix in {'.asset', '.prefab', '.unity', '.asmdef'} or path.name in {'link.xml', 'package.json'}
            if documentation or table or configuration:
                files.append(path)
    files += [path for path in [framework / 'package.json'] + [package / 'package.json' for package in packages]
              if path.is_file()]
    files += [package / relative for package in packages
              for relative in OWNED_EXTERNAL_INPUTS.get(package.name, ())
              if (package / relative).is_file()]
    # Proto 与 SBN 是生成代码的权威输入，仅扫描已经核实的自有生产者目录。
    producer_bases = [(framework / 'Protos', '*.proto')]
    producer_bases += [(package / 'Nova/Protos', '*.proto') for package in packages]
    producer_bases.append((framework / 'Templates/Luban', '*.sbn'))
    for base, pattern in producer_bases:
        files += [path for path in base.rglob(pattern) if path.is_file() and not any(
            part in INPUT_EXCLUDED for part in path.relative_to(base).parts[:-1])]
    files += [root / source for source in OWNED_NATIVE_INPUTS + OWNED_BUILD_INPUTS
              if (root / source).is_file()]
    descriptors = [framework / 'nova-samples.json'] + [package / 'nova-samples.json' for package in packages]
    files += [path for path in descriptors if path.is_file()]
    sample_inputs = []
    sample_exclusions = []
    samples = root / 'Assets/Samples'
    for path in sorted(samples.rglob('*')):
        if not path.is_file() or path.suffix == '.meta':
            continue
        directories = path.relative_to(samples).parts[:-1]
        suffix = path.suffix.lower()
        if any(part in OWNED_SOURCE_EXCLUDED - {'Resources'} for part in directories):
            reason = 'excluded-directory'
        elif '_configs' in directories or suffix == '.bytes' or suffix == '.json' and 'Jsons' in directories:
            reason = 'generated-data-or-metadata'
        elif suffix == '.asset' and re.search(r'(?m)^\s*m_EditorClassIdentifier:.*\bConfigRuntimeSO\s*$', path.read_text(encoding='utf-8')):
            reason = 'generated-runtime-configuration'
        elif suffix == '.md':
            # Sample 说明也是生命周期和接入边界的输入，不能遗漏新增 README 的语义漂移。
            sample_inputs.append(path)
            continue
        elif suffix in {'.asset', '.prefab', '.unity', '.asmdef'} or path.name == 'link.xml':
            sample_inputs.append(path)
            continue
        elif suffix in {'.xlsx', '.xls', '.csv'} and any(part in {'Excels', 'Tracks'} for part in directories):
            sample_inputs.append(path)
            continue
        elif 'Excels' in directories and 'Tables' in directories and path.name in {'tables.xml', 'luban.conf'}:
            sample_inputs.append(path)
            continue
        else:
            continue
        sample_exclusions.append({'source': path.relative_to(root).as_posix(), 'reason': reason})
    files += sample_inputs
    agents_index = framework / 'Agents/INDEX.md'
    if agents_index.is_file():
        files.append(agents_index)
    if discovery is not None:
        discovery.update({'samples': {'production': len(sample_inputs),
            'excludedCounts': {reason: sum(item['reason'] == reason for item in sample_exclusions)
                               for reason in ('generated-data-or-metadata', 'generated-runtime-configuration', 'excluded-directory')},
            'exclusions': sample_exclusions}})
    return sorted(set(files))


def _input_record(path: Path, root: Path) -> dict:
    """记录输入指纹及导航类别，不推断文档与函数之间的语义对应关系。"""
    source = path.relative_to(root).as_posix()
    suffix = path.suffix.lower()
    if suffix == '.md':
        kind = 'documentation'
    elif path.name == 'google-services.json' and '/FirebaseDemo/Plugins/Android/' in source:
        kind = 'configuration'
    elif path.name == 'nova-samples.json':
        kind = 'sample-descriptor'
    elif suffix in {'.xlsx', '.xls', '.csv', '.json'} and path.name != 'package.json':
        kind = 'table'
    else:
        kind = {'.cs': 'source-declaration', '.asset': 'configuration', '.prefab': 'prefab', '.unity': 'scene',
                '.asmdef': 'assembly', '.rb': 'script', '.proto': 'protocol', '.sbn': 'template',
                '.java': 'native-source', '.swift': 'native-source', '.mm': 'native-source', '.jslib': 'native-source',
                '.pro': 'shrinker', '.gradle': 'build-configuration', '.conf': 'generator-configuration',
                '.plist': 'configuration'}.get(suffix, 'package' if path.name == 'package.json' else 'linker')
        if suffix == '.xml' and path.name != 'link.xml':
            kind = 'dependency' if path.name.endswith('Dependencies.xml') else 'manifest' if path.name.endswith('Manifest.xml') else 'schema'
    navigation = source.replace('/Docs/', '/Scripts/').replace('/Doc/', '/Scripts/').replace('/DOCS/', '/Scripts/')
    family = family_for(navigation)
    if family == 'framework.support':
        family = 'framework.inputs'
    record = {'source': source, 'sourceHash': hashlib.sha256(path.read_bytes()).hexdigest(),
              'family': family, 'kind': kind}
    if kind == 'native-source':
        record.update({'language': {'.java': 'Java', '.swift': 'Swift', '.mm': 'Objective-C++',
                                   '.jslib': 'JavaScript/Emscripten'}[suffix],
                       'reviewBoundary': 'source-declaration-and-csharp-binding; no-native-function-ast'})
    return record


def _compact(value: str) -> str:
    """仅压缩身份中的空白，不改正文。"""
    return re.sub(r'\s+', ' ', value).strip()


def _python_parameters(arguments: ast.arguments) -> list[dict]:
    """按声明顺序记录全部 Python 参数、注解、默认值和调用位置约束。"""
    records = []
    positional = arguments.posonlyargs + arguments.args
    default_offset = len(positional) - len(arguments.defaults)

    def add(parameter: ast.arg, default: ast.expr | None, modifiers: list[str]) -> None:
        """把单个参数变成通用声明记录，默认值只转为源码表达式而不求值。"""
        records.append({
            'name': parameter.arg,
            'type': ast.unparse(parameter.annotation) if parameter.annotation else '',
            'default': ast.unparse(default) if default is not None else None,
            'modifiers': modifiers,
        })

    for index, parameter in enumerate(positional):
        default = arguments.defaults[index - default_offset] if index >= default_offset else None
        add(parameter, default, ['positional-only'] if index < len(arguments.posonlyargs) else [])
    if arguments.vararg:
        add(arguments.vararg, None, ['var-positional'])
    for parameter, default in zip(arguments.kwonlyargs, arguments.kw_defaults):
        add(parameter, default, ['keyword-only'])
    if arguments.kwarg:
        add(arguments.kwarg, None, ['var-keyword'])
    return records


def _python_signature(text: str, node: ast.FunctionDef | ast.AsyncFunctionDef) -> str:
    """取完整声明头；多行参数和注解不能被第一行截断。"""
    # AST 列偏移以 UTF-8 字节计数，不能直接切 Python 的 Unicode 字符索引。
    encoded = text.encode('utf-8')
    lines = encoded.splitlines(keepends=True)
    start = sum(len(line) for line in lines[:node.lineno - 1]) + node.col_offset
    first_body = node.body[0]
    end = sum(len(line) for line in lines[:first_body.lineno - 1]) + first_body.col_offset
    return _compact(encoded[start:end].decode('utf-8'))


def index_python(path: Path, root: Path) -> list[dict]:
    """用标准库 AST 索引已有 Python 固定操作；无代码执行。"""
    source = path.relative_to(root).as_posix()
    text = path.read_text(encoding='utf-8')
    lines = text.splitlines(keepends=True)
    tree = ast.parse(text)
    records = []
    def visit(node, owners):
        """递归记录函数和类归属，局部函数继续沿既有身份结构保存。"""
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            qualified = '.'.join(owners+[node.name])
            parameters = _python_parameters(node.args)
            segment = ''.join(lines[node.lineno-1:node.end_lineno])
            records.append({'id':source+'#'+qualified+'('+','.join(p['name'] for p in parameters)+')',
                'source':source,'line':node.lineno,'endLine':node.end_lineno,'kind':'python_function',
                'namespace':'','type':'.'.join(owners),'name':node.name,'signature':_python_signature(text,node),
                'parameters':parameters,'returns':ast.unparse(node.returns) if node.returns else '',
                'visibility':'private' if node.name.startswith('_') else 'public','attributes':[],
                'sourceHash':hashlib.sha256(segment.encode()).hexdigest(),'xml':ast.get_docstring(node) or '',
                'calls':sorted(set(ast.unparse(n.func) for n in ast.walk(node) if isinstance(n,ast.Call))),
                'throws':[], 'family':family_for(source)})
            owners = owners+[node.name]
        elif isinstance(node, ast.ClassDef):
            owners = owners+[node.name]
        for child in ast.iter_child_nodes(node):
            visit(child, owners)
    visit(tree,[])
    return records


def _stop_ast_process(process: subprocess.Popen) -> None:
    """只终止本次新建的解析进程树；不按进程名称清理其它 SDK 或编译服务。"""
    if os.name == 'nt':
        # Windows 未经真机验证；精确 PID 树终止不能替换为全局 dotnet/task 名称匹配。
        try:
            subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                           capture_output=True, timeout=5, check=False)
        except (OSError, subprocess.TimeoutExpired):
            pass
        if process.poll() is None:
            process.kill()
    else:
        # 每次启动都创建新 session，组号即本次 PID，包含其未脱离组的子进程。
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    process.communicate(timeout=5)


def _run_ast_process(command: list[str], *, deadline: float, cwd: Path | None = None,
                     input: str | None = None, check: bool = False) -> subprocess.CompletedProcess:
    """共享单调时钟总期限运行一个解析阶段；超时或中断先收掉本次进程树再抛错。"""
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise RuntimeError('AST 子进程共享总期限已耗尽，未启动下一阶段。')
    isolation = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
    process = subprocess.Popen(command, cwd=cwd, stdin=subprocess.PIPE if input is not None else None,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, **isolation)
    try:
        stdout, stderr = process.communicate(input, timeout=max(0, deadline - time.monotonic()))
    except subprocess.TimeoutExpired as error:
        _stop_ast_process(process)
        # 维持 capability CLI 已有 RuntimeError -> JSON 失败通道，不能只留下 traceback。
        raise RuntimeError('AST 子进程在共享总期限内未完成：' + command[0]) from error
    except BaseException:
        _stop_ast_process(process)
        raise
    result = subprocess.CompletedProcess(command, process.returncode, stdout, stderr)
    if check:
        result.check_returncode()
    return result


def build_index(root: Path) -> dict:
    """完整读取当前源；Dotnet 各阶段共享 120 秒期限，调用方决定是否写正式产物。"""
    import tempfile
    import shutil
    deadline = time.monotonic() + _AST_TIMEOUT_SECONDS
    source_discovery = {}
    input_discovery = {}
    paths = source_files(root, source_discovery)
    cs_paths = [p for p in paths if p.suffix == '.cs']
    sdk = _run_ast_process(['dotnet','--version'], deadline=deadline, check=True).stdout.strip()
    target = 'net' + sdk.split('.')[0] + '.0'
    with tempfile.TemporaryDirectory(prefix='nova-function-ast-') as temp:
        build = Path(temp)
        for name,target_name in (('nova_ast.csproj.txt','nova_ast.csproj'),('nova_ast.cs.txt','nova_ast.cs.txt')):
            shutil.copyfile(Path(__file__).with_name(name),build/target_name)
        command = ['dotnet','build',str(build/'nova_ast.csproj'),'-o',str(build/'out'),
                   '--nologo','-v:q','--disable-build-servers','-p:TargetFramework='+target]
        result = _run_ast_process(command, deadline=deadline)
        if result.returncode:
            raise RuntimeError('C# AST 编译器工具构建失败：'+result.stdout+result.stderr)
        result = _run_ast_process(['dotnet',str(build/'out/nova_ast.dll')], deadline=deadline, cwd=root,
            input=json.dumps([p.relative_to(root).as_posix() for p in cs_paths]), check=True)
        index = json.loads(result.stdout)
    functions = index['functions']
    for path in paths:
        if path.suffix == '.py':
            functions.extend(index_python(path,root))
    file_hashes={p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
    for record in functions + index['fields']:
        record['family'] = family_for(record['source'])
        record['sourceFileHash'] = file_hashes[record['source']]
    # 条件编译替代实现同签名时，以分支正文指纹区分，不吞掉另一个实现。
    from collections import Counter
    duplicates = Counter(f['id'] for f in functions)
    duplicate_bodies = Counter((f['id'], f['sourceHash']) for f in functions)
    for f in functions:
        if duplicates[f['id']] > 1:
            fingerprint = f.get('variantHash', f['sourceHash']) if duplicate_bodies[(f['id'], f['sourceHash'])] > 1 else f['sourceHash']
            f['id'] += '@' + fingerprint[:12]
    files = [{'source':p.relative_to(root).as_posix(),'sourceHash':hashlib.sha256(p.read_bytes()).hexdigest(),
              'family':family_for(p.relative_to(root).as_posix())} for p in paths]
    inputs = [_input_record(path, root) for path in input_files(root, input_discovery)]
    return {'functions':functions,'fields':index['fields'],'files':files,'inputs':inputs,'parseIssues':index['parseIssues'],
            'parser':{'engine':'Microsoft.CodeAnalysis.CSharp','sdk':sdk,'conditionalMode':'all-branches-or-exhaustive-small-symbols-and-guard-coverage'},
            'exclusions':sorted(EXCLUDED), 'discovery': {'sources': source_discovery, 'inputs': input_discovery}}


def build_navigation(root: Path, output: Path | None = None) -> dict:
    """显式解析当前源并写轻量派生快照；开发缓存不进入 Git，发布副本由发布器生成。"""
    import nova_api_index
    result = build_index(root)
    if result.get('parseIssues'):
        raise ValueError('AST 解析尚有问题，未更新快照：' + str(result['parseIssues']))
    coverage = json.loads((root/'Assets/Framework/Agents/coverage.json').read_text(encoding='utf-8'))
    destination = output or root/'Library/Nova/AgentIndex/api-index.jsonl'
    counts = nova_api_index.write_snapshot(result, destination, coverage,
        docs_resolver=lambda record: nova_api_index.resolve_doc_refs(record, root, coverage))
    return {'valid': True, 'snapshot': str(destination), 'summary': counts,
            'semanticAuthority': 'source-xml-docs-contract', 'semanticReview': 'not-performed'}


def main() -> int:
    """显式构建轻量导航；完整 AST 只作本次内存输入，不保存第二份知识或大索引。"""
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--root',type=Path,default=Path.cwd())
    ap.add_argument('--write',type=Path, help='显式输出 api-index.jsonl 文件；默认项目 Library/Nova/AgentIndex/')
    args=ap.parse_args()
    result = build_navigation(args.root.resolve(), args.write)
    print(json.dumps(result, ensure_ascii=False))
    return 0
if __name__=='__main__':
    raise SystemExit(main())
