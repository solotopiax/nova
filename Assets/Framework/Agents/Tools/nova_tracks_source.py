#!/usr/bin/env python3
"""按真实登记快照维护自有 Tracks 事件页；汇总由稳定 TrackRegistry Action 完成。"""
from __future__ import annotations
import argparse
import base64
import copy
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
import nova_table_source as table

NS = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
PKG = 'http://schemas.openxmlformats.org/package/2006/relationships'
CT = 'http://schemas.openxmlformats.org/package/2006/content-types'
HEADERS = table.TRACKS_HEADERS
VERSION = 1
STATE = table.STATE
Error = table.TableSourceError


def _tag(name):
    """构造 SpreadsheetML 节点名，保留其他命名空间扩展。"""
    return '{' + NS + '}' + name


def _xml(node):
    """序列化被明确修改的 XML 部件，不保存或重建整份 openpyxl 工作簿。"""
    return ET.tostring(node, encoding='utf-8', xml_declaration=True)


def _path(root, relative, exists=True):
    """限定目标为正式 Tracks 文件；库缓存、厂商、符号链接和越界永不可写。"""
    if not isinstance(relative, str) or relative != '/'.join(PurePosixPath(relative).parts) or '\\' in relative or ':' in relative:
        raise Error('Tracks 路径必须为规范项目相对路径。')
    parts = PurePosixPath(relative).parts
    if PurePosixPath(relative).is_absolute() or '..' in parts or '.' in parts:
        raise Error('Tracks 路径不能越界。')
    if not parts or parts[0] not in ('Assets', 'UPMPackages', 'Packages') or parts[-1] != 'Tracks.xlsx':
        raise Error('目标必须是项目自有正式 Tracks.xlsx。')
    denied = table.DENIED_PARTS - {'packages', 'upmpackages'}
    if any(p.casefold() in denied for p in parts):
        raise Error('厂商、缓存和生成目录不可写。')
    path = root
    for part in parts:
        path /= part
        if path.is_symlink():
            raise Error('Tracks 路径含符号链接。')
    if not path.resolve(strict=False).is_relative_to(root) or exists and not path.is_file():
        raise Error('Tracks 源不存在或超出项目。')
    return path


def _file(path):
    """冻结文件整份摘要及存在状态，不在结果回显文件内容。"""
    if path.is_symlink() or any(p.is_symlink() for p in path.parents):
        raise Error('登记源或元数据含符号链接。')
    return {'path': str(path.absolute()).replace('\\', '/'), 'exists': path.is_file(),
            'sha256': table._hash(path.read_bytes()) if path.is_file() else None}


def _transport_registry(root, snapshot):
    """只将 Gateway 的项目根占位路径还原到已核实根；不接受导航、链接或用户主目录推断。"""
    if not isinstance(snapshot,dict):
        raise Error('registry 必须是实际登记快照对象。')
    snapshot=copy.deepcopy(snapshot)
    def path(value):
        """严格限定项目根占位符的相对后缀，不把其他脱敏路径猜成可访问源。"""
        if not isinstance(value,str):
            raise Error('登记路径必须为字符串。')
        prefix='<project-root>/'
        if value.startswith(prefix):
            relative=value[len(prefix):]
            parts=PurePosixPath(relative).parts
            if not relative or relative!='/'.join(parts) or PurePosixPath(relative).is_absolute() or '..' in parts or '\\' in relative or ':' in relative or '<' in relative or '>' in relative:
                raise Error('项目根占位路径后缀无效。')
            current=root
            for part in parts:
                current/=part
                if current.is_symlink():
                    raise Error('登记占位路径含符号链接。')
            return str(current).replace('\\','/')
        if '<' in value or '>' in value:
            raise Error('项目根外路径已脱敏，无法核验；需要明确可读取的来源。')
        return value
    for field in ('sources','metadata'):
        if not isinstance(snapshot.get(field),list):
            raise Error('登记快照集合无效。')
        for state in snapshot[field]:
            if not isinstance(state,dict):
                raise Error('登记文件快照无效。')
            state['path']=path(state.get('path'))
            # C# JSON serializer会省略null字段；只在明确不存在且未给SHA时补规范null，不接受丢失存在文件hash。
            if state.get('exists') is False and 'sha256' not in state:
                state['sha256']=None
    if not isinstance(snapshot.get('registeredPackages'),list):
        raise Error('已解析包快照无效。')
    for package in snapshot['registeredPackages']:
        if not isinstance(package,dict):
            raise Error('已解析包快照无效。')
        package['resolvedPath']=path(package.get('resolvedPath'))
    return snapshot


