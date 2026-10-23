"""用真实 Roslyn 和 Python AST 验证声明覆盖，不执行被索引的业务代码。"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import nova_function_index as index


class PythonDeclarations(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='nova-python-index-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.path = self.root / 'Assets/Framework/Scripts/example.py'
        self.path.parent.mkdir(parents=True)

    def parse(self, source):
        self.path.write_text(source, encoding='utf-8')
        return index.index_python(self.path, self.root)

    def test_all_parameter_kinds_annotations_and_defaults(self):
        node = self.parse('def f(a: int = 3, /, b: str = "x", *args, c: bool = False, **kwargs) -> int:\n    return a\n')[0]
        self.assertEqual(node['parameters'], [
            {'name': 'a', 'type': 'int', 'default': '3', 'modifiers': ['positional-only']},
            {'name': 'b', 'type': 'str', 'default': "'x'", 'modifiers': []},
            {'name': 'args', 'type': '', 'default': None, 'modifiers': ['var-positional']},
            {'name': 'c', 'type': 'bool', 'default': 'False', 'modifiers': ['keyword-only']},
            {'name': 'kwargs', 'type': '', 'default': None, 'modifiers': ['var-keyword']},
        ])
        self.assertEqual(node['returns'], 'int')
        self.assertTrue(node['signature'].endswith('-> int:'))

    def test_multiline_async_unicode_and_nested_functions(self):
        nodes = self.parse('@marker\nasync def 处理(\n    参数: str = "值",\n    *, 必填: int,\n) -> str:\n    def 内部(): return 参数\n    return 内部()\n')
        outer, inner = nodes
        self.assertEqual(outer['signature'], 'async def 处理( 参数: str = "值", *, 必填: int, ) -> str:')
        self.assertEqual(outer['parameters'][1], {'name': '必填', 'type': 'int', 'default': None, 'modifiers': ['keyword-only']})
        self.assertEqual(inner['signature'], 'def 内部():')
        self.assertTrue(inner['id'].endswith('#处理.内部()'))
        self.assertEqual(outer['calls'], ['内部'])

    def test_ordinary_argument_ids_remain_compatible(self):
        nodes = self.parse('def parse_args(): pass\ndef convert_aab_to_apks(args): pass\ndef install_apks_to_device(args): pass\ndef install_apk(apk_path, adb_path): pass\ndef main(): pass\n')
        self.assertEqual([n['id'].split('#')[1] for n in nodes], [
            'parse_args()', 'convert_aab_to_apks(args)', 'install_apks_to_device(args)',
            'install_apk(apk_path,adb_path)', 'main()',
        ])


class PythonToolDiscovery(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='nova-python-discovery-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def add_file(self, source, content='def run(value=3): return value\n'):
        path = self.root / source
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8')
        return path

    def test_production_python_owners_are_discovered_and_indexed_without_execution(self):
        """按真实 owner 发现生产代码，保留稳定函数身份；自有工具不能递归执行自身。"""
        tools = self.add_file('Assets/Framework/Agents/Tools/nova_function_index.py')
        self.add_file('.agents/skills/nova-example/SKILL.md', 'name: nova-example\n')
        skill = self.add_file('.agents/skills/nova-example/scripts/run.py')
        helper = self.add_file('.agents/skills/nova-example/scripts/helpers/parse.py')
        shared = self.add_file('.agents/skills/_shared/public_exclude.py')
        self.add_file('Assets/Framework/Agents/Skills/nova-project-example/SKILL.md', 'name: nova-project-example\n')
        consumer = self.add_file('Assets/Framework/Agents/Skills/nova-project-example/scripts/run.py')
        expected = {tools, skill, helper, shared, consumer}
        self.assertEqual(set(index.source_files(self.root)), expected)
        families = {tools: 'framework.agents.tools', skill: 'development.skill.nova-example',
                    helper: 'development.skill.nova-example', shared: 'development.shared',
                    consumer: 'framework.agents.tools'}
        for path in expected:
            node = index.index_python(path, self.root)[0]
            self.assertEqual(node['family'], families[path])
            self.assertEqual(node['id'], path.relative_to(self.root).as_posix() + '#run(value)')
            self.assertEqual(node['parameters'][0]['default'], '3')
        self.assertEqual(index.input_files(self.root), [])

    def test_sample_docs_and_agents_navigation_are_context_inputs(self):
        """Sample 用法与随包导航应触发语义漂移；投影、厂商和审查流水账仍排除。"""
        sample = self.add_file('Assets/Samples/Example/README.md', '# 实际入口\n')
        agents = self.add_file('Assets/Framework/Agents/INDEX.md', '# 能力导航\n')
        self.add_file('Assets/Samples/Example/Vendor/README.md')
        self.add_file('.agents/skills/nova-project-example/README.md')
        self.add_file('Assets/Framework/Docs/Onboarding/Reviews/README.md')
        self.assertEqual(set(index.input_files(self.root)), {sample, agents})
        self.assertEqual(index._input_record(sample, self.root)['family'], 'sample.Example')
        self.assertEqual(index._input_record(agents, self.root)['family'], 'framework.agents.tools')

    def test_tests_templates_vendors_and_skill_projections_remain_excluded(self):
        """消费投影、测试和非生产副本不能因 Python 后缀进入功能源码或输入账本。"""
        for owner in ['nova-example', 'nova-project-example', 'other-example']:
            self.add_file('.agents/skills/' + owner + '/SKILL.md', 'name: ' + owner + '\n')
        for source in [
            'Assets/Framework/Agents/Tools/tests/test_example.py',
            'Assets/Framework/Agents/Tools/test_example.py',
            'Assets/Framework/Agents/Tools/helpers/example.py',
            'Assets/Framework/Agents/Skills/nova-project-example/scripts/example.py',
            'Assets/Framework/Agents/Reviews/example.py',
            '.agents/skills/nova-project-example/scripts/example.py',
            '.agents/skills/other-example/scripts/example.py',
            '.agents/skills/nova-missing/scripts/example.py',
            '.agents/skills/nova-example/tests/example.py',
            '.agents/skills/nova-example/scripts/test_example.py',
            '.agents/skills/nova-example/scripts/tests/example.py',
            '.agents/skills/nova-example/scripts/Tests/example.py',
            '.agents/skills/nova-example/scripts/__pycache__/example.py',
            '.agents/skills/nova-example/scripts/Generated/example.py',
            '.agents/skills/nova-example/scripts/Vendor/example.py',
            '.agents/skills/nova-example/scripts/ThirdParty/example.py',
            '.agents/skills/nova-example/scripts/templates/example.py',
            '.agents/skills/nova-example/scripts/fixtures/example.py',
            '.agents/skills/nova-example/scripts/node_modules/example.py',
            '.agents/skills/nova-example/scripts/.venv/example.py',
            '.agents/skills/_shared/tests/example.py',
            '.agents/skills/_shared/Generated/example.py',
        ]:
            self.add_file(source)
        self.assertEqual(index.source_files(self.root), [])
        self.assertEqual(index.input_files(self.root), [])


class InputDiscovery(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='nova-input-index-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def add_file(self, source, content='example'):
        path = self.root / source
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8')
        return path

    def test_owned_docs_tables_configs_and_dependency_inputs(self):
        package = 'UPMPackages/com.solotopia.nova.framework.sdk.example/'
        expected = [
            'Assets/Framework/Docs/Runtime/Modules/UI/UI.md',
            'Assets/Framework/Templates/Table.xlsx', 'Assets/Framework/Tracks/Tracks.csv',
            'Assets/Framework/Templates/Rules.json', 'Assets/Framework/Resources/Debug/Settings.asset',
            'Assets/Framework/Prefabs/Nova.prefab', 'Assets/Framework/Scenes/Entry.unity',
            'Assets/Framework/Scripts/Runtime/NovaFramework.Runtime.asmdef',
            'Assets/Framework/Scripts/Runtime/link.xml', 'Assets/Framework/package.json',
            package + 'Nova/Doc/Plugin.md', package + 'Nova/Tracks/Tracks.xls',
            package + 'Nova/Config/Config.asset', package + 'Nova/Scripts/Plugin.asmdef',
            package + 'Docs/INDEX.md', package + 'Tracks/Tracks.xlsx', package + 'package.json',
        ]
        for source in expected:
            self.add_file(source)
        self.assertEqual({p.relative_to(self.root).as_posix() for p in index.input_files(self.root)}, set(expected))
        records = [index._input_record(path, self.root) for path in index.input_files(self.root)]
        ui = next(record for record in records if record['source'].endswith('/UI.md'))
        self.assertEqual((ui['family'], ui['kind']), ('runtime.UI', 'documentation'))
        self.assertEqual({r['kind'] for r in records}, {'documentation', 'table', 'configuration', 'prefab', 'scene', 'assembly', 'linker', 'package'})

    def test_generated_reviews_agents_and_vendor_files_are_excluded(self):
        for source in [
            'Assets/Framework/Agents/catalog.json',
            'Assets/Framework/Agents/Knowledge/runtime.UI.json',
            'Assets/Framework/Agents/Knowledge/INDEX.md',
            'Assets/Framework/Agents/Skills/example/Docs/README.md',
            'Assets/Framework/Docs/Onboarding/Reviews/Report.md',
            'Assets/Framework/Docs/Onboarding/Reviews/Report.json',
            'Assets/Framework/Minds/Docs/Note.md',
            'Assets/Framework/Scripts/Generated/Example.asmdef',
            'Assets/Framework/Vendor/Config.asset',
            'UPMPackages/com.solotopia.nova.framework.sdk.example/Core/Docs/Vendor.md',
            'UPMPackages/com.solotopia.nova.framework.sdk.example/Nova/Resources/Vendor/Config.asset',
            'UPMPackages/com.solotopia.vendor/package.json',
            'Assets/Business/Docs/Business.md',
        ]:
            self.add_file(source)
        self.assertEqual(index.input_files(self.root), [])

    def test_owned_external_assembly_is_discovered_without_vendor_scan(self):
        """只发现已登记的真实编译边界；相同名称的厂商文件不获得归属。"""
        package = 'UPMPackages/com.solotopia.nova.framework.sdk.wechat.minigame/'
        source = package + 'ThirdParty/GravityEngine-5.0.45/GravityEngine.WeChat.Runtime.asmdef'
        content = json.dumps({
            'name': 'NovaFramework.SDK.WeChatMiniGame.GravityEngine.Runtime',
            'includePlatforms': ['Editor', 'WebGL'],
        })
        owned = self.add_file(source, content)
        for vendor_source in [
            package + 'ThirdParty/GravityEngine-5.0.45/Vendor.asmdef',
            package + 'ThirdParty/GravityEngine-5.0.46/GravityEngine.WeChat.Runtime.asmdef',
            package + 'Core/GravityEngine.WeChat.Runtime.asmdef',
            package + 'ThirdParty/GravityEngine-5.0.45/Vendor.cs',
            'UPMPackages/com.solotopia.nova.framework.sdk.example/'
            'ThirdParty/GravityEngine-5.0.45/GravityEngine.WeChat.Runtime.asmdef',
        ]:
            self.add_file(vendor_source, content)
        self.assertEqual(index.input_files(self.root), [owned])
        self.assertEqual(index._input_record(owned, self.root), {
            'source': source,
            'sourceHash': hashlib.sha256(owned.read_bytes()).hexdigest(),
            'family': 'package.sdk.wechat.minigame',
            'kind': 'assembly',
        })
        self.assertEqual(index.source_files(self.root), [])
        owned.unlink()
        self.assertEqual(index.input_files(self.root), [])

    def test_owned_max_script_is_discovered_without_expanding_ruby_scan(self):
        """固定顺序脚本必须纳入指纹审查；同名、同包或厂商 Ruby 不自动获得归属。"""
        package = 'UPMPackages/com.solotopia.nova.framework.sdk.max/'
        relative = 'Nova/Scripts/Editor/BuildProcessor/MaxIOSPodOwnership.rb'
        source = package + relative
        owned = self.add_file(source, "puts 'before'\nexit(0)\n")
        for excluded in [
            package + 'Nova/Scripts/Editor/BuildProcessor/Other.rb',
            package + 'Nova/Scripts/Editor/Other/MaxIOSPodOwnership.rb',
            package + 'Nova/Vendor/MaxIOSPodOwnership.rb',
            package + 'Core/MaxIOSPodOwnership.rb',
            package + 'ThirdParty/MaxIOSPodOwnership.rb',
            'UPMPackages/com.solotopia.nova.framework.sdk.example/' + relative,
            'UPMPackages/com.solotopia.vendor/' + relative,
            'Assets/Framework/Scripts/Editor/MaxIOSPodOwnership.rb',
        ]:
            self.add_file(excluded, "exit(0)\n")
        self.assertEqual(index.input_files(self.root), [owned])
        first = index._input_record(owned, self.root)
        self.assertEqual(first, {
            'source': source,
            'sourceHash': hashlib.sha256(owned.read_bytes()).hexdigest(),
            'family': 'package.sdk.max',
            'kind': 'script',
        })
        self.assertEqual(index.source_files(self.root), [])
        owned.write_text("puts 'after'\nexit(0)\n", encoding='utf-8')
        updated = index._input_record(owned, self.root)
        self.assertNotEqual(first['sourceHash'], updated['sourceHash'])
        self.assertEqual(updated['sourceHash'], hashlib.sha256(owned.read_bytes()).hexdigest())
        owned.unlink()
        self.assertEqual(index.input_files(self.root), [])

    def test_input_snapshot_tracks_content_additions_and_deletions(self):
        original = self.add_file('Assets/Framework/Docs/INDEX.md', 'before')
        first = index._input_record(original, self.root)
        original.write_text('after', encoding='utf-8')
        second = index._input_record(original, self.root)
        self.assertNotEqual(first['sourceHash'], second['sourceHash'])
        added = self.add_file('Assets/Framework/Templates/Table.xlsx')
        self.assertEqual(set(index.input_files(self.root)), {original, added})
        original.unlink()
        self.assertEqual(index.input_files(self.root), [added])


class OwnedSourceDiscovery(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='nova-owned-discovery-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def add_file(self, source, content='class Example {}\n'):
        path = self.root / source
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8')
        return path

    def test_sdk_contracts_are_owned_without_expanding_vendor_plugins(self):
        """真实 SDK 接口不能被 Plugins 名称吞掉，其他厂商根和生成目录仍不纳入。"""
        sdk = 'Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/'
        own = self.add_file(sdk + 'Ad/IAdPlugin.cs')
        for source in [sdk + 'Vendor/Ad.cs', sdk + 'Generated/Ad.cs', sdk + 'Tests/Ad.cs',
                       'Assets/Framework/Scripts/Runtime/Modules/Other/Plugins/Vendor.cs',
                       'UPMPackages/com.solotopia.nova.framework.sdk.example/Nova/Plugins/Vendor.cs',
                       'UPMPackages/com.solotopia.vendor/Nova/Scripts/Vendor.cs']:
            self.add_file(source)
        discovery = {}
        self.assertEqual(index.source_files(self.root, discovery), [own])
        self.assertEqual(index.family_for(own.relative_to(self.root).as_posix()), 'runtime.SDK')
        self.assertEqual(discovery['sdkContractSources'], 1)

    def test_samples_preserve_handwritten_datatypes_and_copy_templates(self):
        """手写 DataTypes 与复制模板都是生产源；实际生成标记与测试具有可审计排除原因。"""
        base = 'Assets/Samples/ExampleDemo/'
        hand = self.add_file(base + 'Scripts/Runtime/DataTypes/Events/DemoPingEventData.cs')
        copy = self.add_file(base + 'Scripts/Editor/PathRewriter.cs')
        self.add_file(base + 'Scripts/Runtime/DataTypes/Tables/Generated.cs', '// <auto-generated>\nclass G {}\n')
        self.add_file(base + 'Scripts/Runtime/DataTypes/UIs/UI.cs', '// <nova-generated profile="ui" />\nclass U {}\n')
        self.add_file(base + 'Tests/Editor/ExampleTests.cs')
        self.add_file(base + 'Scripts/Editor/Tests/OtherTests.cs')
        self.add_file(base + 'Vendor/Vendor.cs')
        bootstrap = self.add_file('NovaSpark.cs')
        discovery = {}
        self.assertEqual(set(index.source_files(self.root, discovery)), {hand, copy, bootstrap})
        self.assertEqual({index.family_for(path.relative_to(self.root).as_posix()) for path in (hand, copy)}, {'sample.ExampleDemo'})
        self.assertEqual(index.family_for('NovaSpark.cs'), 'development.bootstrap')
        samples = discovery['samples']
        self.assertEqual((samples['candidates'], samples['production']), (7, 2))
        self.assertEqual(samples['excludedCounts'], {'generated': 2, 'test': 2, 'excluded-directory': 1})
        self.assertEqual(len(samples['exclusions']), 5)

    def test_authoritative_producers_and_native_inputs_do_not_expand_vendor_scan(self):
        """按真实自有生产者发现 proto/template，native只作带语言与边界的输入。"""
        protocol = self.add_file('Assets/Framework/Protos/header.proto', 'syntax = "proto3";\n')
        package_protocol = self.add_file('UPMPackages/com.solotopia.nova.framework.sdk.example/Nova/Protos/payment.proto')
        template = self.add_file('Assets/Framework/Templates/Luban/table/cs-bin/table.sbn')
        native_source = index.OWNED_NATIVE_INPUTS[0]
        native = self.add_file(native_source, 'class NovaNativeReviewBridge {}\n')
        manifest = self.add_file(index.OWNED_BUILD_INPUTS[0], '<manifest/>')
        for source in [
            'Assets/Framework/Protos/Vendor/vendor.proto',
            'Assets/Framework/Templates/Luban/ThirdParty/vendor.sbn',
            'Assets/Framework/Templates/Other/template.sbn',
            'UPMPackages/com.solotopia.nova.framework.sdk.example/Core/Protos/vendor.proto',
            'UPMPackages/com.solotopia.nova.framework.sdk.example/Nova/ThirdParty/Protos/vendor.proto',
            'UPMPackages/com.solotopia.vendor/Nova/Protos/vendor.proto',
            'Assets/Framework/Plugins/Android/OtherBridge.java',
            'Assets/Framework/Plugins/Android/OtherManifest.xml',
        ]:
            self.add_file(source)
        self.assertEqual(set(index.input_files(self.root)), {protocol, package_protocol, template, native, manifest})
        record = index._input_record(native, self.root)
        self.assertEqual((record['family'], record['kind'], record['language']), ('runtime.Native', 'native-source', 'Java'))
        self.assertEqual(record['sourceHash'], hashlib.sha256(native.read_bytes()).hexdigest())
        self.assertEqual(record['reviewBoundary'], 'source-declaration-and-csharp-binding; no-native-function-ast')
        self.assertEqual(index._input_record(protocol, self.root)['family'], 'runtime.Network')
        self.assertEqual(index._input_record(template, self.root)['kind'], 'template')
        self.assertEqual(index._input_record(manifest, self.root)['kind'], 'manifest')
        self.assertEqual(index.source_files(self.root), [])

    def test_sample_inputs_use_authoritative_assets_tables_and_descriptors(self):
        """保留配置/绑定/Excel/手工schema，导出物和生成metadata不获得独立权威输入身份。"""
        base = 'Assets/Samples/ExampleDemo/'
        expected = {self.add_file(base + relative) for relative in [
            'Editor/ConfigMaster.asset', 'Prefabs/View.prefab', 'Entry.unity',
            'Scripts/Runtime/Example.asmdef', 'Editor/link.xml', 'Excels/Tables/Items.xlsx',
            'Excels/Tracks/Tracks.csv', 'Excels/Tables/tables.xml', 'Excels/Tables/luban.conf']}
        descriptor = self.add_file('UPMPackages/com.solotopia.nova.framework.sdk.example/nova-samples.json', '{"samples":[]}')
        expected.add(descriptor)
        for relative in ['Excels/UIs/_configs/nova-export-manifest.json', 'Excels/UIs/_configs/__tables__.xml',
                         'Excels/UIs/_configs/luban.conf', 'Excels/UIs/_configs/output-manifests/ui.json',
                         'Jsons/UIs.json', 'Bytes/Tables.bytes', 'Resources/BuiltIn/Jsons/Texts.json']:
            self.add_file(base + relative)
        self.add_file(base + 'Configs/RenamedExport.asset', 'm_EditorClassIdentifier: NovaFramework.Runtime::NovaFramework.Runtime.ConfigRuntimeSO\n')
        self.add_file(base + 'Tests/Config.asset')
        self.add_file(base + 'Vendor/Config.asset')
        self.add_file(base + 'Other.json')
        self.add_file(base + 'Other.xlsx')
        discovery = {}
        self.assertEqual(set(index.input_files(self.root, discovery)), expected)
        self.assertEqual(index._input_record(descriptor, self.root)['kind'], 'sample-descriptor')
        for path in expected - {descriptor}:
            self.assertEqual(index._input_record(path, self.root)['family'], 'sample.ExampleDemo')
        self.assertEqual(discovery['samples']['excludedCounts'], {
            'generated-data-or-metadata': 7, 'generated-runtime-configuration': 1, 'excluded-directory': 2})
        self.assertEqual(discovery['samples']['production'], 9)


class AstProcessCleanup(unittest.TestCase):
    @unittest.skipUnless(os.name == 'posix', '此用例验证 POSIX session 与进程组；Windows 未实机验证')
    def test_timeout_kills_only_owned_parent_and_child_and_cleans_build_directory(self):
        """真实父子进程超时后临时编译目录自清，旁路独立进程不被终止。"""
        with tempfile.TemporaryDirectory(prefix='nova-ast-timeout-test-') as temporary:
            root = Path(temporary)
            marker = root / 'owned-processes.json'
            original_popen = subprocess.Popen
            observed_commands = []
            owned_processes = []
            unrelated = original_popen([sys.executable, '-B', '-c', 'import time; time.sleep(60)'], start_new_session=True)
            build_directory = None

            def launch_fixture(command, **kwargs):
                """将本次 Dotnet 命令替换为真实 Python 进程，保留被测 session 与管道设置。"""
                nonlocal build_directory
                observed_commands.append(command)
                if command == ['dotnet', '--version']:
                    replacement = [sys.executable, '-B', '-c', 'print("10.0.0")']
                else:
                    build_directory = Path(command[2]).parent
                    code = ('import json,os,pathlib,subprocess,sys,time; '
                            'child=subprocess.Popen([sys.executable,"-B","-c","import time; time.sleep(60)"]); '
                            'pathlib.Path(sys.argv[1]).write_text(json.dumps([os.getpid(),child.pid])); '
                            'time.sleep(60)')
                    replacement = [sys.executable, '-B', '-c', code, str(marker)]
                process = original_popen(replacement, **kwargs)
                owned_processes.append(process)
                return process

            try:
                with patch.object(index, '_AST_TIMEOUT_SECONDS', 2.0), \
                        patch.object(index, '_run_ast_process', wraps=index._run_ast_process) as stages, \
                        patch.object(subprocess, 'Popen', side_effect=launch_fixture):
                    with self.assertRaisesRegex(RuntimeError, 'AST 子进程在共享总期限内未完成'):
                        index.build_index(root)
                self.assertEqual(2, len(observed_commands))
                self.assertEqual(stages.call_args_list[0].kwargs['deadline'], stages.call_args_list[1].kwargs['deadline'])
                self.assertIn('--disable-build-servers', observed_commands[1])
                self.assertIsNotNone(build_directory)
                self.assertFalse(build_directory.exists())
                self.assertTrue(marker.is_file(), '超时前必须确实启动真实子进程')
                owned = json.loads(marker.read_text())
                for pid in owned:
                    # 某些宿主的 init 延迟回收孤儿僵尸；僵尸已终止，不再持有管道或执行代码。
                    result = subprocess.run(['ps', '-o', 'stat=', '-p', str(pid)], capture_output=True, text=True, check=False)
                    self.assertTrue(not result.stdout.strip() or result.stdout.lstrip().startswith('Z'), (pid, result.stdout))
                self.assertIsNone(unrelated.poll(), '不得终止其它 session 的进程')
            finally:
                for process in owned_processes:
                    try:
                        os.killpg(process.pid, 9)
                    except ProcessLookupError:
                        pass
                    process.communicate(timeout=5)
                unrelated.kill()
                unrelated.wait(timeout=5)


class RoslynDeclarations(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix='nova-roslyn-index-test-')
        cls.addClassCleanup(cls.temp.cleanup)
        cls.build = Path(cls.temp.name)
        sdk = subprocess.run(['dotnet', '--version'], capture_output=True, text=True, check=True).stdout.strip()
        for source, target in [('nova_ast.cs.txt', 'nova_ast.cs.txt'), ('nova_ast.csproj.txt', 'nova_ast.csproj')]:
            shutil.copyfile(TOOLS / source, cls.build / target)
        result = subprocess.run(['dotnet', 'build', str(cls.build / 'nova_ast.csproj'), '-o', str(cls.build / 'out'),
                                 '--nologo', '-v:q', '-p:TargetFramework=net' + sdk.split('.')[0] + '.0'],
                                capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)

    def parse(self, source):
        with tempfile.TemporaryDirectory(prefix='nova-csharp-fixture-') as temp:
            root = Path(temp)
            path = root / 'Example.cs'
            path.write_text(source, encoding='utf-8')
            result = subprocess.run(['dotnet', str(self.build / 'out/nova_ast.dll')], cwd=root,
                                    input=json.dumps(['Example.cs']), capture_output=True, text=True, check=True)
            return json.loads(result.stdout)

    def test_combined_conditional_inheritance_covers_both_implementations(self):
        result = self.parse('''public class Example
#if A && B && !C
 : BaseA
#else
 : BaseB
#endif
{
#if A && B && !C
 public void IncludedOnlyWhenAB() {}
#else
 public void Fallback() {}
#endif
}
public class BaseA {}
public class BaseB {}
''')
        self.assertEqual({n['name'] for n in result['functions']}, {'IncludedOnlyWhenAB', 'Fallback'})
        self.assertEqual(result['parseIssues'], [])

    def test_large_guard_nested_elif_and_else_are_proved(self):
        condition = ' && '.join([f'S{i}' for i in range(10)] + ['!S10'])
        result = self.parse(f'''class Example
#if {condition}
: First
#elif S0 == S1 && S2 != S3
: Second
#else
: Third
#endif
{{
#if {condition}
 void FirstBranch() {{}}
 #if NESTED
 void NestedTrue() {{}}
 #else
 void NestedFalse() {{}}
 #endif
#elif S0 == S1 && S2 != S3
 void SecondBranch() {{}}
#else
 void ThirdBranch() {{}}
#endif
}}
''')
        self.assertEqual({n['name'] for n in result['functions']}, {'FirstBranch', 'NestedTrue', 'NestedFalse', 'SecondBranch', 'ThirdBranch'})
        self.assertEqual(result['parseIssues'], [])

    def test_large_local_symbol_mutation_reports_unproven_coverage(self):
        condition = ' && '.join(f'S{i}' for i in range(11))
        result = self.parse(f'''#define S0
class Example
#if {condition}
: First
#else
: Second
#endif
{{ void Run() {{}} }}
''')
        self.assertTrue(any('define/undef' in issue['message'] for issue in result['parseIssues']))

    def test_unreachable_branch_does_not_hide_reachable_methods(self):
        result = self.parse('''class Example
#if A && !A
: First
#else
: Second
#endif
{
#if A && !A
 void Unreachable() {}
#else
 void Reachable() {}
#endif
}
''')
        self.assertEqual([n['name'] for n in result['functions']], ['Reachable'])
        self.assertEqual(result['parseIssues'], [])

    def test_generic_owners_explicit_interfaces_indexers_and_unsafe_signatures(self):
        result = self.parse('''namespace Tests;
interface IContract<T> { void Run(T value); }
public unsafe class Outer<T> {
 public class Inner<U> : IContract<U> {
  void IContract<U>.Run(U value) {}
  [Marker] public int this[int key] { get => key; set { Use(value); } }
  public int* Convert(ref int value, int count = 3) => null;
  public void Owner() { int Nested(string text) => text.Length; }
 }
}
''')
        self.assertEqual(result['parseIssues'], [])
        nodes = {n['name']: n for n in result['functions'] if n['type'] == 'Outer<T>.Inner<U>'}
        self.assertTrue(nodes['IContract<U>.Run']['id'].endswith('#Tests.Outer<T>.Inner<U>.IContract<U>.Run(U)'))
        getter = nodes['this[int key].get']
        setter = nodes['this[int key].set']
        self.assertEqual(getter['parameters'], [{'name': 'key', 'type': 'int', 'default': None, 'modifiers': []}])
        self.assertEqual(setter['parameters'][-1], {'name': 'value', 'type': 'int', 'default': None, 'modifiers': ['implicit']})
        self.assertEqual((getter['returns'], setter['returns']), ('int', 'void'))
        self.assertEqual(setter['attributes'], ['[Marker]'])
        self.assertEqual(nodes['Convert']['returns'], 'int*')
        self.assertEqual(nodes['Convert']['parameters'][0]['modifiers'], ['ref'])
        self.assertTrue(nodes['Convert']['id'].endswith('Convert(ref int,int)'))
        self.assertTrue(nodes['Nested']['id'].endswith('.Owner.Nested(string)'))

    def test_expression_accessor_signature_and_explicit_property_identity(self):
        result = self.parse('''class Example {
 public bool IsValid => true;
 public int this[int key] => key;
 int IOne.Value { get => 1; set {} }
 int ITwo.Value { get => 2; set {} }
 event Handler IOne.Changed { add {} remove {} }
 event Handler ITwo.Changed { add {} remove {} }
}
''')
        self.assertEqual(result['parseIssues'], [])
        nodes = {n['name']: n for n in result['functions']}
        self.assertEqual(nodes['IsValid.get']['signature'], 'public bool IsValid')
        self.assertEqual(nodes['this[int key].get']['signature'], 'public int this[int key]')
        self.assertEqual(nodes['IOne.Value.set']['signature'], 'int IOne.Value set')
        self.assertEqual(len({n['id'] for n in result['functions']}), len(result['functions']))
        self.assertEqual(nodes['ITwo.Changed.add']['parameters'][0]['type'], 'Handler')

    def test_conditional_signature_variants_are_not_merged_or_deduplicated(self):
        result = self.parse('''class Example {
 public
#if A
 int
#else
 string
#endif
 Run(
#if B
 int value,
#else
 string value,
#endif
 bool enabled) { return default; }
}
''')
        self.assertEqual(result['parseIssues'], [])
        nodes = result['functions']
        self.assertEqual({(n['returns'], n['parameters'][0]['type']) for n in nodes},
                         {('int', 'int'), ('int', 'string'), ('string', 'int'), ('string', 'string')})
        self.assertTrue(all(len(n['parameters']) == 2 for n in nodes))
        self.assertEqual(len({n['variantHash'] for n in nodes}), 4)

    def test_conditional_field_type_keeps_both_declarations(self):
        result = self.parse('''class Example {
#if A
 int
#else
 string
#endif
 Value;
}
''')
        self.assertEqual(result['parseIssues'], [])
        self.assertEqual({n['type'] for n in result['fields']}, {'int', 'string'})

    def test_real_build_index_exposes_inputs_and_full_file_hashes(self):
        with tempfile.TemporaryDirectory(prefix='nova-build-index-test-') as temp:
            root = Path(temp)
            source = root / 'Assets/Framework/Scripts/Runtime/Modules/Example/Example.cs'
            source.parent.mkdir(parents=True)
            source.write_text('''class Example {
 public void Run() {}
 int Count = 3;
 public
#if A
 int
#else
 string
#endif
 Variant() => default;
}
''', encoding='utf-8')
            doc = root / 'Assets/Framework/Docs/Runtime/Modules/Example/Example.md'
            doc.parent.mkdir(parents=True)
            doc.write_text('Example contract', encoding='utf-8')
            result = index.build_index(root)
            self.assertEqual(result['parseIssues'], [])
            file_hash = hashlib.sha256(source.read_bytes()).hexdigest()
            self.assertEqual(result['functions'][0]['sourceFileHash'], file_hash)
            self.assertEqual(result['fields'][0]['sourceFileHash'], file_hash)
            variants = [node for node in result['functions'] if node['name'] == 'Variant']
            self.assertEqual(len(variants), 2)
            self.assertEqual(len({node['id'] for node in variants}), 2)
            self.assertEqual(len({node['sourceHash'] for node in variants}), 1)
            self.assertEqual(result['inputs'], [{
                'source': doc.relative_to(root).as_posix(),
                'sourceHash': hashlib.sha256(doc.read_bytes()).hexdigest(),
                'family': 'runtime.Example', 'kind': 'documentation',
            }])


if __name__ == '__main__':
    unittest.main()
