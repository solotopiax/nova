# Player TMP 单 atlas 正常清理合同

`nova.project.player.build` 仍冻结完整真实回调集合、准确包源与 DLL 配对。TMP 只支持当前已审的 Android/WebGL 编译配对，未知身份保持 blocked。该合同不扩展到引擎 TextCore `FontAsset`。

字体、源 Font、atlas 与登记行的运行身份保存为完整 `ulong` EntityId，集合和登记顺序图使用同一 64 位类型；名称/家族/样式等业务 hash 仍为各自原整数。反查只比较当前已加载对象的 EntityId，不加载对象补证据；私有源、包/DLL/MVID 与平台准入不因身份 API 兼容而放宽。

正式 Plan 可以通过 `AssetDatabase.LoadAssetAtPath` 合法加载已有字体。这可能触发已审的 `Awake` / `OnValidate`，初始化 lookup、ResourceManager、弱登记和 FontEngine 当前字体面；因此不能称为零内存副作用。首次 Load 前排除旧版本升级和特征重导入，观察 Load 错误，并核对资产/meta 字节未变。旧 schema 未序列化的 `m_ShouldReimportFontFeatures` 按已审 CLR 默认 false；重复、未知标量或二进制文件拒绝首次 Load。源/meta 原文和像素只计算摘要，不进入 Plan/receipt。

自然 `clear=false` / Static 分支保持只读输入。自然 `atlas.width==0` 支持已审 getter 的引用缓存赋值和原过滤早退，绝不先清空 atlas。正宽度分支只接受 Dynamic、version1.1.0、已完整序列化 FaceInfo、无升级/特征导入/旧 kerning、空 glyph/character 表、规范 free rect，以及同资产已有的单个可读 1×1 Alpha8 零纹理。源 Font 必须为既有持久引用，不接受任意路径或 OS 字体。

真正清理由官方 TMP 回调执行。正常效果包括 atlas 的 `Reinitialize` / `ResetAtlasTexture` / `Apply`、lookup 和固定控制字符缓存重建、FontEngine 当前字体面/scratch、资源登记，以及正常 EditorResourceManager singleton 和自己的 Camera/Canvas 渲染回调注册。原 FontEngine 当前字体面的精确身份没有公开恢复 API，因此这里明确保留正常原生内存效果，不假称恢复原生状态。字体完整序列化 SHA、资产/meta/源 Font/依赖、atlas 持久身份、尺寸和零像素摘要必须保持不变。

已有 manager 保留真实对象引用；自然 null manager 只能由官方路径构造。只允许既有 Builtin 管线分支的准确自身 Camera/Canvas 条目，其他事件的有序完整身份保持不变。Update 队列只能包含本组批准字体且 list/EntityId lookup 一致；重导入与定义刷新队列必须为空。自然渲染消费 Update 时，已审源码只设置两个 proxy flag 并清 Update 队列；不强制渲染、Flush 或调用私有回调。资源登记额外保存三张 map 的完整实际行和含 dead 槽的完整 weak 列表，不能只依赖两个 aggregate SHA。允许集仅来自本次 Prepare 已明确分类的正常清理、Static/clear=false 或零宽 marker，且必须在原 before 中实际已加载：冻结 GUID/fileID/path、native ID、完整 JSON、源/资产/meta/依赖与 atlas 身份及内容摘要；不保存非清理字体的强引用，也不为捕获重载/初始化字体。before 未使用且之后自然卸载的候选可以缺席；原强表或活 weak 引用的候选不能消失，后加载同 GUID 的新实例不获许可。

三表每个原 key/value 与 collision winner 必须保持。新增 EntityId 引用只接受由原 name/FaceInfo 及已 pin `TMP_TextUtilities` 算法预测的 name/family/style hash，包括 name hash 为零时的 family 回退；不能以 after 自签预测值。name/family 两 lookup 坚持 `ContainsKey` first-wins，碰撞 winner 与新增 weak 顺序必须能由同一源登记顺序产生。原 live weak 保留同一槽和身份；只允许先复用首个真实 dead 槽，再 append，重复、重排、越过空槽和未知目标均拒绝。hash backing 仅允许零到源预测值；非清理字体完整序列化、dirty、源与 atlas 不变。零宽 Dynamic/clear 分支仅另外允许 `m_AtlasTexture` 从 null 缓存为原数组首纹理，不能改像素或替换引用。