def _registry(root, snapshot):
    """核对 Unity 只读快照、固定来源集合、全部源/meta/包元数据及跨源唯一页名。"""
    fields = {'projectRootHash', 'registeredPackages', 'sources', 'metadata'}
    table._keys(snapshot, fields, fields, 'registry')
    if snapshot['projectRootHash'] != table._hash(str(root).encode()):
        raise Error('登记快照不是当前项目。')
    for field in ('registeredPackages', 'sources', 'metadata'):
        if not isinstance(snapshot[field], list):
            raise Error('登记快照集合无效。')
    roots = []
    for package in snapshot['registeredPackages']:
        required = {'name', 'version', 'assetPath', 'resolvedPath', 'source'}
        table._keys(package, required, required, 'registered package')
        if any(not isinstance(package[k], str) for k in required):
            raise Error('已解析包元数据无效。')
        path = Path(package['resolvedPath']).absolute()
        if package['resolvedPath'] and package['assetPath'].startswith('Packages/') and path.is_dir():
            roots.append(path)
    physical = []
    for directory in ('UPMPackages', 'Packages'):
        parent = root / directory
        if parent.is_symlink():
            raise Error('包根目录含符号链接。')
        entries = sorted((p for p in parent.iterdir() if p.is_dir()), key=lambda p: str(p)) if parent.is_dir() else []
        for path in entries:
            _file(path / 'package.json')
        physical.append(entries)
    package_roots = list(dict.fromkeys([*physical[1], *roots]))
    candidates = [root / 'Assets/Framework/Tracks/Tracks.xlsx']
    candidates += [p / 'Tracks/Tracks.xlsx' for p in package_roots if p.name.split('@')[0] == 'com.solotopia.nova.framework']
    candidates += [p / 'Nova/Tracks/Tracks.xlsx' for p in [*physical[0], *package_roots]]
    actual = list(dict.fromkeys(str(p.absolute()).replace('\\', '/') for p in candidates if p.is_file()))
    given = [state.get('path') for state in snapshot['sources']]
    if actual != given or len(set(given)) != len(given) or not actual:
        raise Error('当前完整登记来源与 Unity 快照不同，先刷新登记快照。')
    expected_meta = [root / 'Packages/manifest.json', root / 'Packages/packages-lock.json']
    expected_meta += [Path(p) .with_name(Path(p).name + '.meta') for p in actual]
    expected_meta += [p / 'package.json' for p in dict.fromkeys([*physical[0], *package_roots])]
    metadata = [_file(p) for p in dict.fromkeys(expected_meta)]
    if metadata != snapshot['metadata'] or [_file(Path(p)) for p in actual] != snapshot['sources']:
        raise Error('完整源、meta 或包元数据已漂移。')
    names = {}
    for source in actual:
        book = _book(Path(source).read_bytes())
        for name in book['sheets']:
            if '$' in name or name.casefold() in names:
                raise Error('页签有 $ 或全登记来源重名，不能进行结构编辑。')
            names[name.casefold()] = source
    return names


def _owner(root, source, owner_root, snapshot):
    """要求精确既有归属；包写入仅允许 Nova 自有工作区或真实 Embedded 包。"""
    path = _path(root, source)
    if not isinstance(owner_root, str) or source != owner_root.rstrip('/') + ('/Tracks/Tracks.xlsx' if owner_root == 'Assets/Framework' else '/Nova/Tracks/Tracks.xlsx'):
        raise Error('ownerRoot 与既有 Tracks 归属不一致。')
    if owner_root == 'Assets/Framework':
        return path
    parts = PurePosixPath(owner_root).parts
    if len(parts) != 2 or parts[0] not in ('UPMPackages', 'Packages'):
        raise Error('只支持既有 Framework 或 Nova 模块包归属。')
    manifest_path = root / owner_root / 'package.json'
    try:
        manifest = json.loads(manifest_path.read_text('utf-8'))
    except (OSError, ValueError) as exc:
        raise Error('包归属缺少有效 package.json。') from exc
    name = manifest.get('name', '')
    if not re.fullmatch(r'com\.solotopia\.nova\.framework\.(?:sdk|kit)\.[a-z0-9._-]+', name):
        raise Error('结构编辑只写自有 Nova SDK/Kit 工作区，不写厂商包。')
    if parts[0] == 'Packages' and not any(p['name'] == name and p['source'] == 'Embedded' and Path(p['resolvedPath']).absolute() == root / owner_root for p in snapshot['registeredPackages']):
        raise Error('Packages 目标不是当前已解析的 Embedded 自有包。')
    return path


def _book(raw):
    """读取 Excel XML 与行值，拒绝宏、重复 ZIP、外部或未知工作表关系。"""
    try:
        import openpyxl
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            names = archive.namelist()
            if len(set(names)) != len(names) or any('vba' in n.lower() for n in names):
                raise Error('含宏或重复 ZIP 成员的工作簿不能安全读取。')
            workbook = ET.fromstring(archive.read('xl/workbook.xml'))
            rels = ET.fromstring(archive.read('xl/_rels/workbook.xml.rels'))
            targets = {r.get('Id'): r.get('Target') for r in rels if r.get('TargetMode') != 'External' and r.get('Type', '').endswith('/worksheet')}
            paths = {}
            for sheet in workbook.find(_tag('sheets')):
                target = targets.get(sheet.get('{' + REL + '}id'))
                if not target or '..' in PurePosixPath(target).parts:
                    raise Error('工作表关系无效或越界。')
                paths[sheet.get('name')] = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            parts = {i.filename: archive.read(i.filename) for i in archive.infolist()}
        book = openpyxl.load_workbook(io.BytesIO(raw), data_only=False, keep_links=True)
        sheets = {name: {'part': part, 'hasRelationships': ('/'.join(part.split('/')[:-1])+'/_rels/'+part.split('/')[-1]+'.rels') in parts, 'rows': [list(r) for r in book[name].iter_rows(values_only=True)],
                         'xml': ET.fromstring(parts[part])} for name, part in paths.items()}
        book.close()
        return {'raw': raw, 'parts': parts, 'workbook': workbook, 'rels': rels, 'sheets': sheets}
    except Error:
        raise
    except (ImportError, OSError, ValueError, KeyError, ET.ParseError, zipfile.BadZipFile) as exc:
        raise Error('工作簿不能安全读取；需要 openpyxl 与有效的普通 xlsx。') from exc


