/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IProfilerService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine.Rendering;

    public static class ProfilerServiceSelector
    {
        [ServiceSelector(typeof(IProfilerService))]
        public static Type GetProfilerServiceType()
        {
            if(GraphicsSettings.defaultRenderPipeline != null)
            {
                return typeof(ScriptableRenderPipelineProfilerService);
            }

            return typeof(ProfilerServiceImpl);
        }
    }

    public struct ProfilerFrame
    {
        public double FrameTime;
        public double OtherTime;
        public double RenderTime;
        public double UpdateTime;
    }

    public interface IProfilerService
    {
        float AverageFrameTime { get; }
        float LastFrameTime { get; }
        CircularBuffer<ProfilerFrame> FrameBuffer { get; }
    }
}