非清理 atlas 可读时核完整原字节；不可读时绑定现有 `Texture.imageContentsHash`、完整 Editor JSON、元数据、持久身份及文件 SHA，并要求原生内容 hash 有效。hash 无效具体 blocked；不设置 readable、不调用 atlas/material getter、不预清或初始化缓存。`ReadFontAssetDefinition` / `OnValidate` 可能自然登记；本合同只证明有限后态，不猜测原回调发生时刻。

Plan 的完整真实 before 在 Execute 前复核两次。只对明确正常缓存、字体/atlas 正常 dirty 增加和引用缓存做独立不变量投影，不使用投影替代写前保护。Scope 在成功或失败时捕获真实 after；schema4 保存 before/after/error 与正式链返回状态。Generate adapter 即便 Dispose 验证抛错也复制 scope 已有实际 after/error，不能用后来的 current 或 before 补造；旧未保存的 Scope.after 保持缺失历史。成功还要求 manager、准确自身事件、lookup 和控制字符后验成立。正常原生修改可以使字体或 atlas 变 dirty，但任何原有 dirty 不得被清除，也不会自动保存或 ClearDirty。Verify 同时核对原 after 和当前状态，不重放或预清；当前域对象被替换/域重载导致不能核对原内存身份时返回 partial。旧 schema2/3 仍为 partial；即使 schema4，缺少本合同完整 typed 登记行也不能升级。

FontEngine Load 失败可能读取 TMP_Settings；缺失/旧版本设置可排队一个延迟 Importer 窗口。Load 的 Error/Exception/Assert 会阻止继续构建；正式构建异常保存真实失败材料，明确可能存在该延迟 UI，不声称其异步队列已结束、不创建设置或预调用 Native 来制造通过。正常链成功且 before/after invariant 保持，才可给本轮产物成功结论。任何磁盘漂移、额外 atlas、非零像素、未知 delegate、其他队列工作或不完整后验保持 blocked/partial。

回归包含独立 DTO 的写前保护、自然创建/后验与未知变更拒绝用例，以及只读取正式 Plan 已自然加载字体的 Unity 集成检查。集成检查在字体尚未加载时明确 inconclusive，不能计作通过；它不会先调用 Clear / FontEngine / getter 预热。测试不改样例字体，不新增存档或设置，不运行真正 Player 构建。

Player 全局输入保护另对本轮精确批准的持久字体与同文件 atlas 作 dirty 存在性投影：无论 clean/dirty 均冻结完整 Editor JSON SHA。其他 dirty 对象保持原逐项规则；批准对象正文变化仍拒绝，原 dirty 不得被清除仍由 TMP 实际 before/after 保护。Plan 在合法 Load 后重采这组输入，但把初次原快照仅按同一精确对象集合投影后逐字段比较，不能重新吸收其他文件、scene、设置或 dirty 正文变化。Execute 前/后和 Verify 使用原 Plan/Receipt 的相同 GUID/fileID/path/native ID 集合；不通过运行 ID 补加载。自然 Update 队列尚未消费或之后实际 dirty 状态与回执不同仍为 partial，不宣称队列完成。

完整行核验是独立必要门：InvariantHash 只对上述有限候选行作投影，BeforeEquals 仍精确比较原完整材料；原 before→after 必须通过逐行追加证明，Verify 的 map/weak 还必须与已记录 after 完全一致，不能把后来发生的许可登记补算进原执行。两次 full typed 快照仍受原 Generate 8MiB 文件预算限制，超限明确失败，不截断材料或扩大 Store。

只有资源登记、没有正常 clear 字体的组，不共享 singleton、own callback、Update 队列或 proxy flag 投影；这些 runtime 控制字段仍完整不变，原 manager 与真实 after/current 的同域对象身份也必须保持。

同域后态采用独立冻结观察模式：Plan 与 Execute 写前继续原 Prepare/安全 Load 生命周期，Scope 结束和 Verify 不再次加载已经自然卸载的非清理字体。逐路径保留原分类、原输入行、实际原 dirty 与文件/meta/依赖绑定；当前 present 对象完整重读身份/native ID/JSON 与原合同，当前 absent 不填写伪造的 JSON、ID 或 dirty。只有原 clean、原/当前均无强资源行或活 weak、真正 Scope after 已缺席且当前仍缺席的非清理候选可缺席。原分类行明确只是已绑定历史输入；其当前磁盘和元数据仍真实复核，未知行/新增路径/分类/新实例或文件漂移均拒绝。完整回调、source/DLL/MVID 和其他输入仍重新捕获。Generate 保存的实际 Scope after 是 TMP 后验权威，较晚 callback current 只能核对它，不能替换、补登记或倒填。新域缺乏这套同域身份证明时明确 partial；此变更没有增加跨域缓存重置的允许规则，也不升级旧操作。