def _events(sheet):
    """验证九列事件、连续属性和 A-C 合并；保留原厂标识及所有现有文字。"""
    rows = sheet['rows']
    if not rows or tuple(rows[0][:9]) != HEADERS or any(v not in (None, '') for r in rows for v in r[9:]):
        raise Error('仅支持首行精确九列事件页，说明页仍保持原结构。')
    node = sheet['xml']
    if sheet.get('hasRelationships') or any(node.find(_tag(n)) is not None for n in ('drawing', 'legacyDrawing', 'tableParts', 'hyperlinks', 'extLst', 'dataValidations', 'conditionalFormatting', 'protectedRanges', 'sheetProtection', 'rowBreaks', 'colBreaks', 'scenarios')) or any(c.find(_tag('f')) is not None for c in node.iter(_tag('c'))):
        raise Error('含公式、图形、表对象或扩展绑定的事件页不做结构编辑。')
    events, current = {}, None
    for i, original in enumerate(rows[1:], 2):
        row = (original + [None] * 9)[:9]
        if not any(v not in (None, '') for v in row):
            raise Error('事件页中间或末尾有空行，需先明确真实结构。')
        if any(v is not None and not isinstance(v, str) for v in row):
            raise Error('登记值必须是字符串，不隐式改写现有类型。')
        if row[0]:
            if row[0] in events:
                raise Error('同页重复事件不能定位。')
            current = {'start': i, 'end': i, 'properties': {}}
            events[row[0]] = current
        if current is None:
            raise Error('属性行缺少所属事件。')
        current['end'] = i
        prop = row[3] or ''
        if prop in current['properties']:
            raise Error('同事件属性重复或缺少可唯一定位的实际属性。')
        current['properties'][prop] = i
    for merge in node.findall(_tag('mergeCells') + '/' + _tag('mergeCell')):
        match = re.fullmatch(r'([ABC])(\d+):\1(\d+)', merge.get('ref', ''))
        if not match:
            raise Error('已有合并必须仅在 A-C 同列。')
        col, start, end = ord(match[1]) - 65, int(match[2]), int(match[3])
        if not any(e['start'] <= start <= end <= e['end'] for e in events.values()) or any(rows[i-1][col] not in (None, '') for i in range(start+1,end+1)):
            raise Error('已有合并跨事件或会吞并非空说明。')
    return events


def _text(value, empty=False):
    """验证登记字符串、XML 字符与凭据材料，不执行字符串内容。"""
    table._type_value(value, 'string')
    if len(value) > 8192 or not empty and not value.strip() or re.search(r'</?(?:summary|param|returns)\b', value):
        raise Error('登记文字为空、过长或含源码 XML 标签。')
    return value


def _property(value):
    """接受明确真实的属性结构，不补预留字段或动态占位属性。"""
    fields = {'name', 'type', 'description', 'timing', 'target', 'remarks'}
    table._keys(value, fields, fields, 'property')
    no_fixed=value['name'] in ('','无固定属性')
    for key in fields:
        _text(value[key], empty=key == 'remarks' or no_fixed and key in ('name','type'))
    if no_fixed and '无固定属性' not in value['description']:
        raise Error('没有固定属性的事件需明确真实无固定属性说明，不能伪造字段。')
    if table._private(value['name']) or '<' in value['name'] or '>' in value['name']:
        raise Error('敏感或动态占位属性不进入结构草稿。')
    return ['', '', '', value['name'], value['type'], value['description'], value['timing'], value['target'], value['remarks']]


def _merges(rows, events):
    """按每个事件与后续非空说明分段，仅合并 A-C，不吞任何非空值。"""
    ranges = []
    for event in events.values():
        for col in range(3):
            segment = event['start']
            for i in range(segment+1, event['end']+2):
                if i == event['end']+1 or rows[i-1][col] not in (None, ''):
                    if i-segment > 1:
                        ranges.append(f'{chr(65+col)}{segment}:{chr(65+col)}{i-1}')
                    segment = i
    return ranges


def _styles(book):
    """只追加 Network 规范样式，不覆盖既有 styles 或其他页引用的样式编号。"""
    part = 'xl/styles.xml'
    if part not in book['parts']:
        raise Error('工作簿缺少既有 styles.xml，不能安全追加样式。')
    root = ET.fromstring(book['parts'][part])
    def append(container, child):
        """复用等价样式节点，避免日常执行无限追加相同样式。"""
        parent = root.find(_tag(container))
        if parent is None:
            raise Error('styles.xml 缺少标准样式集合。')
        data = ET.tostring(child)
        for i, old in enumerate(parent):
            if ET.tostring(old) == data:
                return i
        identity = len(parent)
        parent.append(child)
        parent.set('count', str(len(parent)))
        return identity
    fonts = []
    for name, header in [('Calibri', False), ('Calibri', True), ('宋体-简', False)]:
        font = ET.Element(_tag('font'))
        ET.SubElement(font, _tag('name'), val=name)
        ET.SubElement(font, _tag('charset'), val='134')
        ET.SubElement(font, _tag('sz'), val='11')
        if header:
            ET.SubElement(font, _tag('b'), val='1')
        ET.SubElement(font, _tag('color'), **({'rgb':'FFFFFFFF'} if header else {'theme':'1'}))
        fonts.append(append('fonts', font))
    fills = []
    for color in ['FFEAF4EC','FF1F4E78']:
        fill = ET.Element(_tag('fill'))
        pattern = ET.SubElement(fill, _tag('patternFill'), patternType='solid')
        ET.SubElement(pattern, _tag('fgColor'), rgb=color)
        ET.SubElement(pattern, _tag('bgColor'), indexed='64')
        fills.append(append('fills', fill))
    border = ET.Element(_tag('border'))
    for side in ['left','right','top','bottom']:
        edge = ET.SubElement(border, _tag(side), style='thin')
        ET.SubElement(edge, _tag('color'), rgb='FFB7C7D6')
    ET.SubElement(border, _tag('diagonal'))
    border_id = append('borders', border)
    ids = []
    for kind in ['header','data','identity','description']:
        xf = ET.Element(_tag('xf'), numFmtId='0', fontId=str(fonts[1 if kind=='header' else 2 if kind=='description' else 0]),
                        fillId=str(fills[1 if kind=='header' else 0]), borderId=str(border_id), xfId='0', pivotButton='0', quotePrefix='0')
        if kind != 'data':
            xf.set('applyAlignment','1')
            align = ET.SubElement(xf,_tag('alignment'), horizontal='center' if kind=='header' else 'left', vertical='center' if kind=='header' else 'top')
            if kind in ('header','description'):
                align.set('wrapText','1')
        ids.append(append('cellXfs', xf))
    return _xml(root), ids


