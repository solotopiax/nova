# BestHTTP 网络埋点

## 1. 目标与边界

Best HTTP 3.0.18 与 Best TLS Security 3.0.5 在商业库内部采集请求生命周期和底层结构化错误；商业库只依赖后端无关的 `IBestHttpTelemetrySink`，不依赖 Nova、TGA 或其他上报框架。

`com.solotopia.nova.framework.besthttp` 负责在运行时自动注册 sink，并将事件原样扇出到 Nova 中所有已初始化且可用的 `ITrackPlugin`。单个插件异常会被隔离，不改变网络请求结果。

与 Girl v2 的关键差异：Girl 事件描述业务 fallback 链；本契约描述一个 BestHTTP `HTTPRequest` 及其物理 attempt。DoH provider/cache/preheat、业务命令、业务候选域名和 fallback 轮次不是商业库可可靠获知的事实，需要业务方通过 `TelemetryContext` 扩展字段关联。

## 2. 开关与自动注册

- Network Inspector 的 HTTP 区域提供“启用 BestHTTP 网络埋点”，默认开启。
- 仅检测到包含 `Best.HTTP.Telemetry.BestHttpTelemetry` 的商业库时可编辑；其他情况下置灰且不可点击。
- 开关关闭后，新事件不缓存、不派发，并清空尚未上报的启动期缓存。
- 适配包在 `AfterAssembliesLoaded` 自动注册，不要求业务写初始化代码。
- SDK 尚未初始化时最多缓存 128 条事件，满载淘汰最旧事件；SDK 就绪后按 FIFO 派发。
- 每个插件获得独立属性字典，插件修改参数不会污染其他插件。

## 3. 事件口径

| 事件 | 触发时机 | 数量口径 |
|---|---|---|
| `best_http_request_attempt` | 每次物理发送开始 | 每个 attempt 1 条 |
| `best_http_request_error` | 某个 attempt 以网络/协议异常失败 | 每个失败 attempt 最多 1 条；不代表逻辑请求最终失败 |
| `best_http_request_end` | 逻辑 `HTTPRequest` 进入唯一终态 | 每个逻辑请求恰好 1 条 |

语义约束：

- 重试、重定向、认证挑战或陈旧缓存恢复可能产生多个 attempt。
- attempt 失败后重试成功时，先有 `request_error`，最终 `request_end.result=success` 且 `recovered_by_retry=true`。
- HTTP 4xx/5xx 是已收到 HTTP 响应的终态，不产生 `request_error`；只产生 `request_end.result=http_error`。
- 叶子错误按 attempt 原子 first-write；DNS/TCP/TLS/协议层已经记录的精确错误不会被外层超时、关闭或通用 `request_error` 覆盖。

## 4. 公共与关联字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `best_http_schema_version` | int | 当前 schema 版本，固定为 `1`。 |
| `best_http_request_id` | string | 逻辑请求 ID；同一请求的所有 attempt/error/end 共用。 |
| `best_http_attempt_id` | string | 物理 attempt ID；每次重发重新生成。 |
| `best_http_attempt_index` | int | attempt 序号，从 `0` 开始。 |
| `best_http_correlation_id` | string | 调用方可选关联 ID，例如业务 fallback 链 ID。 |
| `best_http_operation_name` | string | 调用方可选业务操作名，不应包含敏感参数。 |
| `best_http_method` | string | HTTP 方法，大写形式。 |
| `best_http_scheme` | string | `http` 或 `https`。 |
| `best_http_host` | string | 当前请求 host。 |
| `best_http_port` | int | 当前请求端口。 |
| `best_http_path` | string | URL path，不含 query。 |
| `best_http_state` | string | BestHTTP 终态，如 `Finished`、`Error`、`ConnectionTimedOut`、`TimedOut`、`Aborted`。 |
| `best_http_exception_type` | string | 终态异常类型名，不包含 message 或 stack trace。 |
| `best_http_attempt_elapsed_ms` | long | 当前 attempt 已耗时，毫秒。 |

