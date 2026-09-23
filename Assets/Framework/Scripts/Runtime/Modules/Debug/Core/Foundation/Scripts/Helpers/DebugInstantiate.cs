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
using UnityEngine.UI;

public static class DebugInstantiate
{
    public static T Instantiate<T>(T prefab) where T : Component
    {
        T instance = Object.Instantiate(prefab);
        ApplyTextFont(instance.gameObject);
        return instance;
    }

    public static GameObject Instantiate(GameObject prefab)
    {
        GameObject instance = Object.Instantiate(prefab);
        ApplyTextFont(instance);
        return instance;
    }

    public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        T instance = Object.Instantiate(prefab, position, rotation);
        ApplyTextFont(instance.gameObject);
        return instance;
    }

    private static void ApplyTextFont(GameObject root)
    {
        Font font = RuntimeDebugger.TextFont;
        if (font == null)
        {
            return;
        }

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].font = font;
        }
    }
}
}
