/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanTablesLoader.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban Tables 反射加载器
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Luban Tables 反射加载器。
    /// 通过反射构造 *Tables 实例（TableTables / ConfigTables），并提取其中的所有 ITable。
    /// Table / Config 等模块共用此实现，避免重复编写相同的反射加载逻辑。
    /// </summary>
    public static class LubanTablesLoader
    {
        /// <summary>
        /// 反射构造 *Tables 实例，提取所有 ITable 到字典。
        /// </summary>
        /// <param name="tablesClassName">*Tables 类的短名称（如 "TableTables"、"ConfigTables"）。</param>
        /// <param name="namespace_">命名空间。</param>
        /// <param name="loader">传递给 *Tables 构造函数的 loader 参数（透传，不引用 JSON 类型）。</param>
        /// <returns>类型到 ITable 的映射字典；失败时返回 null。</returns>
        public static Dictionary<Type, ITable> Load(string tablesClassName, string namespace_, object loader)
        {
            Type tablesType = ResolveType(tablesClassName, namespace_);
            if (tablesType == null)
            {
                Log.Error(LogTag.Base, "无法找到 {0} 类型（已搜索命名空间：{1}）", tablesClassName, namespace_ ?? "<null>");
                return null;
            }

            object tablesObj;
            try
            {
                tablesObj = Activator.CreateInstance(tablesType, loader);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Base, "反射构造 {0} 失败：{1}", tablesClassName, e);
                return null;
            }

            var result = new Dictionary<Type, ITable>();

            if (tablesObj is ILubanTables lubanTables)
            {
                IReadOnlyList<ITable> allTables = lubanTables.GetAllTables();
                for (int i = 0; i < allTables.Count; i++)
                {
                    ITable table = allTables[i];
                    result[table.GetType()] = table;
                }
            }
            else
            {
                Log.Warning(LogTag.Base, "{0} 类型 {1} 未实现 ILubanTables 接口，尝试通过反射提取属性。", tablesClassName, tablesType.FullName);
                var properties = tablesType.GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    object value = properties[i].GetValue(tablesObj);
                    if (value is ITable table)
                    {
                        result[value.GetType()] = table;
                    }
                }

                if (result.Count == 0)
                {
                    Log.Error(LogTag.Base, "LubanTablesLoader.Load 反射 fallback 未提取到任何 ITable 实例，tablesClassName={0}。", tablesClassName);
                }
            }

            return result;
        }

        /// <summary>
        /// 在指定命名空间下搜索类型，未命中时回退到全名搜索。
        /// </summary>
        /// <param name="typeName">类型短名称。</param>
        /// <param name="namespace_">命名空间。</param>
        /// <returns>找到的类型，未找到返回 null。</returns>
        private static Type ResolveType(string typeName, string namespace_)
        {
            if (!string.IsNullOrEmpty(namespace_))
            {
                Type type = Util.Assembly.GetType(string.Concat(namespace_, ".", typeName));
                if (type != null)
                {
                    return type;
                }
            }

            return Util.Assembly.GetType(typeName);
        }
    }
}