def _rebuild(sheet, rows, ids):
    """仅重建明确授权事件页的单元格/合并/查看样式；保留该页其余受支持节点。"""
    node = copy.deepcopy(sheet['xml'])
    for name in ('dimension','sheetViews','sheetFormatPr','cols','sheetData','autoFilter','mergeCells'):
        old = node.find(_tag(name))
        if old is not None:
            node.remove(old)
    children = []
    children.append(ET.Element(_tag('dimension'),ref=f'A1:I{len(rows)}'))
    views = ET.Element(_tag('sheetViews'))
    view = ET.SubElement(views,_tag('sheetView'),workbookViewId='0')
    ET.SubElement(view,_tag('pane'),ySplit='1',topLeftCell='A2',activePane='bottomLeft',state='frozen')
    children.append(views)
    children.append(ET.Element(_tag('sheetFormatPr'),defaultRowHeight='16.8'))
    cols = ET.Element(_tag('cols'))
    for i,width in enumerate([28,18,36,40,14,58,32,16,48],1):
        ET.SubElement(cols,_tag('col'),min=str(i),max=str(i),width=str(width),customWidth='1')
    children.append(cols)
    data = ET.Element(_tag('sheetData'))
    for i,row in enumerate(rows,1):
        target = ET.SubElement(data,_tag('row'),r=str(i),ht='17' if i==1 else '16.8',customHeight='1')
        for col,value in enumerate((row+[None]*9)[:9]):
            cell = ET.SubElement(target,_tag('c'),r=f'{chr(65+col)}{i}',s=str(ids[0 if i==1 else 2 if col<2 else 3 if col==2 else 1]),t='inlineStr')
            text = ET.SubElement(ET.SubElement(cell,_tag('is')),_tag('t'),{'{http://www.w3.org/XML/1998/namespace}space':'preserve'})
            text.text = value or ''
    children += [data,ET.Element(_tag('autoFilter'),ref=f'A1:I{len(rows)}')]
    probe = {'rows':rows,'xml':ET.Element(_tag('worksheet'))}
    merges = _merges(rows,_events(probe))
    if merges:
        merge = ET.Element(_tag('mergeCells'),count=str(len(merges)))
        for ref in merges:
            ET.SubElement(merge,_tag('mergeCell'),ref=ref)
        children.append(merge)
    # sheetPr 需位于 dimension 之前，其余被保留的打印等节点在结构主体之后。
    offset = 1 if node.find(_tag('sheetPr')) is not None else 0
    for child in children:
        node.insert(offset,child)
        offset += 1
    return _xml(node)


