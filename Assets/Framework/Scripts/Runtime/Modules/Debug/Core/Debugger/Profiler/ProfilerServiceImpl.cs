/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProfilerServiceImpl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System.Diagnostics;
    using UnityEngine;

    public class ProfilerServiceImpl : DebugServiceBase<IProfilerService>, IProfilerService
    {
        public float AverageFrameTime { get; private set; }
        public float LastFrameTime { get; private set; }

        public CircularBuffer<ProfilerFrame> FrameBuffer
        {
            get { return _frameBuffer; }
        }

        private const int FrameBufferSize = 400;

        private readonly CircularBuffer<ProfilerFrame>
            _frameBuffer = new CircularBuffer<ProfilerFrame>(FrameBufferSize);

        private ProfilerLateUpdateListener _lateUpdateListener;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        // Time between first Update() and last LateUpdate().
        private double _updateDuration;

        // Time that first camera rendered.
        private double _renderStartTime;

        // Time between first camera prerender and last camera postrender.
        private double _renderDuration;

        private int _camerasThisFrame;

        protected override void Awake()
        {
            base.Awake();
            _lateUpdateListener = gameObject.AddComponent<ProfilerLateUpdateListener>();
            _lateUpdateListener.OnLateUpdate = OnLateUpdate;

            CachedGameObject.hideFlags = HideFlags.NotEditable;
            CachedTransform.SetParent(Hierarchy.Get("RuntimeDebugger"), true);

            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;
        }

        protected override void Update()
        {
            base.Update();

            _camerasThisFrame = 0;

            EndFrame();

            // Set the frame time for the last frame
            if (FrameBuffer.Count > 0)
            {
                var frame = FrameBuffer.Back();
                frame.FrameTime = Time.unscaledDeltaTime;
                FrameBuffer[FrameBuffer.Count - 1] = frame;
            }

            LastFrameTime = Time.unscaledDeltaTime;

            var frameCount = Mathf.Min(20, FrameBuffer.Count);

            var f = 0d;
            for (var i = 0; i < frameCount; i++)
            {
                f += FrameBuffer[FrameBuffer.Count - 1 - i].FrameTime;
            }

            AverageFrameTime = (float) f / frameCount;

            _stopwatch.Start();
        }

        protected void PushFrame(double totalTime, double updateTime, double renderTime)
        {
            //UnityEngine.Debug.Log("Frame: u: {0} r: {1}".Fmt(updateTime, renderTime));

            _frameBuffer.PushBack(new ProfilerFrame
            {
                OtherTime = totalTime - updateTime - renderTime,
                UpdateTime = updateTime,
                RenderTime = renderTime
            });
        }

        private void OnLateUpdate()
        {
            _updateDuration = _stopwatch.Elapsed.TotalSeconds;
        }

        private void OnCameraPreRender(Camera cam)
        {
            if (_camerasThisFrame == 0)
            {
                _renderStartTime = _stopwatch.Elapsed.TotalSeconds;
            }

            _camerasThisFrame++;
        }

        private void OnCameraPostRender(Camera cam)
        {
            _renderDuration = _stopwatch.Elapsed.TotalSeconds - _renderStartTime;
        }

        private void EndFrame()
        {
            if (_stopwatch.IsRunning)
            {
                PushFrame(_stopwatch.Elapsed.TotalSeconds, _updateDuration, _renderDuration);

                _stopwatch.Reset();
                _stopwatch.Start();
            }

            _updateDuration = _renderDuration = 0;
        }
    }
}