业务可通过 `HTTPRequest.TelemetryContext` 设置 `CorrelationId`、`OperationName` 和最多 16 个扩展字段。扩展 key 不得以 `best_http_` 开头；值只允许字符串、布尔和数值；字符串移除 query 并截断至 256 字符。

## 5. 终态与时序字段

以下字段主要出现在 `best_http_request_end`；没有对应事实时不写入。

| 字段 | 类型 | 说明 |
|---|---|---|
| `best_http_result` | enum-string | `success`、`http_error`、`network_error`、`timeout`、`aborted`。 |
| `best_http_status_code` | int | HTTP 状态码。 |
| `best_http_status_class` | string | `1xx` 至 `5xx`。 |
| `best_http_recovered_by_retry` | bool | 非首个 attempt 最终取得 2xx 时为 true。 |
| `best_http_total_elapsed_ms` | long | 逻辑请求总耗时。 |
| `best_http_queue_ms` | long | BestHTTP 排队累计耗时。 |
| `best_http_dns_ms` | long | DNS 累计耗时。 |
| `best_http_tcp_ms` | long | TCP 建连累计耗时。 |
| `best_http_tls_ms` | long | TLS 协商累计耗时。 |
| `best_http_ttfb_ms` | long | 等待首字节累计耗时。 |

## 6. 失败主码与底层事实

### 6.1 主错误、DNS 与 TCP

| 字段 | 类型 | 说明 |
|---|---|---|
| `best_http_leaf_error_code` | string | 当前 attempt 最精确且 first-write 的稳定叶子错误码。 |
| `best_http_leaf_exception_type` | string | 叶子失败源的异常类型名。 |
| `best_http_dns_host` | string | 系统 DNS 实际查询的 host。 |
| `best_http_dns_socket_error` | int | DNS `SocketError` 原始数值。 |
| `best_http_socket_error` | int | TCP `SocketError` 原始数值。 |
| `best_http_native_error` | int | OS native errno。 |
| `best_http_execute_ip` | string | TCP 胜出 IP，或终态失败 IP。 |
| `best_http_execute_port` | int | TCP 实际端口。 |
| `best_http_connect_attempts` | string | 有界的逐 IP 连接摘要，分号分隔。 |

DNS 叶子码：

- 固定码：`dns_query_timeout`、`dns_empty_result`、`dns_no_ipv4_ipv6_result`、`dns_resolve_failed`。
- `SocketError` 映射：`dns_<socket_error_name>`，例如 `dns_host_not_found`、`dns_try_again`、`dns_no_recovery`、`dns_no_data`、`dns_network_unreachable`。
- 未识别原始值：`dns_unknown_<raw>`。

TCP 叶子码：

- 固定码：`tcp_connect_error`、`tcp_race_timeout`、`tcp_race_all_candidates_failed`。
- `SocketError` 映射：`tcp_<socket_error_name>`，例如 `tcp_timed_out`、`tcp_connection_refused`、`tcp_connection_reset`、`tcp_host_unreachable`。
- 未识别原始值：`tcp_unknown_<raw>`。

### 6.2 TLS、证书与 OCSP

| 字段 | 类型 | 说明 |
|---|---|---|
| `best_http_tls_handler` | string | TLS 后端/实现。 |
| `best_http_tls_sni_host` | string | TLS SNI host。 |
| `best_http_tls_alpn` | string | ALPN 协商结果。 |
| `best_http_tls_version` | string | TLS 版本。 |
| `best_http_tls_cipher_suite` | string | 协商密码套件或 Framework TLS 算法描述。 |
| `best_http_tls_alert_direction` | enum-string | `peer` 或 `local`。 |
| `best_http_tls_alert_level` | int | TLS alert level 原始值。 |
| `best_http_tls_alert_description` | int | TLS alert description 原始值。 |
| `best_http_certificate_policy_errors` | int | Framework TLS `SslPolicyErrors` 位标记原值。 |
| `best_http_ocsp_response_status` | int | OCSP response status 原始值。 |
| `best_http_ocsp_certificate_status` | int | OCSP certificate status 原始值。 |
| `best_http_ocsp_revocation_reason` | int | OCSP revocation reason 原始值。 |

