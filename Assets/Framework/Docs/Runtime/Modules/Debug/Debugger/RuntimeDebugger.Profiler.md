# RuntimeDebugger Profiler

`IProfilerService` 暴露帧缓冲和平均帧时间；内置 Built-in 与 SRP 两种实现测量脚本/渲染回调之间的 CPU 时间片段，不能据此判断真实 GPU 执行时间或设备独占内存。

## 帧时间链

ProfilerServiceSelector 在服务创建时依据 `GraphicsSettings.defaultRenderPipeline` 选择 `ProfilerServiceImpl` 或 `ScriptableRenderPipelineProfilerService`，不会随之后质量档或管线变化自动重建。Built-in 订阅 Camera.onPreRender/onPostRender，SRP 订阅 beginContextRendering 并等待帧末；LateUpdate 由新增 ProfilerLateUpdateListener 同步回调。

每次 Update 结束上一轮 Stopwatch 时间，加入一项帧结构，再用 unscaledDeltaTime 补该项 FrameTime，并计算最近最多 20 项均值；缓冲容量为 400。首轮尚无帧时平均计算可能为 NaN。OtherTime=total-update-render，重叠测量或外部输入可产生负数，未做有限性/非负校验；多相机、编辑器额外相机和回调顺序都会影响时间片段。

FrameBuffer 是原 `CircularBuffer<ProfilerFrame>`，外部可 Push/Pop/Clear，不是只读快照；满时枚举和部分 Back 状态的限制见 [Console 缓冲边界](RuntimeDebugger.Console.md#circularbuffer-的实际边界)。Profiler 的全局相机/SRP 事件没有独立 OnDestroy 解订阅实现；等待帧末协程依赖 Unity 对象生命周期结束。

FPS Label 以 `1 / AverageFrameTime` 格式化，触发器约每 0.5 秒更新，独立标签默认每秒更新；0/NaN 会影响展示，不保证首帧存在有效 FPS。

## 图表

ProfilerGraphControl 每次 Update 标记顶点重绘，根据控件尺寸与 target FPS 计算可见帧数和比例，倒序读取帧，按 Update/Render/Other 堆叠矩形并生成坐标线。小于约 4 像素的分段会略过，坐标不等于精确采样积分；TopAlignment、Clip、轴标签和当前实现的实际裁剪并非全部一致，y 不受 Clip 完整约束。

targetFps 必须为有效正值，LineColours 至少支持三个时间段；非有限时间、太小/负尺寸、无服务、缺轴标签资源可能造成无效比例或异常。编辑器非 Play 使用 Perlin/Random 生成 TestData，并消耗 Unity 全局 Random 状态，不能当实际性能结果。当前/Legacy UI 网格分支只改变顶点提交方式，不补充数值校验。

## 内存页面与采样

ProfilerTabController 每帧刷新 Unity API 的状态与内存摘要；采样按钮另行同步执行全对象扫描。`TakeSample()` 先记录模式/时间并清缓存，然后按总览或指定类型采样，失败可留下部分新结果，没有分页、取消或事务回滚。

- 总览扫描 `Resources.FindObjectsOfTypeAll<Object>()`，按 `Type.Name` 汇总数量和 `GetRuntimeMemorySizeLong`；包括未激活对象、资源及 Editor 对象，同短类名可能合并，共享资源估计可能重复计入。
- 类型列表按估计大小降序，再按类型/名称排序；同 Name/Type/Size 的后续相邻项标为疑似重复。这不是内容一致、内存泄漏或重复占用的证明。
- 显示读取采样缓存而非实时重采。列表名称、类型和数值没有自动脱敏或 rich text 转义；MemoryRowView 错类型仅清文字，旧颜色未统一复位。
- 字节显示以 1024 缩放，部分 MB 显示先整数截断，超大 long 转 float 会失精度；不是精确的原生/GPU 独占占用。
- CreateProfilerBlock/CreateMemoryBlock 直接配置已有对象引用，并不 Instantiate；ApplyLayoutElementSize 当前为空操作。MemorySummaryBlock 初始高度不超过 60 时移动采样/滚动区，一次执行，不随之后尺寸自动重算。

ProfilerMemoryBlock 的清理动作会执行 GC.Collect、等待 UnloadUnusedAssets，再 Collect/刷新；这是影响全项目的实际动作，可卡顿且不保证全部资源释放，重复触发也没有统一 busy 门。ProfilerMonoBlock 用 Mono 堆 API，部分不支持路径回退 GC.GetTotalMemory；Used=0 既可能是数值也可能影响支持判定，不能直接等同不可用。

ProfilerEnableControl 读取 Unity Profiler 支持状态并切换全局 `Profiler.enabled`；页面显示开关和固定 Profiler 的开关并不等于该全局开关。许可判断沿用当前源码分支，不能从旧版提示推导所有 Unity 版本都要求同一许可证。

源码：[ProfilerServiceImpl.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/Profiler/ProfilerServiceImpl.cs)、[ProfilerTabController.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/UI/Tabs/ProfilerTabController.cs)、[ProfilerGraphControl.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/UI/Controls/ProfilerGraphControl.cs)。