def _apply(book, operations, all_names):
    """执行有限强类型结构草稿：新增事件/属性/事件页、定向文字和目标、统一事件页样式。"""
    if not isinstance(operations,list) or not 1 <= len(operations) <= 128:
        raise Error('结构计划需 1 至 128 个操作。')
    book = copy.deepcopy(book)
    if any(n.lower().endswith('.bin') and not n.startswith('xl/printerSettings/') for n in book['parts']):
        raise Error('目标源含不支持的二进制扩展，结构写入保持只读。')
    # 其他页的公式、图表和计算链也可能引用移动区域；不能仅保留旧坐标而宣称结构闭合。
    if any(n=='xl/calcChain.xml' or n.startswith('xl/charts/') for n in book['parts']) or any(c.find(_tag('f')) is not None for sheet in book['sheets'].values() for c in sheet['xml'].iter(_tag('c'))):
        raise Error('含跨页公式、图表或计算链的工作簿不进行结构编辑。')
    touched, summaries, change_cells = {}, [], set()
    for operation in operations:
        if not isinstance(operation,dict):
            raise Error('operation 必须是对象。')
        kind, name = operation.get('kind'),operation.get('sheet')
        _text(name)
        if '$' in name or len(name)>31 or any(c in name for c in '[]:*?/\\'):
            raise Error('页名必须符合 Excel 规范且无 $。')
        common = {'kind','sheet'}
        if kind == 'newSheet':
            table._keys(operation,common,common,'newSheet')
            if not name.startswith('打点表-') or name.casefold() in all_names:
                raise Error('新事件页需打点表-前缀且全登记源唯一。')
            all_names = {**all_names,name.casefold():''}
            index = 1
            while f'xl/worksheets/sheet{index}.xml' in book['parts']:
                index += 1
            part = f'xl/worksheets/sheet{index}.xml'
            relid = 'rIdNovaTracks'+str(index)
            while any(r.get('Id')==relid for r in book['rels']):
                relid += '_'
            sheetid = max([int(s.get('sheetId')) for s in book['workbook'].find(_tag('sheets'))],default=0)+1
            ET.SubElement(book['workbook'].find(_tag('sheets')),_tag('sheet'),name=name,sheetId=str(sheetid),**{'{'+REL+'}id':relid})
            ET.SubElement(book['rels'],'{'+PKG+'}Relationship',Id=relid,Type=REL+'/worksheet',Target='worksheets/sheet'+str(index)+'.xml')
            types=ET.fromstring(book['parts']['[Content_Types].xml'])
            ET.SubElement(types,'{'+CT+'}Override',PartName='/'+part,ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml')
            book['parts']['[Content_Types].xml']=_xml(types)
            book['sheets'][name]={'part':part,'rows':[list(HEADERS)],'xml':ET.Element(_tag('worksheet'))}
        if name not in book['sheets']:
            raise Error('目标事件页不存在。')
        sheet=book['sheets'][name]
        # 移位需要完整更新打印区域等定义名；未知绑定保持阻断，绝不改错作用范围。
        sheet_index=list(book['sheets']).index(name)
        for defined in book['workbook'].findall(_tag('definedNames')+'/'+_tag('definedName')):
            text=defined.text or ''
            if defined.get('localSheetId')==str(sheet_index) or name+'!' in text or "'"+name.replace("'","''")+"'!" in text:
                if defined.get('name')!='_xlnm._FilterDatabase' or defined.get('localSheetId')!=str(sheet_index):
                    raise Error('目标事件页有定义名或打印区域绑定，结构编辑保持只读。')
        events=_events(sheet)
        rows=[(r+[None]*9)[:9] for r in sheet['rows']]
        if kind == 'appendEvent':
            fields=common|{'eventName','chineseName','description','properties'}
            table._keys(operation,fields,fields,'appendEvent')
            event=operation['eventName']
            _text(event);_text(operation['chineseName']);_text(operation['description'])
            if event in events or table._private(event) or '<' in event or '>' in event:
                raise Error('新事件标识重复、敏感或为动态占位。')
            props=operation['properties']
            if not isinstance(props,list) or not 1<=len(props)<=128:
                raise Error('新事件须显式给出真实属性行；无固定属性使用真实无固定属性说明行。')
            additions=[_property(p) for p in props]
            if len(additions)>1 and any(r[3] in ('','无固定属性') for r in additions):
                raise Error('无固定属性说明不能与实际属性混合登记。')
            additions[0][:3]=[event,operation['chineseName'],operation['description']]
            rows+=additions
        elif kind == 'appendProperties':
            fields=common|{'eventName','properties'}
            table._keys(operation,fields,fields,'appendProperties')
            event=events.get(operation['eventName'])
            if not event or not isinstance(operation['properties'],list) or not 1<=len(operation['properties'])<=128:
                raise Error('目标事件不存在或属性草稿无效。')
            additions=[_property(p) for p in operation['properties']]
            if any(r[3] in ('','无固定属性') for r in additions):
                raise Error('已有属性事件不能追加伪造的无固定属性行。')
            if '' in event['properties'] or '无固定属性' in event['properties']:
                raise Error('无固定属性事件改为有固定属性需先明确语义，不能保留假属性。')
            rows[event['end']:event['end']]=additions
        elif kind == 'setCell':
            fields=common|{'eventName','propertyName','column','beforeValue','value','targetEvidence'}
            allowed=fields
            required=fields-{'targetEvidence'}
            table._keys(operation,allowed,required,'setCell')
            event=events.get(operation['eventName'])
            if not event or operation['propertyName'] not in event['properties'] or operation['column'] not in table.TRACKS_EDITABLE|{'目标'}:
                raise Error('只改现有复合身份的文字与精确授权目标。')
            col=HEADERS.index(operation['column']);row=event['properties'][operation['propertyName']]
            if col<3:
                while row>event['start'] and rows[row-1][col] in (None,''):
                    row-=1
            _text(operation['value'],empty=True)
            if rows[row-1][col] != operation['beforeValue']:
                raise Error('目标原值已漂移。')
            if operation['column']=='目标':
                _text(operation.get('targetEvidence'))
            elif 'targetEvidence' in operation:
                raise Error('targetEvidence 仅适用于目标修改。')
            identity=(name,row,col)
            if identity in change_cells:
                raise Error('草稿不能重复写入同一说明首格。')
            change_cells.add(identity)
            rows[row-1][col]=operation['value']
        elif kind == 'normalizeStyle':
            table._keys(operation,common,common,'normalizeStyle')
        elif kind != 'newSheet':
            raise Error('不支持删除、重命名、任意单元格或未知结构操作。')
        sheet['rows']=rows
        # 插入后旧合并坐标不再作为下一草稿操作的身份依据；最终统一按未丢失的文字重算。
        old_merge=sheet['xml'].find(_tag('mergeCells'))
        if old_merge is not None:
            sheet['xml'].remove(old_merge)
        _events({'rows':rows,'xml':ET.Element(_tag('worksheet'))})
        touched[name]=sheet
        summaries.append({'kind':kind,'sheet':name,'eventName':operation.get('eventName'),'propertyName':operation.get('propertyName'),'column':operation.get('column')})
    for name,sheet in touched.items():
        index=list(book['sheets']).index(name)
        for defined in book['workbook'].findall(_tag('definedNames')+'/'+_tag('definedName')):
            if defined.get('name')=='_xlnm._FilterDatabase' and defined.get('localSheetId')==str(index):
                # 精确唯一支持内置筛选区域，与本次正式 autoFilter 范围同步；打印等其它定义名仍拒写。
                defined.text="'"+name.replace("'","''")+"'!$A$1:$I$"+str(len(sheet['rows']))
    style,ids=_styles(book)
    book['parts']['xl/styles.xml']=style
    for sheet in touched.values():
        if len(sheet['rows']) < 2 or len(sheet['rows']) > 65536:
            raise Error('最终事件页必须包含真实事件且不超过 65536 行。')
        book['parts'][sheet['part']]=_rebuild(sheet,sheet['rows'],ids)
    book['parts']['xl/workbook.xml']=_xml(book['workbook']) if _xml(book['workbook'])!=_xml(_book(book['raw'])['workbook']) else book['parts']['xl/workbook.xml']
    book['parts']['xl/_rels/workbook.xml.rels']=_xml(book['rels']) if any(o['kind']=='newSheet' for o in operations) else book['parts']['xl/_rels/workbook.xml.rels']
    stream=io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(book['raw'])) as old,zipfile.ZipFile(stream,'w') as target:
        target.comment=old.comment
        for info in old.infolist():
            target.writestr(info,book['parts'].pop(info.filename))
        for name,raw in sorted(book['parts'].items()):
            info=zipfile.ZipInfo(name,date_time=(1980,1,1,0,0,0))
            info.compress_type=zipfile.ZIP_DEFLATED
            target.writestr(info,raw)
    updated=stream.getvalue()
    _book(updated)
    before=_book(book['raw'])['parts'];after=_book(updated)['parts']
    changes=[n for n in after if n not in before or after[n]!=before[n]]
    return updated,summaries,changes