TLS alert 叶子码为 `tls_<peer|local>_alert_<name>`；覆盖 `close_notify`、`unexpected_message`、`bad_record_mac`、`decryption_failed`、`record_overflow`、`decompression_failure`、`handshake_failure`、`bad_certificate`、`certificate_revoked`、`certificate_expired`、`unknown_ca`、`protocol_version`、`internal_error`、`no_application_protocol` 等标准 description。未知值使用 `tls_<direction>_alert_unknown_<raw>`。没有 alert 的协商异常使用 `tls_handshake_failed`。

证书叶子码：`cert_missing_leaf`、`cert_san_mismatch`、`cert_common_name_missing`、`cert_common_name_mismatch`、`cert_not_yet_valid`、`cert_expired`、`cert_unknown_ca`、`cert_path_build_failed`、`cert_framework_name_mismatch`、`cert_framework_chain_error`。

OCSP 叶子码：

- 链与端点：`ocsp_chain_missing_issuer`、`ocsp_endpoint_missing`、`ocsp_must_staple_status_missing`。
- 网络：`ocsp_connect_timeout`、`ocsp_request_timeout`、`ocsp_wait_timeout`、`ocsp_http_transport_error`、`ocsp_http_aborted`、`ocsp_http_status_<status>`。
- 载荷/校验：`ocsp_content_type_invalid`、`ocsp_der_noncanonical`、`ocsp_response_empty`、`ocsp_serial_mismatch`、`ocsp_issuer_name_hash_mismatch`、`ocsp_issuer_key_hash_mismatch`、`ocsp_responder_signer_not_found`、`ocsp_signature_invalid`。
- 证书状态：`ocsp_certificate_revoked`、`ocsp_status_unknown`、`ocsp_certificate_status_unknown_<raw>`、`ocsp_response_status_<raw>`、`ocsp_signer_not_yet_valid`、`ocsp_signer_expired`。

### 6.3 代理与 HTTP/2

| 字段 | 类型 | 说明 |
|---|---|---|
| `best_http_proxy_http_connect_status` | int | HTTP CONNECT 原始状态码。 |
| `best_http_socks_method` | int | SOCKS5 method 原始值。 |
| `best_http_socks_auth_status` | int | SOCKS5 认证状态原始值。 |
| `best_http_socks_reply` | int | SOCKS5 REP 原始值。 |
| `best_http_http2_error_code` | uint | RST_STREAM 或 GOAWAY error code 原始值。 |
| `best_http_http2_last_stream_id` | uint | GOAWAY last stream ID。 |
| `best_http_protocol` | string | 实际 HTTP 协议。 |
| `best_http_connection_reused` | bool | 是否复用已有连接。 |

代理叶子码：

- HTTP CONNECT：`proxy_http_connect_status_<status>`。
- SOCKS method：`socks_method_gssapi_unsupported`、`socks_method_no_acceptable_methods`、`socks_method_unknown_<raw>`。
- SOCKS auth：`socks_auth_status_<raw>`。
- SOCKS reply：`socks_reply_general_server_failure`、`socks_reply_connection_not_allowed`、`socks_reply_network_unreachable`、`socks_reply_host_unreachable`、`socks_reply_connection_refused`、`socks_reply_ttl_expired`、`socks_reply_command_not_supported`、`socks_reply_address_type_not_supported`、`socks_reply_unknown_<raw>`。

HTTP/2 叶子码：

- Peer 错误：`http2_<rst_stream|goaway>_<name>`；标准名包括 `no_error`、`protocol_error`、`internal_error`、`flow_control_error`、`settings_timeout`、`stream_closed`、`frame_size_error`、`refused_stream`、`cancel`、`compression_error`、`connect_error`、`enhance_your_calm`、`inadequate_security`、`http_1_1_required`；未知值为 `http2_<frame>_unknown_<raw>`。
- 状态机/帧：`http2_ping_ack_timeout`、`http2_settings_ack_timeout`、`http2_frame_header_incomplete`、`http2_frame_payload_incomplete`、`http2_hpack_raw_string_offset_invariant`、`http2_header_frame_unexpected_type_<raw>`、`http2_huffman_unsupported_bit_<raw>`。

