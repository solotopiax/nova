"""在隔离目录验证白名单快照、跨进程缓存及无编译器消费，不启动 Unity。"""
import copy
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import nova_api_index as api


class ApiIndexTests(unittest.TestCase):
    def setUp(self):
        """建立不依赖当前仓库源码及 Dotnet 的独立快照夹具。"""
        self.temp = tempfile.TemporaryDirectory(prefix='nova-api-index-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.agents = self.root / 'Packages/com.solotopia.nova.framework/Agents'
        self.agents.mkdir(parents=True)
        self.snapshot = self.agents / 'api-index.jsonl'
        self.source = 'Assets/Framework/Scripts/Example.cs'
        self.digest = hashlib.sha256(b'example declaration').hexdigest()
        self.function = {'id': self.source + '#Nova.Example.Run(int)', 'source': self.source,
            'line': 3, 'endLine': 6, 'startChar': 42, 'endChar': 93,
            'sourceHash': hashlib.sha256(b'void Run(int value) {}').hexdigest(),
            'sourceFileHash': self.digest, 'name': 'Run', 'type': 'Example', 'namespace': 'Nova',
            'signature': 'public void Run(int value = 3)', 'visibility': 'public', 'family': 'runtime.Example',
            'kind': 'method_declaration', 'hasBody': True,
            'parameters': [{'name': 'value', 'type': 'int', 'default': '3', 'modifiers': []}],
            'returns': 'void', 'attributes': [], 'xml': '不可保存的全文', 'calls': ['Execute'],
            'throws': ['throw failure'], 'review': {'status': 'reviewed'}}
        self.field = {'source': self.source, 'sourceFileHash': self.digest, 'line': 2,
            'owner': 'Example', 'name': 'value', 'type': 'int', 'family': 'runtime.Example',
            'initializer': 'SECRET', 'attributes': ['SerializeField'], 'serialized': True,
            'evidence': '不可保留的证据'}
        self.index = {'functions': [self.function], 'fields': [self.field], 'files': [
            {'source': self.source, 'sourceHash': self.digest, 'family': 'runtime.Example'}],
            'inputs': [{'source': 'Assets/Framework/Docs/Example.md', 'sourceHash': self.digest,
                'family': 'runtime.Example', 'kind': 'documentation', 'rawValue': 'SECRET'}]}
        self.coverage = {'features': [{'id': 'runtime.Example', 'sources': [self.source],
            'docs': ['Assets/Framework/Docs/Example.md#usage']} ]}
        self.write()

    def write(self):
        """执行正式白名单投影，返回当前 manifest 计数。"""
        return api.write_snapshot(self.index, self.snapshot, self.coverage)

    def envelopes(self):
        """仅为恶意输入测试读取夹具 JSONL。"""
        return [json.loads(line) for line in self.snapshot.read_text(encoding='utf-8').splitlines()]

    def corrupt(self, envelopes):
        """直接写入故意破坏的输入，模拟不可信安装包快照。"""
        self.snapshot.write_text(''.join(json.dumps(row, ensure_ascii=False) + '\n' for row in envelopes), encoding='utf-8')

    def test_projection_is_allowlisted_deterministic_and_complete(self):
        """快照只保留声明与定位数据，字典及输入顺序不影响字节。"""
        original = self.snapshot.read_bytes()
        result = api.read_snapshot(self.snapshot)
        function = result['functions'][0]
        for key in ('xml', 'calls', 'throws', 'review'):
            self.assertNotIn(key, function)
        self.assertNotIn('initializer', result['fields'][0])
        self.assertNotIn('evidence', result['fields'][0])
        self.assertNotIn('rawValue', result['inputs'][0])
        self.assertEqual([{'path': 'Assets/Framework/Docs/Example.md', 'anchor': 'usage'}], function['docRefs'])
        self.assertEqual({'functions': 1, 'fields': 1, 'sourceFiles': 1, 'capabilityInputs': 1}, self.write())
        self.index['functions'][0] = dict(reversed(list(self.function.items())))
        self.write()
        self.assertEqual(original, self.snapshot.read_bytes())

    def test_strict_reader_rejects_manifest_tampering_and_duplicate_properties(self):
        """计数、版本、扩展 manifest 属性和重复 JSON 属性均为硬错误。"""
        base = self.envelopes()
        for name, value in [('functions', 4), ('schemaVersion', 2), ('functions', True), ('reviewed', 1)]:
            with self.subTest(name=name, value=value):
                rows = copy.deepcopy(base)
                rows[0][name] = value
                self.corrupt(rows)
                with self.assertRaises(api.IndexFormatError):
                    api.read_snapshot(self.snapshot)
        self.snapshot.write_text('{"kind":"manifest","kind":"manifest"}\n', encoding='utf-8')
        with self.assertRaisesRegex(api.IndexFormatError, '重复 JSON'):
            api.read_snapshot(self.snapshot)

    def test_strict_reader_rejects_hidden_payloads_at_every_level(self):
        """记录、参数、文档引用及字段均不能扩展原始配置或历史审查载荷。"""
        base = self.envelopes()
        mutations = [
            lambda rows: rows[1]['data'].update(xml='secret'),
            lambda rows: rows[1]['data']['parameters'][0].update(body='secret'),
            lambda rows: rows[1]['data']['docRefs'][0].update(contents='secret'),
            lambda rows: rows[2]['data'].update(initializer='secret'),
            lambda rows: rows[4]['data'].update(configurationValue='secret'),
        ]
        for mutate in mutations:
            rows = copy.deepcopy(base)
            mutate(rows)
            self.corrupt(rows)
            with self.assertRaises(api.IndexFormatError):
                api.read_snapshot(self.snapshot)

    def test_reader_rejects_unsafe_paths_and_file_hash_inconsistency(self):
        """相对路径不能绕过边界；定位的文件指纹必须与 file 记录一致。"""
        base = self.envelopes()
        for source in ('../secret.cs', '/tmp/secret.cs', 'C:/secret.cs', 'a\\b.cs', 'file:///secret.cs',
                       'a//b.cs', './b.cs', 'a/../b.cs', 'a\0b.cs'):
            rows = copy.deepcopy(base)
            rows[3]['data']['source'] = source
            self.corrupt(rows)
            with self.assertRaises(api.IndexFormatError):
                api.read_snapshot(self.snapshot)
        rows = copy.deepcopy(base)
        rows[3]['data']['sourceHash'] = '1' * 64
        self.corrupt(rows)
        with self.assertRaisesRegex(api.IndexFormatError, 'sourceFileHash'):
            api.read_snapshot(self.snapshot)

    def test_duplicate_identity_and_failed_write_preserve_existing_snapshot(self):
        """非法重载身份不会替换此前有效产物，也不留下临时文件。"""
        before = self.snapshot.read_bytes()
        self.index['functions'].append(copy.deepcopy(self.function))
        with self.assertRaisesRegex(api.IndexFormatError, '重复'):
            self.write()
        self.assertEqual(before, self.snapshot.read_bytes())
        self.assertEqual([], list(self.agents.glob('*.tmp')))

    def test_field_variants_differing_only_in_initializers_collapse_safely(self):
        """条件编译只改变初始化表达式时，声明投影去重且不带出任一实际值。"""
        self.index['fields'].append(dict(self.field, initializer='OTHER_SECRET'))
        self.assertEqual(1, self.write()['fields'])
        field = api.read_snapshot(self.snapshot)['fields'][0]
        self.assertNotIn('initializer', field)

    def test_sqlite_queries_use_exact_ids_family_filter_and_stable_paging(self):
        """重载身份独立，模糊查询、功能族和分页均在缓存中执行。"""
        second = copy.deepcopy(self.function)
        second.update(id=self.source + '#Nova.Example.Stop()', name='Stop', signature='void Stop()')
        self.index['functions'].append(second)
        self.write()
        with api.open_index(self.root, self.agents) as index:
            self.assertEqual('Run', index.get_function(self.function['id'])['name'])
            self.assertIsNone(index.get_function('missing'))
            self.assertEqual(self.digest, index.get_file(self.source)['sourceHash'])
            self.assertIsNone(index.get_file('Assets/missing.cs'))
            self.assertEqual(['Run'], [row['name'] for row in index.find_functions('RUN', 'runtime.Example')])
            self.assertEqual([], index.find_functions(feature='other'))
            self.assertEqual('Stop', index.rows('functions', limit=1, offset=1)[0]['name'])
            self.assertEqual(2, len(index.all_rows('function')))
            self.assertEqual(2, index.counts['functions'])
            self.assertEqual([], index.find_functions("' OR 1=1 --"))
            for kwargs in ({'limit': -1}, {'limit': True}, {'limit': 1001}, {'offset': -1}):
                with self.assertRaises(ValueError):
                    index.rows('function', **kwargs)

    def test_warm_open_never_reads_snapshot_or_imports_ast_tool(self):
        """缓存命中时仅检查文件元数据，无需源码、快照正文或 Dotnet。"""
        with api.open_index(self.root, self.agents) as index:
            cache = index.cache_path
        modified = cache.stat().st_mtime_ns
        with patch.object(api, '_stream', side_effect=AssertionError('热查询不得读取快照')), \
                patch('subprocess.run', side_effect=AssertionError('查询不得启动编译器')):
            with api.open_index(self.root, self.agents) as index:
                self.assertEqual('Run', index.get_function(self.function['id'])['name'])
        self.assertEqual(modified, cache.stat().st_mtime_ns)

    def test_cache_rebuilds_after_snapshot_change_even_with_preserved_mtime(self):
        """ctime 变化能发现保留 mtime 的等长替换，旧缓存不会掩盖新声明。"""
        with api.open_index(self.root, self.agents) as index:
            cache = index.cache_path
        previous = self.snapshot.stat()
        self.index['functions'][0]['name'] = 'New'
        self.write()
        os.utime(self.snapshot, ns=(previous.st_atime_ns, previous.st_mtime_ns))
        with api.open_index(self.root, self.agents) as index:
            self.assertEqual(cache, index.cache_path)
            self.assertEqual('New', index.get_function(self.function['id'])['name'])

    def test_corrupt_or_incomplete_cache_schema_and_metadata_are_rebuilt(self):
        """即使缓存文件存在，损坏、缺表、元数据及记录缺失也必须重建。"""
        with api.open_index(self.root, self.agents) as index:
            cache = index.cache_path
        for sql in ('DROP TABLE records', "DELETE FROM meta WHERE key='complete'", 'DELETE FROM records WHERE kind=\'function\''):
            with closing(sqlite3.connect(cache)) as connection, connection:
                connection.execute(sql)
            with api.open_index(self.root, self.agents) as index:
                self.assertIsNotNone(index.get_function(self.function['id']))
        cache.write_bytes(b'not a database')
        with api.open_index(self.root, self.agents) as index:
            self.assertIsNotNone(index.get_function(self.function['id']))

    def test_cached_record_cannot_bypass_snapshot_allowlist(self):
        """缓存 SQL 数据被局部改写后，查询仍逐条拒绝扩展载荷。"""
        with api.open_index(self.root, self.agents) as index:
            cache = index.cache_path
            row = index.get_function(self.function['id'])
        row['xml'] = '非法正文'
        with closing(sqlite3.connect(cache)) as connection, connection:
            connection.execute('UPDATE records SET data=? WHERE kind=?', (json.dumps(row), 'function'))
        with api.open_index(self.root, self.agents) as index:
            with self.assertRaises(api.IndexFormatError):
                index.get_function(self.function['id'])

    def test_missing_snapshot_does_not_scan_and_local_snapshot_is_supported(self):
        """本地首次查询给出显式 index 提示，随后可读取 Library 内正式本地快照。"""
        self.snapshot.unlink()
        with self.assertRaisesRegex(FileNotFoundError, '显式执行.*index'):
            api.open_index(self.root, self.agents)
        local = self.root / 'Library/Nova/AgentIndex/api-index.jsonl'
        api.write_snapshot(self.index, local, self.coverage)
        with api.open_index(self.root, self.agents) as index:
            self.assertEqual(local.resolve(), index.snapshot_path)

    def test_cross_process_cold_open_and_no_dotnet_consumer(self):
        """多个无 Dotnet 的新进程竞争首次打开，只能发布完整可读缓存。"""
        code = ('import json,sys; from pathlib import Path; sys.path.insert(0,sys.argv[1]); '
            'import nova_api_index as a; '
            'i=a.open_index(Path(sys.argv[2]),Path(sys.argv[3])); '
            'print(json.dumps(i.counts)); i.close()')
        environment = dict(os.environ, PATH='', PYTHONDONTWRITEBYTECODE='1')
        children = [subprocess.Popen([sys.executable, '-B', '-c', code, str(TOOLS), str(self.root), str(self.agents)],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, env=environment) for _ in range(5)]
        try:
            for child in children:
                stdout, stderr = child.communicate(timeout=20)
                self.assertEqual(0, child.returncode, stderr)
                self.assertEqual(1, json.loads(stdout)['functions'])
        finally:
            for child in children:
                if child.poll() is None:
                    child.kill()
                    child.communicate()
        self.assertEqual(1, len(list((self.root / 'Library/Nova/AgentIndex').rglob('index.sqlite'))))
        self.assertEqual([], list((self.root / 'Library/Nova/AgentIndex').rglob('.index-*')))

    def test_document_navigation_prefers_mirror_and_normalizes_actual_case(self):
        """准确镜像优先于泛模块说明，并返回磁盘真实文件名大小写。"""
        doc = self.root / 'Assets/Framework/Docs/Runtime/Example.md'
        doc.parent.mkdir(parents=True)
        doc.write_text('说明', encoding='utf-8')
        record = dict(self.function, source='Assets/Framework/Scripts/Runtime/Example.cs')
        self.assertEqual([{'path': doc.relative_to(self.root).as_posix()}],
            api.resolve_doc_refs(record, self.root, self.coverage))

    def test_document_navigation_unique_name_and_partial_type_are_package_scoped(self):
        """Core 迁位及 partial 文件可用同包类型同名定位，重名接口不会跨包串线。"""
        documents = ('Assets/Framework/Docs/Runtime/Modules/FrameworkComponent.md',
            'Assets/Framework/Docs/Runtime/Modules/Ad/IAdPlugin.md',
            'UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/IAdPlugin.md')
        for source in documents:
            doc = self.root / source
            doc.parent.mkdir(parents=True, exist_ok=True)
            doc.write_text('说明', encoding='utf-8')
        record = dict(self.function, source='Assets/Framework/Scripts/Runtime/Core/FrameworkComponent.Lifecycle.cs',
            type='FrameworkComponent')
        self.assertEqual([{'path': documents[0]}], api.resolve_doc_refs(record, self.root))
        record = dict(self.function,
            source='UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Scripts/Runtime/IAdPlugin.cs', type='IAdPlugin')
        self.assertEqual([{'path': documents[2]}], api.resolve_doc_refs(record, self.root))

    def test_document_navigation_fallback_is_existing_same_package_only(self):
        """后备 Docs 引用保留锚点，修正大小写并排除不存在或跨包引用。"""
        doc = self.root / 'Assets/Framework/Docs/Runtime/Module.md'
        doc.parent.mkdir(parents=True)
        doc.write_text('说明', encoding='utf-8')
        coverage = {'features': [{'id': self.function['family'], 'docs': [
            'Assets/Framework/Docs/runtime/module.md#usage', 'Assets/Framework/Docs/Missing.md',
            'UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/IAdPlugin.md']} ]}
        self.assertEqual([{'path': doc.relative_to(self.root).as_posix(), 'anchor': 'usage'}],
            api.resolve_doc_refs(self.function, self.root, coverage))

    def test_partial_document_fallback_requires_matching_declaring_type(self):
        """文件名前缀恰巧相同但声明类型不同，不得去后缀误配文档。"""
        doc = self.root / 'Assets/Framework/Docs/Runtime/Example.md'
        doc.parent.mkdir(parents=True)
        doc.write_text('说明', encoding='utf-8')
        record = dict(self.function, source='Assets/Framework/Scripts/Runtime/Example.Other.cs', type='Different')
        self.assertEqual([], api.resolve_doc_refs(record, self.root))

    def test_sample_navigation_prefers_own_readme_then_explicit_owning_package_docs(self):
        """Sample 自身 README 优先；不存在时只接受显式映射的所属包 Docs。"""
        readme = self.root / 'Assets/Samples/AdDemo/README.md'
        readme.parent.mkdir(parents=True)
        readme.write_text('广告演示说明', encoding='utf-8')
        package_doc = self.root / 'UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/Usage.md'
        package_doc.parent.mkdir(parents=True)
        package_doc.write_text('广告包说明', encoding='utf-8')
        record = dict(self.function, source='Assets/Samples/AdDemo/Scripts/Demo.cs', family='sample.AdDemo')
        coverage = {'features': [{'id': 'sample.AdDemo', 'docs': [
            'UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/doc/usage.md#demo']} ]}
        self.assertEqual([{'path': readme.relative_to(self.root).as_posix()}],
            api.resolve_doc_refs(record, self.root, coverage))
        readme.unlink()
        self.assertEqual([{'path': package_doc.relative_to(self.root).as_posix(), 'anchor': 'demo'}],
            api.resolve_doc_refs(record, self.root, coverage))

    def test_development_skill_prefers_own_skill_and_tools_use_agents_index(self):
        """开发 Skill 脚本使用自身说明，随包 Tools 使用 Agents 快速入口。"""
        own = self.root / '.agents/skills/nova-publish/SKILL.md'
        own.parent.mkdir(parents=True)
        own.write_text('发布 Skill', encoding='utf-8')
        tools = self.root / 'Assets/Framework/Agents/INDEX.md'
        tools.parent.mkdir(parents=True)
        tools.write_text('Tools 入口', encoding='utf-8')
        record = dict(self.function, source='.agents/skills/nova-publish/scripts/publish.py')
        self.assertEqual([{'path': own.relative_to(self.root).as_posix()}], api.resolve_doc_refs(record, self.root))
        record['source'] = 'Assets/Framework/Agents/Tools/nova_api_index.py'
        self.assertEqual([{'path': tools.relative_to(self.root).as_posix()}], api.resolve_doc_refs(record, self.root))

    def test_shared_development_tools_use_only_explicit_existing_docs(self):
        """共享开发工具不猜用其它 Skill；只解析其显式覆盖文档。"""
        record = dict(self.function, source='.agents/skills/_shared/helper.py', family='development.shared')
        self.assertEqual([], api.resolve_doc_refs(record, self.root))
        doc = self.root / 'Assets/Framework/Docs/Shared.md'
        doc.parent.mkdir(parents=True)
        doc.write_text('共享说明', encoding='utf-8')
        coverage = {'features': [{'id': 'development.shared', 'docs': ['Assets/Framework/Docs/shared.md']} ]}
        self.assertEqual([{'path': doc.relative_to(self.root).as_posix()}],
            api.resolve_doc_refs(record, self.root, coverage))


if __name__ == '__main__':
    unittest.main()
