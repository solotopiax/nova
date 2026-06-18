# Core/Collections

自定义数据结构，针对游戏运行时 GC 优化设计。

| 文件 | 类型 | 说明 |
|------|------|------|
| `NovaLinkedList.cs` | `sealed NovaLinkedList<T>` | 带节点缓存池的链表，避免 `LinkedListNode<T>` 频繁分配 GC |
| `NovaLinkedListRange.cs` | `struct NovaLinkedListRange<T>` | 链表区间（头尾节点对），用于标记链表中的一个连续片段 |
| `NovaLinkedSet.cs` | `NovaLinkedSet<T>` | 基于链表的无重复元素集合（有序，支持快速删除） |
| `NovaMultiDictionary.cs` | `NovaMultiDictionary<TKey,TValue>` | 一键多值字典（`Dictionary<TKey, List<TValue>>`封装） |
| `NovaOrderedDictionary.cs` | `NovaOrderedDictionary<TKey,TValue>` | 保持插入顺序的字典，实现 `IReadOnlyOrderedDictionary` |
| `TypeNamePair.cs` | `struct TypeNamePair` | `(Type type, string name)` 二元组，用作对象池等系统的复合 Key |