def inspect_source(root, source, registry, owner_root):
    """只读返回正式来源/页名/复合身份与整表摘要；不读取派生汇总冒充源。"""
    project=table._project_root(root)
    registry=_transport_registry(project,registry)
    _registry(project,registry)
    path=_owner(project,source,owner_root,registry)
    if str(path).replace('\\','/') not in [s['path'] for s in registry['sources']]:
        raise Error('目标未被真实 TrackRegistry 登记。')
    book=_book(path.read_bytes())
    sheets=[]
    for name,sheet in book['sheets'].items():
        try:
            events=_events(sheet)
            identities=[{'eventName':n,'properties':[p for p in e['properties'] if not table._private(p)]} for n,e in events.items() if not table._private(n)]
            sheets.append({'sheet':name,'layout':'tracks-events','events':identities})
        except Error:
            sheets.append({'sheet':name,'layout':'unsupported-structure'})
    return {'status':'success','source':source,'ownerRoot':owner_root,'sourceHash':table._hash(book['raw']),'sheets':sheets}


def _code_evidence(project, evidence, registry):
    """冻结项目自有调用/载荷源码整份 hash；草稿登记不能脱离真实业务调用证据。"""
    if not isinstance(evidence,list) or not 1 <= len(evidence) <= 128:
        raise Error('codeEvidence 需 1 至 128 个实际源码摘要。')
    seen=set()
    for state in evidence:
        table._keys(state,{'path','sha256'},{'path','sha256'},'code evidence')
        relative=state['path']
        if not isinstance(relative,str):
            raise Error('代码证据路径必须是字符串。')
        if relative.startswith('Assets/'):
            path=table._relative_path(project,relative)
        else:
            parts=PurePosixPath(relative).parts
            if len(parts)<3 or parts[0] not in ('UPMPackages','Packages') or relative!='/'.join(parts) or '..' in parts or '\\' in relative or ':' in relative:
                raise Error('代码证据必须是实际自有源文件。')
            owner='/'.join(parts[:2])
            _owner(project,owner+'/Nova/Tracks/Tracks.xlsx',owner,registry)
            if any(p.casefold() in table.DENIED_PARTS-{'packages','upmpackages'} for p in parts):
                raise Error('厂商和生成代码不能作为业务源证据。')
            path=project/relative
            _file(path)
            if not path.is_file():
                raise Error('实际调用源码不存在。')
        if path.suffix!='.cs' or relative in seen:
            raise Error('代码证据必须是唯一项目自有 C# 源文件。')
        seen.add(state['path'])
        if table._hash(path.read_bytes())!=state['sha256']:
            raise Error('实际调用源码摘要已漂移。')


def plan_patch(root, request):
    """无源写入地冻结全登记 hash/meta、归属、业务证据、完整草稿、OOXML 精确写集及预期整源。"""
    fields={'source','sourceHash','ownerRoot','registry','operations','codeEvidence'}
    table._keys(request,fields,fields,'request')
    project=table._project_root(root)
    request=copy.deepcopy(request)
    request['registry']=_transport_registry(project,request['registry'])
    names=_registry(project,request['registry'])
    path=_owner(project,request['source'],request['ownerRoot'],request['registry'])
    if str(path).replace('\\','/') not in [s['path'] for s in request['registry']['sources']]:
        raise Error('目标未在真实登记源中。')
    _code_evidence(project,request['codeEvidence'],request['registry'])
    raw=path.read_bytes()
    if table._hash(raw)!=request['sourceHash']:
        raise Error('权威整表 hash 与请求不同。')
    updated,summary,parts=_apply(_book(raw),request['operations'],names)
    payload={'schemaVersion':VERSION,'projectRootHash':table._hash(str(project).encode()),'request':request,
             'expectedHash':table._hash(updated),'parts':parts,'summary':summary}
    return {'planId':table._hash(table._canonical(payload)),'payload':payload,'executionStarted':False}


def _validate(root, plan):
    """验证计划身份和严格草稿，不接受编辑状态文件扩大写集。"""
    fields={'planId','payload','executionStarted'}
    table._keys(plan,fields,fields,'plan')
    payload=plan['payload'];fields={'schemaVersion','projectRootHash','request','expectedHash','parts','summary'}
    table._keys(payload,fields,fields,'plan payload')
    if type(plan['executionStarted']) is not bool or type(payload['schemaVersion']) is not int or payload['schemaVersion']!=VERSION or payload['projectRootHash']!=table._hash(str(root).encode()) or plan['planId']!=table._hash(table._canonical(payload)):
        raise Error('计划身份、项目或执行状态不匹配。')
    request=payload['request']
    fields={'source','sourceHash','ownerRoot','registry','operations','codeEvidence'}
    table._keys(request,fields,fields,'plan request')
    if not isinstance(request['sourceHash'],str) or not re.fullmatch(r'[0-9a-f]{64}',request['sourceHash']):
        raise Error('原权威源摘要无效。')
    if not isinstance(payload['parts'],list) or not isinstance(payload['summary'],list):
        raise Error('冻结写集或动作摘要无效。')
    if not isinstance(payload['expectedHash'],str) or not re.fullmatch(r'[0-9a-f]{64}',payload['expectedHash']):
        raise Error('计划结果整表摘要无效。')
    return payload


