"""验证按需定位、消费来源边界及当前契约，不依赖人工知识账本或 Unity。"""
import hashlib
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import nova_api_index as api
import nova_capabilities as cap
import test_nova_skills as skill_fixtures


class CapabilityTests(unittest.TestCase):
    def setUp(self):
        """建立独立开发工程及真实白名单快照，测试后自动删除所有本地缓存。"""
        self.temp = tempfile.TemporaryDirectory(prefix='nova-capability-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.agents = self.root / 'Assets/Framework/Agents'
        fixture = skill_fixtures.NovaSkillsToolTests()
        fixture.agents_root = self.agents
        fixture._write_agents_root()
        self.rel = 'Assets/Framework/Scripts/Example.cs'
        self.source = self.root / self.rel
        self.source.parent.mkdir(parents=True)
        self.body = 'public void Run(int value = 3) { }'
        self.text = 'class Example {\n/// <summary>执行示例。</summary>\n' + self.body + '\n}\n'
        self.source.write_text(self.text, encoding='utf-8')
        self.doc = self.agents.parent / 'Docs/START_HERE.md'
        self.doc_rel = self.doc.relative_to(self.root).as_posix()
        self.node = {'id': self.rel + '#Example.Run(int)', 'source': self.rel, 'name': 'Run',
            'namespace': '', 'type': 'Example', 'signature': 'public void Run(int value = 3)',
            'line': 3, 'endLine': 3, 'startChar': self.text.index(self.body),
            'endChar': self.text.index(self.body) + len(self.body), 'kind': 'method_declaration',
            'hasBody': True, 'visibility': 'public', 'family': 'example',
            'sourceHash': hashlib.sha256(self.body.encode()).hexdigest(),
            'sourceFileHash': hashlib.sha256(self.source.read_bytes()).hexdigest(),
            'parameters': [{'name': 'value', 'type': 'int', 'default': '3', 'modifiers': []}],
            'returns': 'void', 'attributes': []}
        self.sources = {'functions': [self.node], 'fields': [], 'files': [
            {'source': self.rel, 'sourceHash': self.node['sourceFileHash'], 'family': 'example'}],
            'inputs': [{'source': self.doc_rel, 'sourceHash': hashlib.sha256(self.doc.read_bytes()).hexdigest(),
                'family': 'example', 'kind': 'documentation'}]}
        self.cov = {'schemaVersion': 2, 'features': [{'id': 'example', 'sources': [self.rel],
            'docs': [self.doc_rel], 'skills': ['nova-project-router', 'nova-project-ui-create-view'],
            'adapters': [{'kind': 'workspace-inspection', 'entry': 'test adapter', 'when': '读取当前工程'}],
            'limitations': ['声明导航不代表语义已审或运行已验收'], 'taskScope': 'internal',
            'nonUserReason': '测试独立定位链路'}], 'userTasks': []}
        self.persist()
        for name in ('_discover_registered_agent_actions', '_discover_exposed_agent_actions'):
            mocked = patch.object(cap.nova_skills, name, return_value=set())
            mocked.start()
            self.addCleanup(mocked.stop)
        self.source_files = patch('nova_function_index.source_files', side_effect=lambda _: [self.source] if self.source.is_file() else [])
        self.source_files.start()
        self.addCleanup(self.source_files.stop)
        self.input_files = patch('nova_function_index.input_files', side_effect=lambda _: [self.doc] if self.doc.is_file() else [])
        self.input_files.start()
        self.addCleanup(self.input_files.stop)

    def persist(self):
        """写当前导航及派生快照，不保存人工语义或历史运行结果。"""
        (self.agents / 'coverage.json').write_text(json.dumps(self.cov, ensure_ascii=False), encoding='utf-8')
        api.write_snapshot(self.sources, self.agents / 'api-index.jsonl', self.cov)

    def run_check(self, root=None):
        """使用真实检查入口并保留真实 Catalog 和安全契约校验。"""
        return cap.check(root or self.root, self.agents)

    def consumer(self):
        """建立与包真源位置独立的消费工程。"""
        root = self.root / 'Consumer'
        root.mkdir(exist_ok=True)
        return root

    def write_lock(self, root, package, entry):
        """冻结指定消费包的精确 UPM lock 条目。"""
        packages = root / 'Packages'
        packages.mkdir(exist_ok=True)
        path = packages / 'packages-lock.json'
        lock = json.loads(path.read_text()) if path.is_file() else {'dependencies': {}}
        lock['dependencies'][package] = entry
        path.write_text(json.dumps(lock), encoding='utf-8')
        (packages / 'manifest.json').write_text('{"dependencies":{}}', encoding='utf-8')

    def add_task(self, identity=None):
        """引用既有 Skill 契约，任务仍要求当次 compile 证据。"""
        task = {'id': 'example.run', 'goal': '查阅现有功能及执行前提',
            'skill': 'nova-project-ui-create-view',
            'reference': 'Assets/Framework/Agents/Skills/nova-project-ui-create-view/SKILL.md',
            'functions': [identity or self.node['id']],
            'execution': {'mode': 'read-only', 'adapters': [
                {'kind': 'workspace-inspection', 'entry': 'test adapter', 'relation': 'direct'}],
                'conditions': ['读取当前实际工程'], 'gaps': []}, 'verification': {'additionalRequired': []}}
        self.cov['userTasks'] = [task]
        self.cov['features'][0].update(taskScope='consumer', taskRefs=[task['id']])
        self.persist()
        return task

    def test_current_check_reports_navigation_without_semantic_or_runtime_claims(self):
        """检查通过仅表示当前声明、来源和契约一致，不创建已审或已验收统计。"""
        result = self.run_check()
        self.assertTrue(result['valid'], result['errors'])
        self.assertEqual('not-performed', result['summary']['semanticReview'])
        self.assertEqual(1, result['summary']['functions'])
        for name in ('reviewedFunctions', 'pendingFunctions', 'recordedCurrentTasks', 'liveVerifiedTasks'):
            self.assertNotIn(name, result['summary'])

    def test_default_summary_defers_body_and_full_reads_current_exact_span(self):
        """默认只返回导航，显式 full 才读取命中正文与附近 XML。"""
        with patch.object(cap, '_function_body', wraps=cap._function_body) as body:
            summary = cap.describe(self.root, self.agents, self.node['id'])
            body.assert_not_called()
            full = cap.describe(self.root, self.agents, self.node['id'], 'full')
            body.assert_called_once()
        self.assertIsNone(summary['sourceBody'])
        self.assertEqual(self.body, full['sourceBody'])
        self.assertIn('执行示例', full['sourceDocumentation'])
        self.assertEqual('not-performed', full['function']['semanticReview'])
        self.assertEqual('3', full['function']['parameters'][0]['default'])
        self.assertNotIn('review', full)

    def test_utf16_span_handles_bom_crlf_and_non_bmp_character(self):
        """Roslyn UTF-16 偏移不被 BOM、CRLF 或代理对误导。"""
        prefix = '// 😀\r\nclass Example {\r\n'
        text = prefix + self.body + '\r\n}\r\n'
        self.source.write_bytes(b'\xef\xbb\xbf' + text.encode())
        start = len(prefix.encode('utf-16-le')) // 2
        self.node.update(startChar=start, endChar=start + len(self.body.encode('utf-16-le')) // 2,
            sourceFileHash=hashlib.sha256(self.source.read_bytes()).hexdigest())
        self.sources['files'][0]['sourceHash'] = self.node['sourceFileHash']
        self.persist()
        self.assertEqual(self.body, cap.describe(self.root, self.agents, self.node['id'], 'full')['sourceBody'])

    def test_stale_or_missing_source_never_uses_old_span_or_returns_body(self):
        """来源变化或缺失时禁止使用陈旧偏移，不恢复历史正文。"""
        self.source.write_text('class Changed {}', encoding='utf-8')
        with patch.object(cap, '_function_body', side_effect=AssertionError('陈旧偏移不能读取')):
            result = cap.describe(self.root, self.agents, self.node['id'], 'full')
        self.assertEqual('stale', result['status'])
        self.assertIsNone(result['sourceBody'])
        self.source.unlink()
        result = cap.describe(self.root, self.agents, self.node['id'], 'full')
        self.assertEqual('unavailable', result['status'])
        self.assertIsNone(result['sourceBody'])

    def test_check_detects_bad_span_even_when_file_hash_matches(self):
        """文件指纹匹配不能掩盖错误声明偏移。"""
        self.node['startChar'] += 1
        self.persist()
        result = self.run_check()
        self.assertFalse(result['valid'])
        self.assertTrue(any('span' in error for error in result['errors']))

    def test_same_name_overloads_are_ambiguous_until_exact_identity_selected(self):
        """同名不同签名不能猜选，精确身份只命中目标重载。"""
        other = dict(self.node, id=self.rel + '#Example.Run(string)', signature='public void Run(string value)')
        self.sources['functions'].append(other)
        self.persist()
        result = cap.describe(self.root, self.agents, 'Run')
        self.assertEqual('ambiguous', result['status'])
        self.assertEqual({self.node['id'], other['id']}, set(result['candidates']))
        self.assertEqual(self.node['id'], cap.describe(self.root, self.agents, self.node['id'])['function']['id'])

    def test_query_reads_only_target_source_without_full_scan_or_knowledge(self):
        """命中单个函数时不扫描全源码、全快照、其它输入或旧 Knowledge。"""
        with api.open_index(self.root, self.agents):
            pass
        original_read = Path.read_bytes
        original_open = Path.open
        reads = []
        def guarded(path):
            """记录正文读取并拒绝当前命中源码以外的任何文件。"""
            reads.append(path.resolve())
            if path.resolve() != self.source.resolve():
                raise AssertionError('不允许读取：' + str(path))
            return original_read(path)
        def guarded_open(path, *args, **kwargs):
            """覆盖文本及二进制打开路径，旧知识目录不能因读取 API 不同绕过测试。"""
            if any(part in {'Knowledge', 'Reviews'} for part in path.parts):
                raise AssertionError('不能读取旧知识账本：' + str(path))
            return original_open(path, *args, **kwargs)
        with patch.object(api.Index, 'all_rows', side_effect=AssertionError('查询不能全取')), \
                patch.object(api, '_stream', side_effect=AssertionError('热查询不能载快照')), \
                patch('nova_function_index.source_files', side_effect=AssertionError('查询不能扫源码')), \
                patch('nova_function_index.input_files', side_effect=AssertionError('查询不能扫输入')), \
                patch.object(Path, 'read_bytes', guarded), patch.object(Path, 'open', guarded_open):
            result = cap.inventory(self.root, self.agents, 'functions', None, 'Run', 1, 0)
        self.assertEqual(1, len(result['items']))
        self.assertEqual([self.source.resolve()], reads)
        self.assertEqual('current', result['items'][0]['sourceStatus'])

    def test_paging_does_not_read_unreturned_sources(self):
        """分页先筛选，续页探测不读取尚未展示函数的源码。"""
        other = dict(self.node, id='Assets/Framework/Scripts/Z.cs#Z.Stop()',
            source='Assets/Framework/Scripts/Z.cs', name='Stop')
        self.sources['functions'].append(other)
        self.sources['files'].append({'source': other['source'], 'sourceHash': self.node['sourceFileHash'], 'family': 'example'})
        self.persist()
        with patch.object(cap, '_source_state', wraps=cap._source_state) as states:
            result = cap.inventory(self.root, self.agents, 'functions', None, None, 1, 0)
        self.assertEqual(1, states.call_count)
        self.assertEqual(1, result['nextOffset'])

    def test_fields_never_expose_initializer_or_effective_asset_value(self):
        """字段导航保留声明，初始化表达式与当前资产值均不进入快照或响应。"""
        self.sources['fields'] = [{'source': self.rel, 'sourceFileHash': self.node['sourceFileHash'],
            'line': 1, 'owner': 'Example', 'name': 'Count', 'type': 'int', 'family': 'example',
            'initializer': '5', 'serialized': True, 'attributes': []}]
        self.persist()
        row = cap.inventory(self.root, self.agents, 'fields', None, None, 25, 0)['items'][0]
        self.assertEqual('Count', row['name'])
        self.assertNotIn('initializer', row)
        self.assertIn('unknown', row['effectiveConfiguration'])

    def test_check_detects_source_input_hash_and_membership_changes(self):
        """完整检查分别发现源码与输入的增删和内容漂移。"""
        added_source = self.source.with_name('Added.cs')
        added_source.write_text('class Added {}', encoding='utf-8')
        added_input = self.doc.with_name('Added.md')
        added_input.write_text('新增输入', encoding='utf-8')
        with patch('nova_function_index.source_files', return_value=[self.source, added_source]), \
                patch('nova_function_index.input_files', return_value=[self.doc, added_input]):
            result = self.run_check()
        self.assertTrue(any('新增源码未索引' in error for error in result['errors']))
        self.assertTrue(any('新增输入未索引' in error for error in result['errors']))
        self.doc.write_text('内容变化', encoding='utf-8')
        self.source.write_text(self.text + '// 变化\n', encoding='utf-8')
        result = self.run_check()
        self.assertTrue(any('源码索引需刷新' in error for error in result['errors']))
        self.assertTrue(any('能力输入索引需刷新' in error for error in result['errors']))
        self.source.unlink()
        self.doc.unlink()
        result = self.run_check()
        self.assertTrue(any('已删除源码仍在索引' in error for error in result['errors']))
        self.assertTrue(any('已删除输入仍在索引' in error for error in result['errors']))

    def test_contract_safety_and_adapter_membership_remain_hard_gates(self):
        """当前 contract 安全字段及功能族入口归属仍为检查硬门。"""
        path = self.agents / 'Skills/nova-project-ui-create-view/references/contract.json'
        contract = json.loads(path.read_text())
        del contract['confirmation']
        path.write_text(json.dumps(contract), encoding='utf-8')
        self.cov['features'][0]['adapters'].append({'kind': 'agent-action',
            'entry': 'nova.project.missing.execute', 'when': '伪造入口'})
        self.persist()
        result = self.run_check()
        self.assertFalse(result['valid'])
        self.assertTrue(any('confirmation' in error for error in result['errors']))
        self.assertTrue(any('Action 不存在或未暴露' in error for error in result['errors']))

    def test_obsolete_knowledge_references_are_rejected_without_reading_ledger(self):
        """覆盖清单不能重新挂接旧账本，检查也不读取其内容。"""
        self.cov['reviewFiles'] = ['Assets/Framework/Agents/Knowledge/missing.json']
        self.persist()
        result = self.run_check()
        self.assertFalse(result['valid'])
        self.assertTrue(any('人工知识账本' in error for error in result['errors']))

    def test_consumer_framework_source_ignores_residual_development_copy(self):
        """消费工程残留 Assets/Framework 源不能覆盖已选择的实际包真源。"""
        root = self.consumer()
        shadow = root / self.rel
        shadow.parent.mkdir(parents=True)
        shadow.write_text('class Wrong {}', encoding='utf-8')
        self.assertEqual(self.source, cap._source_path(root, self.agents, self.rel))
        self.source.unlink()
        self.assertEqual('applicable', cap._source_applicability(root, self.agents, self.rel)['status'])
        self.assertIsNone(cap._source_path(root, self.agents, self.rel))
        self.assertFalse(self.run_check(root)['valid'])

    def test_consumer_source_symlink_cannot_escape_resolved_framework_package(self):
        """包内同名符号链接不能把源码定位重定向到包边界以外。"""
        root = self.consumer()
        outside = self.root / 'Outside.cs'
        outside.write_text('class Outside {}')
        self.source.unlink()
        self.source.symlink_to(outside)
        self.assertIsNone(cap._source_path(root, self.agents, self.rel))

    def test_registry_extension_uses_exact_lock_despite_residual_directories(self):
        """lock 和 package.json 决定扩展实际来源，残留本地目录与旧缓存不可抢占。"""
        root = self.consumer()
        package = 'com.solotopia.nova.framework.kit.example'
        self.write_lock(root, package, {'source': 'registry', 'version': '2.0.0'})
        locations = [('UPMPackages/' + package, '8.0.0'), ('Packages/' + package, '7.0.0'),
            ('Library/PackageCache/' + package + '@old', '1.0.0'), ('Library/PackageCache/' + package + '@hash', '2.0.0')]
        for relative, version in locations:
            directory = root / relative
            (directory / 'Nova').mkdir(parents=True)
            (directory / 'package.json').write_text(json.dumps({'name': package, 'version': version}))
            (directory / 'Nova/Example.cs').write_text('class Extension {}')
        expected = root / ('Library/PackageCache/' + package + '@hash/Nova/Example.cs')
        source = 'UPMPackages/' + package + '/Nova/Example.cs'
        self.assertEqual(expected, cap._source_path(root, self.agents, source))
        expected.unlink()
        self.assertEqual('applicable', cap._source_applicability(root, self.agents, source)['status'])
        self.assertIsNone(cap._source_path(root, self.agents, source))

    def test_default_context_resolves_locked_framework_over_residual_agents_directory(self):
        """自动定位必须服从当前 Framework lock，残留 Assets/Framework/Agents 不可覆盖。"""
        root = self.consumer()
        package = 'com.solotopia.nova.framework'
        self.write_lock(root, package, {'source': 'registry', 'version': '0.6.9'})
        (root / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {package: '0.6.9'}}))
        installed = root / ('Library/PackageCache/' + package + '@hash')
        shutil.copytree(self.agents.parent, installed)
        residual = root / 'Assets/Framework/Agents'
        residual.mkdir(parents=True)
        (residual / 'coverage.json').write_text('{"obsolete":true}')
        result = cap.describe(root, None, self.node['id'])
        self.assertEqual('success', result['status'])
        self.assertEqual('current', result['function']['sourceStatus'])

    def test_uninstalled_and_development_sources_are_explicitly_not_applicable(self):
        """未安装扩展与开发工具仅保留导航；Framework 自身缺文件始终适用。"""
        root = self.consumer()
        for source in ('UPMPackages/com.solotopia.nova.framework.kit.absent/Nova/Example.cs',
                       '.agents/skills/nova-publish/scripts/tool.py', 'NovaSpark.cs'):
            self.assertEqual('not_applicable', cap._source_applicability(root, self.agents, source)['status'])
        self.assertEqual('applicable', cap._source_applicability(root, self.agents, 'Assets/Framework/Missing.cs')['status'])

    def sample(self, version='0.6.9'):
        """只在隔离目录写 Sample 文本夹具，验证工具的只读身份判定。"""
        package = json.loads((self.agents.parent / 'package.json').read_text())
        package.update(displayName='Nova Framework', samples=[{'displayName': 'MainDemo', 'path': 'Samples~/MainDemo'}])
        (self.agents.parent / 'package.json').write_text(json.dumps(package))
        root = self.consumer()
        imported = root / ('Assets/Samples/Nova Framework/' + version + '/MainDemo')
        (imported / 'Editor').mkdir(parents=True)
        (imported / 'Scripts/Editor').mkdir(parents=True)
        guid = 'abc12300000000000000000000000000'
        manifest = imported / 'Editor/SamplePathManifest.asset'
        manifest.write_text('  m_DevSampleRoot: Assets/Samples/MainDemo\n'
            '  m_Script: {fileID: 11500000, guid: ' + guid + ', type: 3}\n')
        (imported / 'Scripts/Editor/SamplePathManifest.cs').write_text('class SamplePathManifest {}')
        (imported / 'Scripts/Editor/SamplePathManifest.cs.meta').write_text('guid: ' + guid + '\n')
        expected = imported / 'Scripts/Example.cs'
        expected.write_text('class Sample {}')
        return root, imported, manifest, expected

    def test_sample_requires_exact_manifest_identity_and_script_guid(self):
        """Sample 位置、manifest 来源及 MonoScript 绑定缺一项均不猜选。"""
        root, _, manifest, expected = self.sample()
        source = 'Assets/Samples/MainDemo/Scripts/Example.cs'
        valid = manifest.read_text()
        self.assertEqual(expected, cap._source_path(root, self.agents, source))
        for text in (valid.replace('Assets/Samples/MainDemo', 'Assets/Samples/OtherDemo'),
                     valid.replace('abc123', 'def456'), valid.replace('fileID: 11500000', 'fileID: 0'),
                     valid.replace('type: 3', 'type: 2'), valid + valid):
            manifest.write_text(text)
            self.assertEqual('applicable', cap._source_applicability(root, self.agents, source)['status'])
            self.assertIsNone(cap._source_path(root, self.agents, source))
        manifest.write_text(valid)
        expected.unlink()
        self.assertTrue(cap._installed_source(root, source, self.agents))
        self.assertIsNone(cap._source_path(root, self.agents, source))

    def test_old_sample_import_and_unrelated_same_name_have_distinct_statuses(self):
        """同包旧版不误判为未导入，其它包同名目录不算当前 Sample。"""
        root, _, _, _ = self.sample(version='0.5.0')
        source = 'Assets/Samples/MainDemo/Scripts/Example.cs'
        self.assertEqual('applicable', cap._source_applicability(root, self.agents, source)['status'])
        self.assertIsNone(cap._source_path(root, self.agents, source))
        other = self.root / 'OtherConsumer'
        (other / 'Assets/Samples/Other Package/0.6.9/MainDemo').mkdir(parents=True)
        self.assertEqual('not_applicable', cap._source_applicability(other, self.agents, source)['status'])

    def test_task_summary_and_full_preserve_minimum_evidence_without_history(self):
        """摘要与 full 均保留最低证据，full 仅展开当前契约。"""
        task = self.add_task()
        summary = cap.describe(self.root, self.agents, task['id'])['task']
        full = cap.describe(self.root, self.agents, task['id'], 'full')['task']
        self.assertEqual('current', summary['sourceStatus'])
        self.assertEqual('not-performed', summary['semanticReview'])
        self.assertEqual(['compile'], summary['validation']['required'])
        self.assertEqual('verify-at-execution', summary['validation']['status'])
        self.assertEqual('ephemeral', summary['validation']['retention'])
        self.assertNotIn('contract', summary)
        self.assertIn('contract', full)
        self.assertEqual(summary['validation']['required'], full['validation']['required'])

    def test_task_history_is_rejected_without_reading_old_result_files(self):
        """历史 passed 与上下文记录不能变为当次运行通过，也不触发文件读取。"""
        task = self.add_task()
        task['verification']['evidence'] = [{'record': {'path': '../outside.json', 'sha256': 'a' * 64}}]
        self.persist()
        with patch.object(cap.nova_skills, '_task_file_state', side_effect=AssertionError('不能读取历史结果')):
            result = cap.describe(self.root, self.agents, task['id'])
        self.assertEqual('blocked', result['status'])
        self.assertTrue(any('verification' in error for error in result['errors']))

    def test_invalid_task_contract_returns_blocked_without_index_error(self):
        """契约缺安全字段时即使报告没有任务行，也必须返回结构化 blocked。"""
        task = self.add_task()
        path = self.agents / 'Skills/nova-project-ui-create-view/references/contract.json'
        contract = json.loads(path.read_text())
        del contract['confirmation']
        path.write_text(json.dumps(contract))
        result = cap.describe(self.root, self.agents, task['id'])
        self.assertEqual('blocked', result['status'])
        self.assertTrue(any('confirmation' in error for error in result['errors']))

    def test_missing_task_function_is_invalid_not_not_applicable(self):
        """Framework 中不存在的任务函数不能通过未安装范围豁免。"""
        task = self.add_task('Assets/Framework/Scripts/Absent.cs#Absent.Run()')
        result = cap.describe(self.root, self.agents, task['id'])
        self.assertEqual('blocked', result['status'])
        self.assertEqual('applicable', result['task']['applicability'])
        self.assertTrue(any('不存在的函数' in error for error in result['errors']))

    def test_rebuilt_check_needs_no_existing_snapshot_or_permanent_cache(self):
        """首次检出可显式全量检查，只使用自动清理的临时快照。"""
        (self.agents / 'api-index.jsonl').unlink()
        before = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        with patch('nova_function_index.build_index', return_value=self.sources), \
                patch.object(api, 'open_index', side_effect=AssertionError('全量重建不得创建永久缓存')):
            result = cap.check_rebuilt(self.root, self.agents)
        self.assertTrue(result['valid'], result['errors'])
        self.assertEqual('not-performed', result['summary']['semanticReview'])
        after = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        self.assertEqual(before, after)

    def test_python_line_range_must_match_normalized_body_hash(self):
        """Python CRLF 与生成器一致规范化，错误行范围或正文摘要必须拒绝。"""
        from nova_function_index import index_python
        path = self.source.with_suffix('.py')
        path.write_bytes(b'def run():\r\n    return 3\r\n')
        node = index_python(path, self.root)[0]
        self.assertEqual('def run():\n    return 3\n', cap._function_body(path, node))
        for changed in (dict(node, line=2), dict(node, sourceHash='0' * 64)):
            with self.assertRaisesRegex(ValueError, '行范围与正文指纹不一致'):
                cap._function_body(path, changed)

    def test_rebuilt_check_rejects_parse_issues_before_projection(self):
        """解析不完整必须阻断，不能把部分发现当作成功导航。"""
        with patch('nova_function_index.build_index', return_value={'parseIssues': ['incomplete branch']}), \
                patch.object(api, 'write_snapshot', side_effect=AssertionError('有解析问题不得投影')):
            with self.assertRaisesRegex(ValueError, 'AST 解析存在问题'):
                cap.check_rebuilt(self.root, self.agents)

    def test_cli_default_summary_and_explicit_full(self):
        """通过 argparse 验证默认摘要与显式 full 的实际 JSON 接线。"""
        prefix = ['--root', str(self.root), '--agents-root', str(self.agents)]
        def invoke(arguments):
            """捕获 CLI 输出并确认成功退出。"""
            stream = io.StringIO()
            with redirect_stdout(stream):
                self.assertEqual(0, cap.main(prefix + arguments))
            return json.loads(stream.getvalue())
        summary = invoke(['describe', self.node['id'], '--json'])
        full = invoke(['describe', self.node['id'], '--detail', 'full', '--json'])
        self.assertIsNone(summary['sourceBody'])
        self.assertEqual(self.body, full['sourceBody'])


if __name__ == '__main__':
    unittest.main()
