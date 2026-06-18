/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugInstantiate.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
using UnityEngine;

public static class DebugInstantiate
{
    public static T Instantiate<T>(T prefab) where T : Component
    {
        return (T) Object.Instantiate(prefab);
    }

    public static GameObject Instantiate(GameObject prefab)
    {
        return (GameObject) Object.Instantiate(prefab);
    }

    public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        return (T) Object.Instantiate(prefab, position, rotation);
    }
}
}