def _summary(plan):
    """只报告动作、身份、文件/部件写集和摘要，草稿正文保存在受控计划中。"""
    p=plan['payload'];r=p['request']
    return {'planId':plan['planId'],'source':r['source'],'sourceHash':r['sourceHash'],'expectedHash':p['expectedHash'],
            'ownerRoot':r['ownerRoot'],'writeSet':[r['source']],'changedParts':p['parts'],'operations':p['summary']}


def execute_plan(root, plan_path, token, receipt_path):
    """复用同项目表源写锁，写前备份与持久 Receipt，原子提交并禁止重放已消费计划。"""
    project=table._project_root(root)
    with table._write_lock(project):
        plan=table._read_state(project,plan_path);payload=_validate(project,plan)
        if plan_path==receipt_path or plan['executionStarted'] or token!=plan['planId']:
            raise Error('计划已消费、确认 token 不匹配或 Plan/Receipt 路径冲突。')
        rebuilt=plan_patch(project,payload['request'])
        if rebuilt['payload']!=payload:
            raise Error('登记来源、结构或草稿已漂移。')
        request=payload['request'];path=_owner(project,request['source'],request['ownerRoot'],request['registry'])
        raw=path.read_bytes();updated,_,_=_apply(_book(raw),request['operations'],_registry(project,request['registry']))
        backup=STATE+'tracks-'+plan['planId']+'-backup.json'
        if backup in (plan_path,receipt_path):
            raise Error('备份路径不能覆盖计划或 Receipt。')
        # 预检全部固定状态路径，不能覆盖既有计划、Receipt 或备份。
        for relative in (backup,receipt_path):
            if table._relative_path(project,relative,state=True,exists=False).exists():
                raise Error('本次备份或 Receipt 路径已存在，禁止重放或覆盖。')
        receipt={'schemaVersion':VERSION,'plan':{**plan,'executionStarted':True},'backup':backup,'completed':False,'recovered':False}
        try:
            # 第一项持久副作用就是消费计划；之后的状态写失败也属于 partial，不能再次 Execute。
            plan['executionStarted']=True
            table._atomic_write(project,plan_path,table._canonical(plan)+b'\n',state=True)
            table._atomic_write(project,backup,table._canonical({'source':request['source'],'beforeHash':table._hash(raw),'data':base64.b64encode(raw).decode()})+b'\n',state=True,replace=False)
            table._atomic_write(project,receipt_path,table._canonical(receipt)+b'\n',state=True,replace=False)
            _registry(project,request['registry'])
            _atomic_source(project,request['source'],updated,request['sourceHash'])
            receipt['completed']=True
            table._atomic_write(project,receipt_path,table._canonical(receipt)+b'\n',state=True)
        except BaseException as exc:
            if not isinstance(exc,(Exception,KeyboardInterrupt)):
                raise
            return {'status':'partial',**_summary(plan),'plan':plan_path,'backup':backup,'receipt':receipt_path,
                    'planPersisted':table._relative_path(project,plan_path,state=True).is_file(),
                    'backupExists':table._relative_path(project,backup,state=True,exists=False).is_file(),
                    'receiptExists':table._relative_path(project,receipt_path,state=True,exists=False).is_file(),
                    'reason':'持久事务已开始；保留备份和现场，Receipt 存在时先 Verify，禁止重放 Execute。'}
        return {'status':'success',**_summary(plan),'receipt':receipt_path}