### 6.4 HTTP/1、解压与上传

HTTP/1 叶子码：`http1_invalid_version`、`http1_invalid_status_code`、`http1_invalid_content_length`、`http1_invalid_chunk_size`、`http1_peer_closed_before_status_line`、`http1_peer_closed_during_headers`、`http1_peer_closed_before_content`、`http1_peer_closed_during_content`、`http1_fixed_body_incomplete`、`http1_chunk_length_incomplete`、`http1_chunk_data_incomplete`、`http1_chunk_terminator_incomplete`、`http1_chunk_trailing_headers_incomplete`、`http1_chunk_framing_incomplete`。

解压叶子码：

- Brotli：`brotli_invalid_data`。
- gzip：`gzip_header_incomplete`、`gzip_header_invalid_signature_or_method`、`gzip_header_extra_field_incomplete`、`gzip_header_text_field_incomplete`、`gzip_trailer_incomplete`、`gzip_trailer_crc_mismatch`、`gzip_trailer_size_mismatch`。
- deflate/zlib：`deflate_invalid_block_type`、`deflate_stored_block_length_mismatch`、`deflate_too_many_length_or_distance_symbols`、`deflate_invalid_bit_length_repeat`、`deflate_invalid_literal_length_code`、`deflate_invalid_distance_code`、`deflate_header_checksum_mismatch`、`deflate_preset_dictionary_required`、`deflate_data_checksum_mismatch`、`deflate_unknown_compression_method_<raw>`、`deflate_invalid_window_size_<raw>`；无法进一步细分时为 `decompression_zlib_failed`。

上传叶子码：`upload_stream_read_failed`、`upload_stream_write_failed`、`upload_stream_flush_failed`、`upload_stream_wait_failed`、`upload_cancelled_by_client`、`upload_unknown_<stage>`。

### 6.5 生命周期与 WebGL

- 生命周期兜底：`request_connect_timeout`、`request_timeout`、`request_aborted_by_client`、`request_aborted_on_quit`、`request_error`。
- WebGL XHR：`webgl_xhr_error`、`webgl_xhr_timeout`、`webgl_xhr_aborted`。

这些都是兜底或平台叶子；若更深层错误已 first-write，不会覆盖它。

## 7. 不采集与不内置的字段

商业库明确不采集 URL query、header、请求/响应正文、凭证、证书内容、OCSP endpoint URL、异常 message 和 stack trace。

以下 Girl v2 字段没有被机械搬入商业库，因为底层库无法准确判断：

- 业务 fallback：主/备域名、round/candidate、业务命令名、业务链最终结果。
- Nova DoH：是否启用、provider、预热状态、缓存命中、DNS RCODE、注入 IP。
- Unity 网络环境：`Application.internetReachability`。
- 业务 timeout 参数来源、默认值与调用入口。
- response message、代理模式/host 类型等可能包含环境信息的上层描述。

需要这些关联信息时，应由 Nova 或业务请求创建处通过 `TelemetryContext.Set(...)` 加入经过脱敏的非保留字段；不要在商业库中反向依赖框架。

## 8. 验证范围

自动化验证覆盖事件常量、属性快照、无 sink 行为、sink 异常隔离、request/attempt 关联、重试恢复、HTTP 409 终态、超时去重、DNS/TLS/代理/HTTP 协议叶子映射，以及 Nova 开关、启动缓存和多插件扇出。

尚未覆盖真实 Android/iOS 弱网、真实代理服务器、TLS alert 注入和 HTTP/2 GOAWAY 故障注入；这些场景仍需设备或可控服务端验证字段是否在真实网络栈中如期到达。
