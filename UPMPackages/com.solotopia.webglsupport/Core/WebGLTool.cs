using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR

public class WebGLTool : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [MenuItem("Tools/Attach WebGL InputField")]
    public static void CheckInputField()
    {
        List<string> allPaths = new List<string>();
        SearchDirectory("Assets/Game", allPaths);

        string[] allAssetPath = AssetDatabase.FindAssets("t:Prefab", allPaths.ToArray());
        for (int i = 0; i < allAssetPath.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(allAssetPath[i]);
            var obj = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
            if (obj == null)
            {
                continue;
            }

            var inputFields = obj.GetComponentsInChildren<InputField>();
            foreach (var inputField in inputFields)
            {
                if (inputField.GetComponent<WebGLSupport.WebGLInput>() == null)
                {
                    inputField.gameObject.AddComponent<WebGLSupport.WebGLInput>();
                }
            }
        }
        AssetDatabase.SaveAssets();
    }

    private static void SearchDirectory(string folderPath, List<string> allPaths)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        allPaths.Add(folderPath);

        IEnumerable<string> paths = System.IO.Directory.EnumerateFileSystemEntries(folderPath);
        foreach (var path in paths)
        {
            SearchDirectory(path, allPaths);
        }
    }
}

#endif
