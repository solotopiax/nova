/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Convert.File.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   类型转换工具 —— 文件编码转换
 ***************************************************************/

using System.IO;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Convert
        {
            /// <summary>
            /// 将指定目录中指定后缀名的所有文件转换为 UTF-8 无 BOM 文件。
            /// </summary>
            /// <param name="dirName">目录名称。</param>
            /// <param name="suffixInfos">后缀名信息（如：*.lua.txt）。</param>
            public static void ToNoBOMUTF8(string dirName, string suffixInfos)
            {
                string[] fileFullPaths = Util.SysIO.Directory.GetFiles(dirName, suffixInfos, SearchOption.AllDirectories);
                for (int index = 0; index < fileFullPaths.Length; index++)
                {
                    string content = Util.SysIO.File.ReadAllTextSync(fileFullPaths[index]);
                    Util.SysIO.File.WriteAllTextSync(fileFullPaths[index], content);
                }
            }
        }
    }
}
