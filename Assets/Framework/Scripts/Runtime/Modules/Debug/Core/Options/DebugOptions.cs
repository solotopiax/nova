/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugOptions.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Scripting;

namespace NovaFramework.Runtime
{
public delegate void DebugOptionsPropertyChanged(object sender, string propertyName);

#if !DISABLE_RUNTIME_DEBUGGER
[Preserve]
#endif
public partial class DebugOptions : INotifyPropertyChanged
{
    private static DebugOptions _current;

    public static DebugOptions Current
    {
        get { return _current; }
    }

#if !DISABLE_RUNTIME_DEBUGGER
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void OnStartup()
    {
        _current = new DebugOptions(); // Need to reset options here so if we enter play-mode without a domain reload there will be the default set of options.
        DebugServiceRegistry.GetService<InternalOptionsRegistry>().AddOptionContainer(Current);
    }
#endif

    public event DebugOptionsPropertyChanged PropertyChanged;
    
#if UNITY_EDITOR
    [JetBrains.Annotations.NotifyPropertyChangedInvocator]
#endif
    public void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
        {
            PropertyChanged(this, propertyName);
        }

        if (InterfacePropertyChangedEventHandler != null)
        {
            InterfacePropertyChangedEventHandler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private event PropertyChangedEventHandler InterfacePropertyChangedEventHandler;

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
    {
        add { InterfacePropertyChangedEventHandler += value; }
        remove { InterfacePropertyChangedEventHandler -= value; }
    }
}
}
