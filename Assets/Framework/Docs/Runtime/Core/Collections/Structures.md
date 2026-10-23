# Core/Collections

自定义数据结构，针对游戏运行时 GC 优化设计。

| 文件 | 类型 | 说明 |
|------|------|------|
| `NovaLinkedList.cs` | `sealed NovaLinkedList<T>` | 带节点缓存池的链表，避免 `LinkedListNode<T>` 频繁分配 GC |
| `NovaLinkedListRange.cs` | `sealed class NovaLinkedListRange<T>` | 链表区间引用（包含 First，排除 Terminal），不复制或固定节点 |
| `NovaLinkedSet.cs` | `NovaLinkedSet<T>` | 基于链表的无重复元素集合（有序，支持快速删除） |
| `NovaMultiDictionary.cs` | `NovaMultiDictionary<TKey,TValue>` | 一键多值字典，以节点范围和排他哨兵组织同键值 |
| `NovaOrderedDictionary.cs` | `NovaOrderedDictionary<TKey,TValue>` | List+Dictionary 组合保留插入顺序，以 AsReadOnly 返回接口包装 |
| `TypeNamePair.cs` | `struct TypeNamePair` | `(Type type, string name)` 二元组，用作对象池等系统的复合 Key |