def _atomic_source(project, relative, data, expected):
    """写入自有包目标时复用原子算法；保留文件权限并紧邻 replace 重验整源 hash。"""
    import stat
    import tempfile
    path=_path(project,relative)
    fd,temporary=tempfile.mkstemp(prefix='.nova-tracks-',dir=path.parent)
    try:
        with os.fdopen(fd,'wb') as output:
            output.write(data);output.flush();os.fsync(output.fileno())
        os.chmod(temporary,stat.S_IMODE(path.stat().st_mode))
        path=_path(project,relative)
        if table._hash(path.read_bytes())!=expected:
            raise Error('原子提交前源已漂移。')
        os.replace(temporary,path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def _receipt(project, relative):
    """读取并验证完整 Receipt；验证和恢复始终绑定原归属与前后整表摘要。"""
    receipt=table._read_state(project,relative)
    fields={'schemaVersion','plan','backup','completed','recovered'}
    table._keys(receipt,fields,fields,'receipt')
    payload=_validate(project,receipt['plan'])
    if type(receipt['schemaVersion']) is not int or receipt['schemaVersion']!=VERSION or not receipt['plan']['executionStarted'] or type(receipt['completed']) is not bool or type(receipt['recovered']) is not bool or receipt['backup']!=STATE+'tracks-'+receipt['plan']['planId']+'-backup.json':
        raise Error('Receipt 无有效一次执行或备份身份。')
    request=payload['request']
    _owner(project,request['source'],request['ownerRoot'],request['registry'])
    return receipt,payload


def _rebuild_receipt(project, receipt, payload):
    """从真实备份与原始严格草稿重新生成 after bytes，不把可自洽修改的 expectedHash 视为签名。"""
    request=payload['request']
    backup=table._read_state(project,receipt['backup'])
    table._keys(backup,{'source','beforeHash','data'},{'source','beforeHash','data'},'backup')
    raw=base64.b64decode(backup['data'],validate=True)
    if backup['source']!=request['source'] or backup['beforeHash']!=request['sourceHash'] or table._hash(raw)!=request['sourceHash']:
        raise Error('备份身份或整源摘要不匹配。')
    original=_book(raw)
    names={}
    # 原始目标页名来自备份；其他来源仍读取真实当前 bytes 与其冻结摘要。
    for state in request['registry']['sources']:
        book=original if state['path']==str(project/request['source']).replace('\\','/') else _book(Path(state['path']).read_bytes())
        for name in book['sheets']:
            if name.casefold() in names or '$' in name:
                raise Error('原登记页名不唯一或不规范。')
            names[name.casefold()]=state['path']
    updated,summary,parts=_apply(original,request['operations'],names)
    if table._hash(updated)!=payload['expectedHash'] or summary!=payload['summary'] or parts!=payload['parts']:
        raise Error('原草稿不能生成冻结 after 摘要与精确写集。')
    return raw


def verify_receipt(root, relative):
    """只读核对整源、登记元数据/其他来源、精确预期结果与备份，不生成或重放。"""
    project=table._project_root(root);receipt,payload=_receipt(project,relative);request=payload['request']
    path=_path(project,request['source']);actual=table._hash(path.read_bytes());issues=[]
    expected=request['sourceHash'] if receipt['recovered'] else payload['expectedHash']
    if actual!=expected:
        issues.append('source_hash_changed')
    registry=copy.deepcopy(request['registry'])
    for state in registry['sources']:
        if state['path']==str(path).replace('\\','/'):
            state['sha256']=actual
    try:
        _code_evidence(project,request['codeEvidence'],request['registry'])
        _registry(project,registry)
        _rebuild_receipt(project,receipt,payload)
    except (Error,ValueError,KeyError):
        issues.append('registry_or_backup_changed')
    if not receipt['completed'] and not receipt['recovered']:
        issues.append('completion_evidence_missing')
    return {'status':'partial' if issues else 'success',**_summary(receipt['plan']),'receipt':relative,'fileHash':actual,'recovered':receipt['recovered'],'issues':issues}


def recover_receipt(root, relative, token):
    """仅按恢复 token 原子恢复自己执行前备份；当前源非 before/after hash 时保留现场拒绝覆盖。"""
    project=table._project_root(root)
    with table._write_lock(project):
        receipt,payload=_receipt(project,relative);request=payload['request'];path=_path(project,request['source'])
        if token!='restore:'+receipt['plan']['planId'] or receipt['recovered']:
            raise Error('恢复 token 无效或已经恢复。')
        raw=_rebuild_receipt(project,receipt,payload)
        actual=table._hash(path.read_bytes())
        if actual not in (request['sourceHash'],payload['expectedHash']):
            raise Error('用户后续编辑已出现，禁止盲目回滚覆盖。')
        registry=copy.deepcopy(request['registry'])
        for state in registry['sources']:
            if state['path']==str(path).replace('\\','/'):
                state['sha256']=actual
        _registry(project,registry)
        try:
            if actual!=request['sourceHash']:
                _atomic_source(project,request['source'],raw,actual)
            receipt['recovered']=True
            table._atomic_write(project,relative,table._canonical(receipt)+b'\n',state=True)
        except (Exception,KeyboardInterrupt):
            return {'status':'partial','receipt':relative,'reason':'恢复提交已开始；只读 Verify，不覆盖后续用户编辑。'}
        return {'status':'success','receipt':relative,'recovered':True,'fileHash':request['sourceHash']}


def main(argv=None):
    """提供稳定结构 CLI；状态输出统一到缓存，JSON 输入由文件或 stdin 提供且错误不回显值。"""
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root',required=True)
    commands=parser.add_subparsers(dest='command',required=True)
    inspect=commands.add_parser('inspect')
    inspect.add_argument('--source',required=True);inspect.add_argument('--owner-root',required=True);inspect.add_argument('--registry',required=True)
    plan=commands.add_parser('plan');plan.add_argument('--request',required=True);plan.add_argument('--plan',required=True)
    execute=commands.add_parser('execute');execute.add_argument('--plan',required=True);execute.add_argument('--confirmation-token',required=True);execute.add_argument('--receipt',required=True)
    verify=commands.add_parser('verify');verify.add_argument('--receipt',required=True)
    recover=commands.add_parser('recover');recover.add_argument('--receipt',required=True);recover.add_argument('--confirmation-token',required=True)
    args=parser.parse_args(argv)
    try:
        project=table._project_root(args.project_root)
        if args.command=='inspect':
            snapshot=table._read_state(project,args.registry)
            result=inspect_source(project,args.source,snapshot,args.owner_root)
        elif args.command=='plan':
            request=json.loads(sys.stdin.read()) if args.request=='-' else table._read_state(project,args.request)
            planned=plan_patch(project,request)
            table._atomic_write(project,args.plan,table._canonical(planned)+b'\n',state=True,replace=False)
            result={'status':'success',**_summary(planned),'plan':args.plan,'executionStarted':False}
        elif args.command=='execute':
            result=execute_plan(project,args.plan,args.confirmation_token,args.receipt)
        elif args.command=='recover':
            result=recover_receipt(project,args.receipt,args.confirmation_token)
        else:
            result=verify_receipt(project,args.receipt)
    except Error as exc:
        result={'status':'blocked','reason':str(exc)}
    except (OSError,ValueError,TypeError,KeyError,AttributeError):
        result={'status':'blocked','reason':'文件、快照或严格 JSON 无法形成安全结构操作；未回显输入。'}
    print(json.dumps(result,ensure_ascii=False,allow_nan=False))
    return 0 if result['status']=='success' else 3 if result['status']=='partial' else 2


if __name__=='__main__':
    raise SystemExit(main())
