/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BindTrackEvents.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   GameBind 埋点事件名与字段名常量
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    internal static class BindTrackEvents
    {
        public const string Bind = "nova_gamebind_bind";
        public const string QueryConflict = "nova_gamebind_query_conflict";
        public const string Resolve = "nova_gamebind_resolve";
    }

    internal static class BindTrackFields
    {
        public const string BindProvider = "nova_gamebind_bind_provider";
        public const string BindResult = "nova_gamebind_bind_result";
        public const string BindErrorCode = "nova_gamebind_bind_error_code";
        public const string BindDurationMs = "nova_gamebind_bind_duration_ms";
        public const string QueryConflictResult = "nova_gamebind_query_conflict_result";
        public const string QueryConflictErrorCode = "nova_gamebind_query_conflict_error_code";
        public const string QueryConflictDurationMs = "nova_gamebind_query_conflict_duration_ms";
        public const string ResolveChoice = "nova_gamebind_resolve_choice";
        public const string ResolveHasVerifyCode = "nova_gamebind_resolve_has_verify_code";
        public const string ResolveResult = "nova_gamebind_resolve_result";
        public const string ResolveErrorCode = "nova_gamebind_resolve_error_code";
        public const string ResolveDurationMs = "nova_gamebind_resolve_duration_ms";
        public const string OpenId = "nova_openid";
    }
}
