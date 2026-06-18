/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ComponentMenuPaths.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    internal static class ComponentMenuPaths
    {
        public const string PathRoot = "Nova/Debug/Foundation";
        public const string DebugServiceRegistry = PathRoot + "/Service/Service Manager";

        #region Behaviours

        public const string BehavioursRoot = PathRoot + "/Behaviours";

        public const string DestroyOnDisable = BehavioursRoot + "/Destroy On Disable";
        public const string DontDestroyOnLoad = BehavioursRoot + "/Don't Destroy On Load";
        public const string MatchTransform = BehavioursRoot + "/Match Transform";
        public const string LookAt = BehavioursRoot + "/LookAt";
        public const string MatchForwardDirection = BehavioursRoot + "/Match Forward Direction";
        public const string MatchMainCameraForwardDirection = BehavioursRoot + "/Match Forward Direction (Main Camera)";

        public const string RuntimePosition = BehavioursRoot + "/Runtime Position";
        public const string ScrollTexture = BehavioursRoot + "/Scroll Texture";
        public const string SmoothFloatBehaviour = BehavioursRoot + "/Smooth Float";
        public const string SmoothFollow2D = BehavioursRoot + "/Smooth Follow (2D)";
        public const string SpringFollow = BehavioursRoot + "/Spring Follow";
        public const string SmoothMatchTransform = BehavioursRoot + "/Match Transform (Smooth)";
        public const string SpawnPrefab = BehavioursRoot + "/Spawn Prefab";
        public const string Velocity = BehavioursRoot + "/Velocity";

        public const string SmoothOscillate = BehavioursRoot + "/Smooth Oscillate";

        public const string RuntimeDebuggerCamera = BehavioursRoot + "/Camera/RuntimeDebuggerCamera";

        #endregion

        #region Components

        public const string ComponentsRoot = PathRoot + "/Components";

        public const string DebugLineRenderer = ComponentsRoot + "/DebugLineRenderer";
        public const string SelectionRoot = ComponentsRoot + "/Selection Root";

        public const string DebugSpriteFadeRenderer = ComponentsRoot + "/Fade Renderer (Sprite)";
        public const string DebugMaterialFadeRenderer = ComponentsRoot + "/Fade Renderer (Material)";
        public const string DebugCompositeFadeRenderer = ComponentsRoot + "/Fade Renderer (Composite)";

        #endregion

        #region UI

        public const string UIRoot = PathRoot + "/UI";

        public const string TiltOnTouch = UIRoot + "/Tilt On Touch";
        public const string ScaleOnTouch = UIRoot + "/Scale On Touch";
        public const string InheritColour = UIRoot + "/Inherit Colour";
        public const string FlashGraphic = UIRoot + "/Flash Graphic";
        public const string CopyPreferredSize = UIRoot + "/Copy Preferred Size";
        public const string CopyPreferredSizes = UIRoot + "/Copy Preferred Size (Multiple)";
        public const string CopyLayoutElement = UIRoot + "/Copy Layout Element";
        public const string CopySizeIntoLayoutElement = UIRoot + "/Copy Size Into Layout Element";
        public const string DebugText = UIRoot + "/DebugText";
        public const string Unselectable = UIRoot + "/Unselectable";
        public const string LongPressButton = UIRoot + "/Long Press Button";
        public const string ScrollToBottom = UIRoot + "/Scroll To Bottom Behaviour";

        public const string FlowLayoutGroup = UIRoot + "/Layout/Flow Layout Group";
        public const string VirtualVerticalLayoutGroup = UIRoot + "/Layout/VerticalLayoutGroup (Virtualizing)";

        public const string StyleRoot = UIRoot + "/Style Root";
        public const string StyleComponent = UIRoot + "/Style Component";

        public const string ResponsiveEnable = UIRoot + "/Responsive (Enable)";
        public const string ResponsiveResize = UIRoot + "/Responsive (Resize)";

        public const string RetinaScaler = UIRoot + "/Retina Scaler";
        public const string NumberButton = UIRoot + "/DebugNumberButton";
        public const string NumberSpinner = UIRoot + "/DebugNumberSpinner";
        public const string DebugSpinner = UIRoot + "/Spinner";
        public const string ContentFitText = UIRoot + "/Content Fit Text";

        #endregion
    }
}
