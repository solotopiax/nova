/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  OptionControlFactory.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public static class OptionControlFactory
    {
        private static IList<DataBoundControl> _dataControlPrefabs;
        private static ActionControl _actionControlPrefab;

        private static readonly Dictionary<OptionType, DataBoundControl> TypeCache = new Dictionary<OptionType, DataBoundControl>();

        public static bool CanCreateControl(OptionDefinition from)
        {
            PopulateDataControlPrefabs();
            if (from.Property != null)
            {
                return TryGetDataControlPrefab(from) != null;
            }
            else
            {
                return _actionControlPrefab != null;
            }
        }

        /// <summary>
        /// Create a control from an <c>OptionDefinition</c>, optionally providing <paramref name="categoryPrefix" /> to remove
        /// the category name from the start of the control.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="categoryPrefix"></param>
        /// <returns></returns>
        public static OptionsControlBase CreateControl(OptionDefinition from, string categoryPrefix = null)
        {
            PopulateDataControlPrefabs();

            if (from.Property != null)
            {
                return CreateDataControl(from, categoryPrefix);
            }

            if (from.Method != null)
            {
                return CreateActionControl(from, categoryPrefix);
            }

            throw new Exception("OptionDefinition did not contain property or method.");
        }

        private static void PopulateDataControlPrefabs()
        {
            if (_dataControlPrefabs == null)
            {
                _dataControlPrefabs = Resources.LoadAll<DataBoundControl>(RuntimeDebuggerPaths.DataControlsResourcesPath);
            }

            if (_actionControlPrefab == null)
            {
                _actionControlPrefab =
                    Resources.LoadAll<ActionControl>(RuntimeDebuggerPaths.DataControlsResourcesPath).FirstOrDefault();
            }

            if (_actionControlPrefab == null)
            {
                Debug.LogError("[NovaFramework.Runtime.Options] Cannot find ActionControl prefab.");
            }
        }

        private static ActionControl CreateActionControl(OptionDefinition from, string categoryPrefix = null)
        {
            var control = DebugInstantiate.Instantiate(_actionControlPrefab);

            if (control == null)
            {
                Debug.LogWarning("[NovaFramework.Runtime.OptionsTab] Error creating action control from prefab");
                return null;
            }

            control.SetMethod(from.Name, from.Method);
            control.Option = from;

            return control;
        }

        private static DataBoundControl CreateDataControl(OptionDefinition from, string categoryPrefix = null)
        {
            var prefab = TryGetDataControlPrefab(from);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[NovaFramework.Runtime.OptionsTab] Can't find data control for type {0}".Fmt(from.Property.PropertyType));
                return null;
            }

            var instance = DebugInstantiate.Instantiate(prefab);

            try
            {
                var n = from.Name;

                // Remove category name from the start of the property name
                if (!string.IsNullOrEmpty(categoryPrefix) && n.StartsWith(categoryPrefix))
                {
                    n = n.Substring(categoryPrefix.Length);
                }

                instance.Bind(n, from.Property);
                instance.Option = from;
            }
            catch (Exception e)
            {
                Debug.LogError("[NovaFramework.Runtime.Options] Error binding to property {0}".Fmt(from.Name));
                Debug.LogException(e);

                Object.Destroy(instance);
                instance = null;
            }

            return instance;
        }

        private static DataBoundControl TryGetDataControlPrefab(OptionDefinition from)
        {
            OptionType type = new OptionType(@from.Property.PropertyType, !@from.Property.CanWrite);

            DataBoundControl control;
            if (!TypeCache.TryGetValue(type, out control))
            {
                control = _dataControlPrefabs.FirstOrDefault(p =>
                    p.CanBind(@from.Property.PropertyType, !@from.Property.CanWrite));
                TypeCache.Add(type, control);
            }

            return control;
        }

        private struct OptionType
        {
            public readonly Type Type;
            public readonly bool IsReadyOnly;

            public OptionType(Type type, bool isReadyOnly)
            {
                Type = type;
                IsReadyOnly = isReadyOnly;
            }

            public bool Equals(OptionType other)
            {
                return Equals(Type, other.Type) && IsReadyOnly == other.IsReadyOnly;
            }

            public override bool Equals(object obj)
            {
                return obj is OptionType other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ IsReadyOnly.GetHashCode();
                }
            }
        }
    }
}
