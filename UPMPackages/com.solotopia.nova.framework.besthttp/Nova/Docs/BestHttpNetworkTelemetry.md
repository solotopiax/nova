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
- SOCKS framing/version：`socks_method_response_length_<count>`、`socks_version_unknown_<raw>`、`socks_auth_response_length_<count>`、`socks_connect_response_length_<count>`。
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

## 7. 字段出现矩阵与完整字典

本节是字段级的详细契约；第 4～6 节可作为快速参考。

### 7.1 出现规则

| 类别 | `best_http_request_attempt` | `best_http_request_error` | `best_http_request_end` |
|---|---|---|---|
| 请求与 attempt 身份 | 必有 | 必有 | 必有 |
| URL 脱敏定位 | 必有 | 必有 | 必有 |
| `TelemetryContext` 关联字段 | 按调用方设置 | 按当前 attempt 快照 | 按最终 attempt 快照 |
| 连接复用、协议与 attempt 耗时 | 必有键，部分值可为空 | 必有键，值为失败时快照 | 必有键，值为终态时快照 |
| `state` / 异常 / 叶子失败事实 | 不写入 | 按已发生事实写入 | 按最终 attempt 已发生事实写入 |
| HTTP 状态码 | 不写入 | 不写入 | 实际收到响应时写入 |
| `result` / `recovered_by_retry` | 不写入 | 不写入 | 必有 |
| 逻辑请求总耗时与分阶段耗时 | 不写入 | 不写入 | `Timing` 可用时写入 |

`必有键` 表示源码会把该 key 放入属性字典；防御性空值仍可能在非正常调用路径出现。`不写入` 与值为 `null` 不是同一语义：前者表示当前事件没有这个事实。

### 7.2 请求、attempt 与业务关联字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_schema_version` | 埋点属性契约版本，不是 Best HTTP 包版本。 | 当前固定为整数 `1`。查询与 ETL 应先按 schema 分支，不要根据客户端版本猜测字段含义。 |
| `best_http_request_id` | 一个 `HTTPRequest` 逻辑请求的随机 ID。请求内重试、重定向或认证挑战不会改变它。 | 32 位小写十六进制 GUID，无连字符。用它聚合一次逻辑请求的所有 attempt/error/end。只在单进程内具有跟踪意义。 |
| `best_http_attempt_id` | 当前物理发送的 ID。 | 当前实现为 `<request_id>-<attempt_index>`，但消费方应当它是不透明字符串。用于把一条 `request_error` 精确连回对应的 `request_attempt`。 |
| `best_http_attempt_index` | 逻辑请求内物理发送序号。 | 从 `0` 开始。`0` 是首次发送；大于 `0` 说明至少经历过一次重发。它不等同于 Nova 业务 fallback 轮次。 |
| `best_http_correlation_id` | 调用方提供的跨层关联 ID，例如 Nova fallback chain ID。 | 可选。在 attempt 开始时快照；包含 `?` 时 query 被替换为 `?<redacted>`，最长 256 字符。用于跨 Best HTTP 请求聚合业务链。 |
| `best_http_operation_name` | 调用方提供的稳定业务操作名。 | 可选，例如 `login`、`config_download`；不应放 URL、UID 或 token。同样执行 query 脱敏和 256 字符截断。用于按业务场景统计底层错误率。 |
| 调用方扩展字段 | `TelemetryContext.Set(key, value)` 注入的非保留字段。 | 最多 16 个；key 不得以 `best_http_` 开头；value 只允许 `null`、string、bool 和基础数值类型。字符串执行同样脱敏。请为扩展字段建立独立业务契约，不要依赖临时 key。 |

### 7.3 URL 与 HTTP 定位字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_method` | 当前 attempt 的 HTTP method。 | `GET`、`POST`、`PUT`、`DELETE` 等大写字符串。用于区分读写请求和判断重试是否有幂等风险。 |
| `best_http_scheme` | 当前 `CurrentUri` 的 scheme。 | 通常为 `http` 或 `https`。当值为 `https` 但没有 TLS 字段时，应判断是否在 DNS/TCP/代理阶段已失败。 |
| `best_http_host` | 当前 attempt URL 的 host。 | 不含 scheme、port 和 path。可能因 Best HTTP 重定向而在后续 attempt 变化。它是 URL host，不一定是系统 DNS 实际查询的 `best_http_dns_host`。 |
| `best_http_port` | `Uri.Port` 返回的当前端口。 | 显式端口或 scheme 默认端口，例如 HTTP `80`、HTTPS `443`。与 `execute_port` 不一致时优先检查代理、转发或连接目标。 |
| `best_http_path` | URL 的 `AbsolutePath`。 | 例如 `/v1/user/login`；不含 query 和 fragment。可用于按接口聚合，但 path 中若嵌入 UID 等业务数据，调用方仍需自行评估隐私风险。 |
| `best_http_status_code` | 收到并解析出的 HTTP 状态码。 | 只在 `request_end` 且存在 response 时写入。缺失通常表示未建立 HTTP 响应，应转查 leaf/TCP/TLS 字段。 |
| `best_http_status_class` | 由状态码整除 `100` 得到的分组字符串。 | 常规值为 `1xx`～`5xx`。用于聚合报表；精确排障仍应使用 `status_code`。 |

### 7.4 状态、结果与异常字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_state` | Best HTTP `HTTPRequestStates` 终态名。 | 埋点终态常见 `Finished`、`Error`、`Aborted`、`ConnectionTimedOut`、`TimedOut`。`Finished` 只表示已完成 HTTP 处理，不代表 2xx；仍需联合 `result` 和 `status_code`。 |
| `best_http_result` | 逻辑请求对外统计用的稳定结果。 | `success`：2xx 或 304；`http_error`：`Finished` 但非 2xx/304；`network_error`：非 `Finished` 的普通错误；`timeout`：连接或总请求超时；`aborted`：主动中止。只出现于 `request_end`。 |
| `best_http_recovered_by_retry` | 前面已发生过物理发送，最终由非首 attempt 得到 2xx 的标记。 | `attempt_index > 0` 且最终状态为 `Finished` 且状态码为 2xx 时才是 `true`。304 虽然 `result=success`，但该字段不会因 304 置 `true`。 |
| `best_http_exception_type` | 逻辑终态或 attempt 完成时的外层异常类型名。 | 例如 `SocketException`、`TimeoutException`；不包命名空间、message 和 stack trace。空值不等于成功，超时和 abort 可以没有异常对象。 |
| `best_http_leaf_error_code` | 当前 attempt 最先被记录的精确、稳定错误码。 | 原子 first-write；一旦 DNS/TCP/TLS/协议层写入，外层 timeout/error 不覆盖。这是排障主键，必须联合同层 raw 字段。 |
| `best_http_leaf_exception_type` | 写入叶子失败事实时的底层异常类型名。 | 当前主要由 DNS 和 TCP 失败路径填充。它与 `best_http_exception_type` 可不同：前者描述根因层，后者描述终态外层。 |

### 7.5 连接、DNS 与 TCP 字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_connection_reused` | attempt 开始时是否已有可用连接/协议 handler。 | 布尔值。它表示 Best HTTP 连接对象复用，不表示 TLS session resumption。复用连接上发生 reset/GOAWAY 时，可与新建连接失败分组对比。 |
| `best_http_protocol` | 当前实际请求 handler/协议标识。 | 实现字符串，常见为 HTTP/1 或 HTTP/2 handler 名。新建连接在 `request_attempt` 发出时可尚未协商而为空，终态事件中可已有值。不要把当前字符串当作永久枚举。 |
| `best_http_dns_host` | 系统 DNS 路径实际查询的 hostname。 | 只在 DNS 失败成为首个叶子根因时写入。对比 `best_http_host` 可发现代理、CNAME 上层适配或其他路由差异。 |
| `best_http_dns_socket_error` | DNS `SocketError` 的原始整数值。 | 只在 DNS 失败是 `SocketException` 时写入。跨平台数值可不同，聚合使用 leaf code，单机深挖时再查 raw value。 |
| `best_http_socket_error` | TCP 终态失败的 `SocketError` 原始整数值。 | 只在 TCP 失败提供 `SocketError` 时写入。可区分同一 leaf 映射下的平台差异。 |
| `best_http_native_error` | OS socket native errno。 | DNS/TCP 路径可写入，即使值为 `0` 也可存在。它不适合跨 OS 聚合；应结合平台、`socket_error` 和 leaf code 解读。 |
| `best_http_execute_ip` | TCP race 胜出 IP，或最终失败 lane 的 IP。 | 可为 IPv4 或 IPv6；DNS 在更早阶段失败时不存在。按 IP 聚合可识别单节点或单地址族异常，但需注意 IP 属于环境信息。 |
| `best_http_execute_port` | TCP 实际连接端口。 | 在记录胜出端点或终态失败端点时写入。与 URL port 分开，便于识别代理/隧道场景。 |
| `best_http_connect_attempts` | TCP race 各 IP lane 的有界摘要。 | 最多 8 项，用分号分隔；单项格式为 `ip:port=result\|socketErrorRaw\|nativeError`。`result` 是 `success`、`tcp_*` 或 `tcp_connect_error`；无 `SocketError` 时中间位为空。该字段是诊断文本，不建议作为严格结构化 API 二次解析。 |

### 7.6 TLS、证书与 OCSP 字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_tls_handler` | Best HTTP 选择的 TLS 实现。 | 当前为 `bouncycastle` 或 `framework`。用于区分 Best TLS/BouncyCastle 链路与系统 `SslStream` 链路；两者证书和 cipher 字段口径不完全相同。 |
| `best_http_tls_sni_host` | 发起 TLS 时使用的 SNI/target host。 | 在 TLS handler 选定时写入。应与证书 SAN/CN 匹配；与 `execute_ip` 一起可判断“连对 IP 但 SNI/证书名不匹配”。 |
| `best_http_tls_alpn` | TLS ALPN 协商结果。 | 成功协商后写入，常见 `h2`、`http/1.1`。空值可表示协商前失败、服务端未返回 ALPN 或平台不提供。 |
| `best_http_tls_version` | 实际协商的 TLS 版本字符串。 | BouncyCastle 返回协议版本名，Framework TLS 返回 `SslProtocol.ToString()`。用于识别 TLS 1.2/1.3 或协议降级问题；不应依赖两个 handler 的字符串完全一致。 |
| `best_http_tls_cipher_suite` | 实际协商的 cipher suite/算法摘要。 | BouncyCastle 使用 `SecurityParameters.CipherSuite.ToString()` 的结果（当前通常是 cipher suite 数字字符串）；Framework TLS 使用 `<CipherAlgorithm>_<CipherStrength>`。用于识别服务端密码套件兼容性，不建议跨 handler 直接等值比较。 |
| `best_http_tls_alert_direction` | fatal TLS alert 的方向。 | `peer` 表示对端发来，`local` 表示客户端本地产生。只记录 fatal alert；用于先判断是服务端拒绝还是客户端校验/协议失败。 |
| `best_http_tls_alert_level` | TLS alert level 原始值。 | 当前记录路径仅在 fatal 时进入，通常为 `2`。保留 raw 值便于未来协议/实现差异排查。 |
| `best_http_tls_alert_description` | TLS alert description 原始数值。 | 与 `leaf_error_code=tls_<direction>_alert_*` 一起写入。leaf 用于聚合，raw 用于对照 RFC 和服务端日志。 |
| `best_http_certificate_policy_errors` | Framework TLS `SslPolicyErrors` 位标记原值。 | 仅 Framework TLS 证书 validator 拒绝且 policy error 非 None 时写入。常见位：`1` 证书不可用、`2` 名称不匹配、`4` 链错误，可按位组合。 |
| `best_http_ocsp_response_status` | OCSPResponse `responseStatus` 原值。 | 常见 `0=successful`、`1=malformedRequest`、`2=internalError`、`3=tryLater`、`5=sigRequired`、`6=unauthorized`。非 0 时通常对应 `ocsp_response_status_<raw>`。 |
| `best_http_ocsp_certificate_status` | OCSP 对目标证书的状态原值/tag。 | 当前实现中常见 `0=good`、`1=revoked`、`2=unknown`；仅失败路径有值时写入。联合 `ocsp_certificate_revoked` 或 `ocsp_certificate_status_unknown_<raw>` 解读。 |
| `best_http_ocsp_revocation_reason` | OCSP revoked info 中的 revocation reason 原值。 | 可选；常见 `0=unspecified`、`1=keyCompromise`、`2=cACompromise`、`3=affiliationChanged`、`4=superseded`、`5=cessationOfOperation`、`6=certificateHold`、`8=removeFromCRL`、`9=privilegeWithdrawn`、`10=aACompromise`。 |

### 7.7 代理与 HTTP/2 字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_proxy_http_connect_status` | HTTPS 经 HTTP 代理建立 CONNECT tunnel 时，代理返回的状态码。 | CONNECT 非 `200`/`407` 终止时写入。`407` 可进入认证流程，不必然是最终失败。用于区分代理拒绝与目标站 HTTP 状态码。 |
| `best_http_socks_method` | SOCKS5 服务端选择的 METHOD 原始字节。 | 失败时常见 `1=GSSAPI`、`255=无可接受方法`。`0=无认证`、`2=用户名密码` 是正常方法，不会因成功单独写入该失败事实。 |
| `best_http_socks_auth_status` | SOCKS5 username/password 子协商 STATUS 原值。 | `0` 是成功；非 `0` 失败值写入，并对应 `socks_auth_status_<raw>`。 |
| `best_http_socks_reply` | SOCKS5 CONNECT REP 原值。 | `1`～`8` 分别表示服务端失败、规则禁止、网络不可达、主机不可达、连接被拒、TTL 过期、命令不支持、地址类型不支持。 |
| `best_http_http2_error_code` | HTTP/2 RST_STREAM 或 GOAWAY error code 原值。 | `uint`；包括 `0x0=NO_ERROR`～`0xD=HTTP_1_1_REQUIRED`。用于区分流级 reset 与连接级 GOAWAY；需联合 leaf code 中的 `rst_stream`/`goaway`。 |
| `best_http_http2_last_stream_id` | GOAWAY frame 声明的 last-stream-id。 | 仅 GOAWAY 有该事实时写入。当请求 stream ID 大于它时，服务端可能未处理该流，是否可安全重试还需考虑 HTTP method 幂等性。 |

### 7.8 时序字段

| 字段 | 详细说明 | 取值、缺省与排障用法 |
|---|---|---|
| `best_http_attempt_elapsed_ms` | 当前 attempt 从 `BeginAttempt` 到事件构建时的单调时钟耗时。 | 毫秒整数，最小 `0`。`request_attempt` 中通常接近 0；`request_error`/`request_end` 才反映当前 attempt 实际耗时。不包之前 attempt 的耗时。 |
| `best_http_total_elapsed_ms` | 逻辑请求从 `Timing.Created` 到 `request_end` 的总墙钟时间。 | 包含排队、所有重试/重定向、网络阶段与中间等待；是用户体感总耗时的主字段。 |
| `best_http_queue_ms` | `Queued` timing event 的累计 duration。 | 同名 timing 多次出现时求和。高值通常指向连接池/主机并发限制或主线程调度，不是服务端响应慢。 |
| `best_http_dns_ms` | `DNS Lookup` timing event 的累计 duration。 | 可因多 attempt 累加。值为 0 或缺失可能是缓存命中、复用连接、未走 DNS 或时序源没有生成该事件。 |
| `best_http_tcp_ms` | `TCP Connection` timing event 的累计 duration。 | 可包含多 IP race/重连的累计时间。复用连接时通常不会有新的 TCP 耗时。 |
| `best_http_tls_ms` | `TLS Negotiation` timing event 的累计 duration。 | 只有进入 TLS 协商才有意义。高值需联合 handler、TLS version、OCSP 与服务端地域判断。 |
| `best_http_ttfb_ms` | `Waiting TTFB` timing event 的累计 duration。 | 从请求发送完成到首字节到达的等待时间。高 TTFB 更倾向服务端处理/上游网络，但仍需排除上传未完成、代理与拥塞。 |

## 8. 叶子错误码完整字典

排障时首先按 `best_http_leaf_error_code` 定位阶段，再查该阶段的 raw 字段。下表中“排查”是客户端可以立即采取的最小鉴别步骤，不表示看到该码就可以单方定责。

### 8.1 DNS 固定码

| 错误码 | 含义 | 典型原因 | 关联字段与排查 |
|---|---|---|---|
| `dns_query_timeout` | DNS 查询在限时内未完成。 | 本地 DNS 不可达、系统解析器卡住、网络切换。 | 查 `dns_host`、`leaf_exception_type`、`dns_ms`；对比同设备其他 host 与系统 DNS 结果。 |
| `dns_empty_result` | DNS 调用成功返回，但地址集合为空。 | 解析器/平台异常、域名无地址记录。 | 查 `dns_host`；用权威 DNS 与本地解析分别验证 A/AAAA。 |
| `dns_no_ipv4_ipv6_result` | DNS 有返回项，但过滤后没有 IPv4/IPv6 地址。 | 返回了非 IP 结果或平台地址族不兼容。 | 查返回记录类型、IPv4/IPv6 网络能力和地址族过滤。 |
| `dns_resolve_failed` | 非 `SocketException`/非 timeout 的通用 DNS 异常。 | 平台 API 异常、内部解析流程失败。 | 优先查 `leaf_exception_type`，然后复现并采集同时段平台网络日志。 |

### 8.2 DNS/TCP `SocketError` 映射

下表每个 suffix 都可形成 `dns_<suffix>` 或 `tcp_<suffix>`。DNS 实际常见最后四项；TCP 则更常见 reset/refused/timeout/unreachable。未在表内的原始值使用 `dns_unknown_<raw>` 或 `tcp_unknown_<raw>`。

| suffix | 含义/典型原因 | 排查要点 |
|---|---|---|
| `success` | API 返回 Success，但上层仍走到失败记录；属于异常组合。 | 查 raw value、调用时序和是否同时有更精确 leaf。 |
| `socket_error_minus_one` | .NET 通用 SocketError `-1`，没有更精确分类。 | 联合 `native_error`、异常类型和 OS 日志。 |
| `interrupted` | 阻塞调用被中断。 | 检查线程/网络切换、取消和底层 EINTR。 |
| `access_denied` | OS/安全策略拒绝 socket 操作。 | 检查平台网络权限、防火墙、沙盒和企业策略。 |
| `fault` | socket API 检测到无效内存/参数地址。 | 通常是平台或 native 交互异常，查平台日志和调用参数。 |
| `invalid_argument` | socket 参数或当前状态无效。 | 检查地址族、端口、socket 生命周期和平台兼容性。 |
| `too_many_open_sockets` | 进程/系统 socket 句柄耗尽。 | 检查连接泄漏、并发峰值和 fd 限制。 |
| `would_block` | 非阻塞 socket 当前无法立即完成。 | 若成为终态，检查异步状态机是否误将暂态当失败。 |
| `in_progress` | 非阻塞连接仍在进行。 | 同上，检查超时与完成回调竞态。 |
| `already_in_progress` | 同一 socket 上已有未完成操作。 | 检查重复 connect/send 和并发使用。 |
| `not_socket` | 句柄不是有效 socket。 | 检查释放后使用、native 句柄被覆盖。 |
| `destination_address_required` | 未连接 socket 发送时没有目标地址。 | 检查连接流程与目标 endpoint 生成。 |
| `message_size` | 消息超出底层协议/缓冲限制。 | 检查上传分片、MTU 和 socket 类型。 |
| `protocol_type` | 协议类型与 socket 不匹配。 | 检查 protocol/socket type/address family 组合。 |
| `protocol_option` | 协议选项无效或不被支持。 | 检查 setsockopt、keepalive 和平台分支。 |
| `protocol_not_supported` | OS 不支持请求的协议。 | 检查平台能力、IPv6/IPv4 配置和裁剪。 |
| `socket_not_supported` | 指定地址族不支持该 socket 类型。 | 检查 address family/type/protocol 组合。 |
| `operation_not_supported` | 当前 socket 或平台不支持该操作。 | 检查平台特例与调用阶段。 |
| `protocol_family_not_supported` | 协议族不受支持。 | 检查 OS 网络栈和地址族选择。 |
| `address_family_not_supported` | 目标地址族不受支持。 | 常见于 IPv6-only/IPv4-only 不匹配；查 `execute_ip`。 |
| `address_already_in_use` | 本地地址/端口已被占用。 | 检查端口耗尽、TIME_WAIT 和本地 bind 策略。 |
| `address_not_available` | 本地或目标地址当前不可用。 | 检查网卡切换、无效本地 IP 与地址族。 |
| `network_down` | OS 报告网络子系统不可用。 | 检查飞行模式、网卡和 VPN 切换。 |
| `network_unreachable` | 没有到目标网络的路由。 | 检查路由表、VPN/代理、IPv4/IPv6 连通性。 |
| `network_reset` | 连接期间网络子系统重置。 | 检查网络切换、NAT 刷新与中间设备。 |
| `connection_aborted` | 本地系统/软件中止连接。 | 检查本地安全软件、超时和连接关闭竞态。 |
| `connection_reset` | 已建立连接被 RST 重置。 | 检查服务端/负载均衡日志、空闲连接复用和中间网络。 |
| `no_buffer_space_available` | OS socket 缓冲或网络资源不足。 | 检查资源泄漏、发送速率与系统内存压力。 |
| `is_connected` | 对已连接 socket 重复执行不允许的 connect。 | 检查连接复用状态机。 |
| `not_connected` | 需要已连接 socket 的操作在未连接时执行。 | 检查 connect 失败后是否仍发送/读取。 |
| `shutdown` | socket 已 shutdown，仍执行读写。 | 检查 abort/quit/连接回收与工作线程竞态。 |
| `timed_out` | socket 层操作超时。 | 查 `attempt_elapsed_ms`、`tcp_ms`、连接超时配置与目标 IP 分布。 |
| `connection_refused` | 目标主动拒绝连接。 | 检查 IP/port、服务是否监听、防火墙 REJECT 和发布状态。 |
| `host_down` | OS 判定目标主机不可用。 | 检查目标节点、ARP/邻居发现和网络路由。 |
| `host_unreachable` | 没有到目标主机的可达路径。 | 检查单 IP 节点、网关与 IPv4/IPv6 路由。 |
| `process_limit` | 进程达到 socket/资源限制。 | 检查并发、句柄泄漏与平台配额。 |
| `system_not_ready` | 底层网络子系统尚未就绪。 | 常见于网络初始化/切换瞬间；稍后重试并检查平台状态。 |
| `version_not_supported` | 系统 socket 实现版本不兼容。 | 检查平台运行时/底层网络库兼容性。 |
| `not_initialized` | socket 网络子系统未初始化。 | 检查平台启动时序和 native 初始化。 |
| `disconnecting` | socket 正在断开。 | 检查关闭过程中的并发读写。 |
| `type_not_found` | 请求的 socket 类型未找到。 | 检查平台协议栈和参数。 |
| `host_not_found` | DNS 确认主机名不存在/无记录。 | 检查域名拼写、发布记录、搜索域与污染 DNS。 |
| `try_again` | DNS 临时失败，建议稍后重试。 | 检查本地 resolver 负载、SERVFAIL 和短时网络波动。 |
| `no_recovery` | DNS 发生不可恢复的解析器错误。 | 检查 DNS 服务器返回、配置与权威链。 |
| `no_data` | 域名存在，但没有请求类型的地址数据。 | 检查 A/AAAA 记录与当前地址族。 |
| `io_pending` | 异步 I/O 尚未完成。 | 若成为终态，检查异步完成回调与超时竞态。 |
| `operation_aborted` | I/O 被取消或 socket 关闭。 | 联合 `state`、abort/quit 代码和网络切换判断是主动还是被动。 |

### 8.3 TCP race 固定码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `tcp_connect_error` | 单个 TCP lane 失败，但没有可用 `SocketError`。 | 查 `connect_attempts`、`native_error` 和 `leaf_exception_type`；属于精度较低的 TCP 失败。 |
| `tcp_race_timeout` | 多 IP TCP race 整体超时。 | 查所有 `connect_attempts`、DNS 返回地址数与连接超时；判断是全地址不可达还是时间窗过短。 |
| `tcp_race_all_candidates_failed` | TCP race 所有候选均失败，且终态没有更精确 socket leaf。 | 逐项检查 `connect_attempts`，按 IP/地址族/错误类型分组；不要只看最后一个 `execute_ip`。 |

### 8.4 TLS alert 与通用协商码

TLS alert leaf 格式为 `tls_<peer|local>_alert_<name>`。`peer` 表示对端发送 fatal alert，`local` 表示本地 TLS 实现产生 fatal alert。未识别 description 使用 `tls_<direction>_alert_unknown_<raw>`。

| `<name>` | 含义 | 典型原因/排查 |
|---|---|---|
| `close_notify` | TLS 关闭通知被以 fatal 路径记录。 | 检查对端是否提前关闭、alert level raw 和请求是否已完成。 |
| `unexpected_message` | 收到当前 TLS 状态不允许的消息。 | 检查协议版本、中间代理和服务端 TLS 实现。 |
| `bad_record_mac` | TLS record MAC/AEAD 校验失败。 | 检查中间设备篡改、传输损坏和 cipher 兼容性。 |
| `decryption_failed` | TLS record 解密失败。 | 检查密钥/密码套件、协议实现和中间设备。 |
| `record_overflow` | TLS record 长度超出允许范围。 | 检查非 TLS 数据打到 TLS 端口、代理和损坏流量。 |
| `decompression_failure` | TLS 级压缩数据无法解压。 | 现代 TLS 不应常见；检查旧协议/服务端与中间设备。 |
| `handshake_failure` | 对端/本地无法协商握手参数。 | 优先对比 TLS version、cipher、SNI、ALPN 和客户端证书要求。 |
| `no_certificate` | 协议期望证书但未提供。 | 检查服务端/双向 TLS 的证书配置。 |
| `bad_certificate` | 证书格式或内容不可接受。 | 检查证书解析、签名链和服务端发送的完整链。 |
| `unsupported_certificate` | 证书类型/算法不受支持。 | 检查签名算法、密钥类型与客户端版本。 |
| `certificate_revoked` | TLS 对端报告证书已撤销。 | 核对证书序列号、OCSP/CRL 和证书替换状态。 |
| `certificate_expired` | TLS 对端报告证书过期。 | 检查证书 NotAfter、设备时钟和 CDN/边缘节点证书。 |
| `certificate_unknown` | 证书因未细分原因被拒绝。 | 查相邻 cert/OCSP leaf、TLS 日志和原始 alert description。 |
| `illegal_parameter` | 握手参数超出协议允许范围或相互矛盾。 | 检查 extension、signature scheme、key share 与协议版本。 |
| `unknown_ca` | 证书链无法连到受信 CA。 | 检查中间证书是否缺失、私有 CA 是否安装、边缘节点链配置。 |
| `access_denied` | TLS 对端基于策略拒绝继续。 | 检查 mTLS 身份、访问控制与 WAF/代理策略。 |
| `decode_error` | TLS 握手消息结构无法解码。 | 检查协议实现兼容、数据损坏和非 TLS 端口。 |
| `decrypt_error` | 握手签名/密钥确认无法验证。 | 检查证书私钥配对、签名算法和中间设备。 |
| `export_restriction` | 历史 TLS 出口限制错误。 | 现代环境极少见；检查老旧服务端/协议栈。 |
| `protocol_version` | 双方没有共同 TLS 版本。 | 对比客户端启用版本与服务端最小/最大 TLS 版本。 |
| `insufficient_security` | 对端认为可用参数安全强度不足。 | 检查 cipher、密钥长度、签名算法和服务端安全策略。 |
| `internal_error` | TLS 实现内部失败。 | 查 direction 判断哪一端报错，再结合对端/客户端 TLS 日志。 |
| `inappropriate_fallback` | 对端拒绝不当的 TLS 版本降级。 | 检查客户端版本回退策略与中间设备。 |
| `user_canceled` | TLS 一方主动取消握手。 | 联合 direction、请求 abort 时序和对端日志。 |
| `no_renegotiation` | 对端拒绝 TLS renegotiation。 | 检查是否使用老旧 renegotiation 流程。 |
| `missing_extension` | TLS 握手缺少必需 extension。 | 检查 TLS 1.3 extension、SNI、ALPN 与 key share。 |
| `unsupported_extension` | 收到不允许/不支持的 extension。 | 检查客户端与服务端 TLS 实现版本。 |
| `certificate_unobtainable` | 无法获取协议要求的证书。 | 检查证书选择器、mTLS 配置和证书库。 |
| `unrecognized_name` | 对端不识别 SNI host。 | 核对 `tls_sni_host`、HostKey/域名、CDN 绑定和虚拟主机配置。 |
| `bad_certificate_status_response` | OCSP stapling/status response 无效。 | 检查服务端 staple、OCSP 响应完整性和相关 `ocsp_*` leaf。 |
| `bad_certificate_hash_value` | 证书 hash 校验失败。 | 检查证书状态/扩展数据与中间设备。 |
| `unknown_psk_identity` | 服务端不识别 PSK identity。 | 检查 PSK/session 配置和会话恢复。 |
| `certificate_required` | 对端要求客户端证书但未提供。 | 检查 mTLS 客户端证书与 signer credentials。 |
| `no_application_protocol` | ALPN 没有共同协议。 | 对比客户端提供的 `h2/http/1.1` 与服务端 ALPN 配置。 |

`tls_handshake_failed` 表示没有更精确 fatal alert/certificate/OCSP leaf 的通用握手失败。此时应依次检查 `tls_handler`、`tls_sni_host`、`tls_version`、`tls_cipher_suite`、`exception_type` 和服务端 TLS 日志。

### 8.5 证书码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `cert_missing_leaf` | 证书链中没有可用叶子证书，或 Framework TLS 报远端证书不可用。 | 检查服务端是否发送证书及完整链。 |
| `cert_san_mismatch` | URL host 不匹配证书 Subject Alternative Name。 | 核对 `host`、`tls_sni_host`、证书 SAN 和 CDN/回源域名。 |
| `cert_common_name_missing` | 证书没有 SAN 可用且 Common Name 也缺失。 | 更换为包含正确 SAN 的证书。 |
| `cert_common_name_mismatch` | fallback 的 Common Name 与 host 不匹配。 | 核对 host/SNI/CN；避免使用 IP 替换 HTTPS host 而不保留正确 SNI。 |
| `cert_not_yet_valid` | 当前 UTC 早于证书 NotBefore。 | 检查设备时钟、时区、证书生效时间和发布节点。 |
| `cert_expired` | 当前 UTC 晚于证书 NotAfter。 | 续期/替换证书，并检查 CDN 各边缘节点是否均已刷新。 |
| `cert_unknown_ca` | Best TLS 无法找到受信根/签发者。 | 检查中间证书、根库更新、私有 CA 与证书链顺序。 |
| `cert_path_build_failed` | 找到证书但无法构建到受信根的有效路径。 | 检查链缺失、过期/交叉签名中间证书和 BasicConstraints/KeyUsage。 |
| `cert_framework_name_mismatch` | Framework TLS `SslPolicyErrors` 含 RemoteCertificateNameMismatch。 | 查 `certificate_policy_errors`、host/SNI 和平台证书验证差异。 |
| `cert_framework_chain_error` | Framework TLS 证书拒绝，且主因不是缺证书或名称不匹配。 | 按 `certificate_policy_errors` 位标记、OS 信任库和 `X509ChainStatus` 日志排查。 |

### 8.6 OCSP 码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `ocsp_chain_missing_issuer` | OCSP 校验缺少叶子证书的 issuer。 | 检查服务端证书链是否至少包含 leaf + issuer。 |
| `ocsp_endpoint_missing` | 证书 AIA 中没有可用 OCSP endpoint。 | 检查 Authority Information Access 扩展与 OCSP 策略；文档不上报 endpoint URL。 |
| `ocsp_must_staple_status_missing` | 证书声明 Must-Staple，但服务端未随 TLS 提供 certificate status。 | 在服务端/CDN 启用有效 OCSP stapling。 |
| `ocsp_connect_timeout` | OCSP HTTP 请求建连超时。 | 检查客户端到 OCSP responder 的路由、DNS/防火墙和 connect timeout。 |
| `ocsp_request_timeout` | OCSP HTTP 请求总超时。 | 检查 responder 延迟、丢包和 request timeout。 |
| `ocsp_wait_timeout` | 等待 OCSP 查询/共享结果超时。 | 检查 OCSP 并发、缓存锁/回调时序和 responder 耗时。 |
| `ocsp_http_transport_error` | OCSP HTTP 运输进入 Error 终态。 | 结合 TLS/HTTP 日志和当时网络环境；该码不包含 endpoint URL。 |
| `ocsp_http_aborted` | OCSP HTTP 请求被中止。 | 检查请求取消、应用退出、上层 TLS 流程提前结束。 |
| `ocsp_http_status_<status>` | OCSP responder 返回非成功 HTTP 状态。 | 例如 `ocsp_http_status_404/500`；按 status 检查 responder/CDN/代理，不要与 OCSP 协议 `responseStatus` 混淆。 |
| `ocsp_content_type_invalid` | OCSP HTTP 响应 Content-Type 不符合预期。 | 检查 responder 是否返回 HTML/代理错误页或 MIME 配置错误。 |
| `ocsp_der_noncanonical` | OCSP DER 编码不规范。 | 检查 responder 生成器、中间设备改写和 DER 解析兼容。 |
| `ocsp_response_empty` | OCSP 响应缺少预期载荷/单证书响应。 | 检查 responder 返回与请求 CertID。 |
| `ocsp_serial_mismatch` | OCSP SingleResponse 证书序列号与目标证书不匹配。 | 检查缓存污染、responder 返回错误证书结果。 |
| `ocsp_issuer_name_hash_mismatch` | OCSP CertID issuer name hash 不匹配。 | 检查 issuer 选择、证书链顺序与 responder 数据。 |
| `ocsp_issuer_key_hash_mismatch` | OCSP CertID issuer key hash 不匹配。 | 检查是否使用了错误/交叉签名 issuer。 |
| `ocsp_responder_signer_not_found` | 无法在响应/链中找到可用的 OCSP 签名者证书。 | 检查 responder 是否附带 signer cert、ResponderID 和 OCSPSigning EKU。 |
| `ocsp_signature_invalid` | OCSP 响应签名验证失败。 | 检查 signer cert、响应完整性、算法兼容和中间设备。 |
| `ocsp_certificate_revoked` | OCSP 确认目标证书已撤销。 | 立即核对 `ocsp_certificate_status`、`ocsp_revocation_reason`、序列号并替换证书；不应简单重试绕过。 |
| `ocsp_status_unknown` | OCSP 查询总体得到 Unknown，且 FailHard 策略将其视为失败。 | 检查 responder 数据新鲜度、证书是否刚签发和 FailHard 配置。 |
| `ocsp_certificate_status_unknown_<raw>` | SingleResponse 中证书状态 tag 不是 Good/Revoked。 | 查 `ocsp_certificate_status` 原值与 responder 实现。 |
| `ocsp_response_status_<raw>` | OCSPResponse 协议状态非 Successful。 | 查 `ocsp_response_status`；1/2/3/5/6 分别对应 malformedRequest/internalError/tryLater/sigRequired/unauthorized。 |
| `ocsp_signer_not_yet_valid` | OCSP signer 证书尚未生效。 | 检查设备时钟、signer NotBefore 与 responder 发布。 |
| `ocsp_signer_expired` | OCSP signer 证书已过期。 | 检查 responder signer 换证与缓存中旧响应。 |

### 8.7 HTTP 代理与 SOCKS5 码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `proxy_http_connect_status_<status>` | HTTP CONNECT tunnel 返回不被当作成功/可继续认证的状态码。 | 查 `proxy_http_connect_status`；403 多为策略拒绝，5xx 多为代理/上游故障。 |
| `socks_method_response_length_<count>` | SOCKS5 METHOD 协商响应长度不是 2 字节。 | 代理返回截断/非 SOCKS5 数据；检查代理类型、port 和中间设备。 |
| `socks_version_unknown_<raw>` | METHOD 响应中 VER 不是 `5`。 | 检查是否连到 SOCKS4/HTTP 代理或错误端口。 |
| `socks_method_gssapi_unsupported` | 服务端选择 GSSAPI (`0x01`)，客户端不支持。 | 调整代理允许 no-auth 或 username/password，或增加 GSSAPI 能力。 |
| `socks_method_no_acceptable_methods` | 服务端返回 `0xFF`，没有双方共同认证方式。 | 核对客户端提供方式与代理配置。 |
| `socks_method_unknown_<raw>` | 服务端选择未实现/未识别 METHOD。 | 查 `socks_method` 和代理自定义认证配置。 |
| `socks_auth_response_length_<count>` | username/password 认证响应长度不是 2。 | 检查代理协议实现、数据截断和错误端口。 |
| `socks_auth_status_<raw>` | username/password STATUS 非 0。 | 查 `socks_auth_status`，核对用户名密码和代理账号状态；不上报凭据内容。 |
| `socks_connect_response_length_<count>` | SOCKS5 CONNECT 响应少于实现预期的 10 字节。 | 检查代理返回截断、IPv4/IPv6/domain 响应实现和中间设备。 |
| `socks_reply_general_server_failure` | REP `0x01`，代理服务器通用失败。 | 查代理服务器日志与上游连接。 |
| `socks_reply_connection_not_allowed` | REP `0x02`，规则不允许连接。 | 检查 ACL、目标域名/IP/port 白名单。 |
| `socks_reply_network_unreachable` | REP `0x03`，代理到目标网络不可达。 | 检查代理侧路由/VPN/地址族。 |
| `socks_reply_host_unreachable` | REP `0x04`，代理到目标主机不可达。 | 检查目标 IP、DNS 和代理出口网络。 |
| `socks_reply_connection_refused` | REP `0x05`，目标拒绝代理连接。 | 检查目标 port/服务监听/防火墙。 |
| `socks_reply_ttl_expired` | REP `0x06`，转发 TTL 过期。 | 检查代理到目标的路由环路与网络路径。 |
| `socks_reply_command_not_supported` | REP `0x07`，代理不支持 CONNECT 命令/请求命令。 | 核对代理能力和客户端命令。 |
| `socks_reply_address_type_not_supported` | REP `0x08`，代理不支持请求的地址类型。 | 切换 domain/IPv4/IPv6 目标表达或升级代理。 |
| `socks_reply_unknown_<raw>` | 未分配/未识别 REP。 | 查 `socks_reply` raw 和代理私有扩展。 |

### 8.8 HTTP/2 码

RST_STREAM/GOAWAY peer 码格式为 `http2_<rst_stream|goaway>_<name>`。未知值为 `http2_<frame>_unknown_<raw>`。

| `<name>` | 协议含义 | 排查要点 |
|---|---|---|
| `no_error` | 没有协议错误，但对端要求关闭流/连接。 | GOAWAY 场景查 `last_stream_id` 和优雅下线；RST_STREAM 场景查业务取消。 |
| `protocol_error` | 对端检测到通用 HTTP/2 协议违规。 | 检查 frame 序列、stream state 与中间代理。 |
| `internal_error` | 对端 HTTP/2 内部错误。 | 查服务端/网关日志，并按节点聚合。 |
| `flow_control_error` | 连接或流流控窗口违规。 | 检查 WINDOW_UPDATE、上下行流控和大包传输。 |
| `settings_timeout` | 对端等待 SETTINGS ACK 超时。 | 检查丢包、主线程/网络线程卡顿和中间代理。 |
| `stream_closed` | 对已关闭 stream 执行了不允许的操作。 | 检查并发 reset/end-stream 时序。 |
| `frame_size_error` | frame 大小不符合协议/对端 SETTINGS。 | 检查 MAX_FRAME_SIZE、帧切分与代理。 |
| `refused_stream` | 对端未处理并拒绝该 stream。 | 幂等请求通常可重试；非幂等请求必须先确认服务端是否已产生副作用。 |
| `cancel` | 对端取消 stream。 | 检查服务端超时/业务取消、客户端 abort 时序。 |
| `compression_error` | HPACK/header compression 状态错误。 | 检查动态表同步、header block 完整性和代理实现。 |
| `connect_error` | CONNECT 协议隧道连接错误。 | 检查 HTTP/2 proxy/tunnel 上游连接。 |
| `enhance_your_calm` | 对端认为客户端行为过载/超限。 | 降低并发/QPS/header 体积，查网关限流。 |
| `inadequate_security` | 当前 HTTP/2 TLS/安全参数不足。 | 检查 TLS version、cipher 和 HTTP/2 服务端安全要求。 |
| `http_1_1_required` | 对端要求改用 HTTP/1.1。 | 确认客户端是否会安全降级，检查网关对 HTTP/2 的支持。 |

| 其他 HTTP/2 码 | 含义 | 排查要点 |
|---|---|---|
| `http2_ping_ack_timeout` | 发出 PING 后未在限时内收到 ACK。 | 检查连接假死、中间设备空闲超时和网络线程卡顿。 |
| `http2_settings_ack_timeout` | 本地 SETTINGS 未收到 ACK。 | 检查对端 HTTP/2 实现、丢包和网络处理线程。 |
| `http2_frame_header_incomplete` | TCP 关闭时 HTTP/2 9 字节 frame header 未读完。 | 检查 reset/提前关闭和中间代理截断。 |
| `http2_frame_payload_incomplete` | TCP 关闭时 frame payload 未读满声明长度。 | 检查对端提前关闭、流量损坏和代理。 |
| `http2_hpack_raw_string_offset_invariant` | HPACK raw string 编码内部 offset 不变式被破坏。 | 更像客户端实现/输入边界缺陷；保留请求头结构的脱敏复现条件。 |
| `http2_header_frame_unexpected_type_<raw>` | 读 header block 时遇到非预期 frame type。 | 检查 CONTINUATION/HEADERS 序列、代理和对端协议实现。 |
| `http2_huffman_unsupported_bit_<raw>` | HPACK Huffman 解码遇到实现不支持的 bit/path。 | 检查 header 压缩数据是否损坏，并保留对端/代理版本信息。 |

### 8.9 HTTP/1 码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `http1_invalid_version` | 状态行 HTTP 版本格式无效。 | 检查非 HTTP 数据、代理/WAF 错误页与响应损坏。 |
| `http1_invalid_status_code` | 状态行中状态码无法解析。 | 检查服务端/代理原始响应格式。 |
| `http1_invalid_content_length` | `Content-Length` 无法解析为有效长度。 | 检查重复冲突 header、非数字值和中间设备改写。 |
| `http1_invalid_chunk_size` | chunked body 的十六进制 chunk size 无效。 | 检查服务端 chunk framing、压缩/代理损坏。 |
| `http1_peer_closed_before_status_line` | 未读到完整状态行对端就关闭。 | 检查服务端提前 close、空闲连接复用、LB/WAF reset。 |
| `http1_peer_closed_during_headers` | 响应 header 未结束对端就关闭。 | 检查过大/非法 header、代理截断和服务端异常。 |
| `http1_peer_closed_before_content` | 解析状态不在 content 阶段时连接关闭，且无更精确状态行/header 码。 | 结合状态码、header 日志和服务端 close 时序。 |
| `http1_peer_closed_during_content` | 无固定长度/非 chunked 内容传输中断。 | 检查服务端 close-delimited body、中间网络与下载流消费。 |
| `http1_fixed_body_incomplete` | 已下载字节少于 `Content-Length`。 | 对比声明长度/实际长度，检查服务端提前关闭、CDN 与丢包。 |
| `http1_chunk_length_incomplete` | 连接关闭时 chunk length 行未读完。 | 检查 chunked framing 截断。 |
| `http1_chunk_data_incomplete` | 当前 chunk 的数据少于声明长度。 | 检查服务端提前 close、代理/CDN 截断。 |
| `http1_chunk_terminator_incomplete` | chunk 后 CRLF 终止符未读完。 | 检查服务端 chunk writer 和数据损坏。 |
| `http1_chunk_trailing_headers_incomplete` | 0-size chunk 后 trailing headers 未完成。 | 检查 trailer 生成、代理支持和提前 close。 |
| `http1_chunk_framing_incomplete` | chunked body 结束结构不完整，但无更精确子阶段。 | 检查最后 0-size chunk、CRLF/trailer 与中间设备。 |

### 8.10 响应解压码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `brotli_invalid_data` | Brotli decoder 判定输入无效。 | 检查 `Content-Encoding: br`、载荷是否截断/被代理改写。 |
| `gzip_header_incomplete` | GZIP 固定 header 不完整。 | 检查响应截断或错误 Content-Encoding。 |
| `gzip_header_invalid_signature_or_method` | GZIP magic/method 无效。 | 服务端可能声明 gzip 却返回原文/其他格式。 |
| `gzip_header_extra_field_incomplete` | GZIP FEXTRA 字段不完整。 | 检查载荷截断和压缩器输出。 |
| `gzip_header_text_field_incomplete` | GZIP filename/comment 等文本字段在 EOF 前未结束。 | 检查载荷截断。 |
| `gzip_trailer_incomplete` | GZIP 8 字节 trailer 不完整。 | 检查响应末尾截断、Content-Length/chunk framing。 |
| `gzip_trailer_crc_mismatch` | 解压数据 CRC32 与 trailer 不一致。 | 检查传输损坏、缓存污染和代理重压缩。 |
| `gzip_trailer_size_mismatch` | 解压数据大小与 trailer ISIZE 不一致。 | 检查截断、多成员 gzip 兼容与生成器。 |
| `deflate_invalid_block_type` | deflate block type 为保留/无效值。 | 检查数据损坏或 Content-Encoding 错标。 |
| `deflate_stored_block_length_mismatch` | stored block LEN/NLEN 校验不一致。 | 检查传输损坏和压缩器。 |
| `deflate_too_many_length_or_distance_symbols` | 动态 Huffman 长度/距离符号数超标。 | 检查损坏载荷或非 deflate 数据。 |
| `deflate_invalid_bit_length_repeat` | Huffman bit-length repeat 超出允许范围。 | 检查动态 Huffman 数据损坏。 |
| `deflate_invalid_literal_length_code` | literal/length Huffman code 无效。 | 检查压缩数据损坏。 |
| `deflate_invalid_distance_code` | distance Huffman code/距离无效。 | 检查压缩数据损坏或窗口边界。 |
| `deflate_header_checksum_mismatch` | zlib header FCHECK 不通过。 | 检查 zlib wrapper 头和 Content-Encoding 格式。 |
| `deflate_preset_dictionary_required` | 流需要预置字典，客户端未提供。 | 服务端不应对普通 HTTP deflate 使用未约定字典；调整压缩配置。 |
| `deflate_data_checksum_mismatch` | zlib Adler32 与解压数据不一致。 | 检查传输损坏、缓存和代理重压缩。 |
| `deflate_unknown_compression_method_<raw>` | zlib CM 不是受支持的 deflate 方法。 | 查 raw method，检查错标 Content-Encoding/自定义压缩。 |
| `deflate_invalid_window_size_<raw>` | zlib CINFO 表示的窗口大小无效或超支持范围。 | 检查压缩器窗口配置与数据损坏。 |
| `decompression_zlib_failed` | zlib/gzip 解压失败但底层未提供更精确 leaf。 | 查 Content-Encoding、响应长度与是否可稳定复现；属于解压通用兜底。 |

### 8.11 上传码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `upload_stream_read_failed` | 从调用方 upload stream 读取失败。 | 检查 stream 是否已 dispose、文件权限/存储 I/O 与并发读。 |
| `upload_stream_write_failed` | 把上传数据写入网络 stream 失败。 | 检查连接 reset/closed、socket 错误与上传大小。 |
| `upload_stream_flush_failed` | flush 上传 stream 失败。 | 检查连接在上传末尾是否关闭、底层 I/O 异常。 |
| `upload_stream_wait_failed` | 等待上传可写/完成时失败。 | 检查背压、取消、上传线程与超时。 |
| `upload_cancelled_by_client` | 客户端主动取消上传。 | 联合 `state=Aborted`、业务取消时序；不应默认归因于弱网。 |
| `upload_unknown_<stage>` | 未识别的上传阶段失败。 | 查 stage 后缀和当时调用栈/源码版本；属于未来扩展兜底。 |

### 8.12 生命周期与 WebGL 码

| 错误码 | 含义 | 典型原因与排查 |
|---|---|---|
| `request_connect_timeout` | 请求在连接阶段超时，且没有更深 leaf。 | 查 DNS/TCP/TLS 耗时、connect timeout 和是否缺失更精确诊断。 |
| `request_timeout` | 请求总时限到期，且没有更深 leaf。 | 查 `total_elapsed_ms`、TTFB/下载/上传阶段与总 timeout。 |
| `request_aborted_by_client` | `HTTPRequest.Abort()` 导致终态，且没有更深 leaf。 | 检查业务取消、页面/流程销毁和 timeout 实现是否通过 Abort 触发。 |
| `request_aborted_on_quit` | HTTP 连接在应用/网络管理器退出时被中止。 | 联合应用退出/重启时序；不计入一般网络质量失败率。 |
| `request_error` | Best HTTP 进入 Error，但未采集到更精确叶子根因。 | 查 `exception_type`、attempt 耗时、平台日志；该码信息量最低，应用于发现诊断覆盖缺口。 |
| `webgl_xhr_error` | 浏览器 XHR 触发 error。 | 可能是 CORS、DNS/TLS/网络错误，浏览器通常不暴露更精确原因；查 DevTools Network/Console 和服务端 CORS。 |
| `webgl_xhr_timeout` | 浏览器 XHR 超时。 | 查 XHR timeout、页面后台限流、服务端耗时和浏览器网络面板。 |
| `webgl_xhr_aborted` | 浏览器 XHR 被 abort。 | 检查业务取消、页面卸载/切换和超时后主动 abort。 |

## 9. 事件联合解读示例

### 9.1 首次 TCP 失败、重试成功

```text
best_http_request_attempt request_id=R attempt_id=R-0 attempt_index=0
best_http_request_error   request_id=R attempt_id=R-0 attempt_index=0 leaf=tcp_connection_reset
best_http_request_attempt request_id=R attempt_id=R-1 attempt_index=1
best_http_request_end     request_id=R attempt_id=R-1 attempt_index=1 result=success recovered_by_retry=true status=200
```

正确口径：一次失败 attempt，但逻辑请求最终成功。不能因为看到 `request_error` 就统计为最终请求失败。

### 9.2 HTTP 409

```text
best_http_request_attempt request_id=R attempt_id=R-0 attempt_index=0
best_http_request_end     request_id=R attempt_id=R-0 attempt_index=0 state=Finished result=http_error status=409
```

正确口径：客户端已收到可解析 HTTP 响应，因此没有 `request_error`。它可能是业务冲突，不能默认标记为 DNS/TCP/TLS 异常。

### 9.3 TLS 证书名不匹配

```text
best_http_request_error
  result: <not present>
  leaf: cert_san_mismatch
  host: api.example.com
  tls_sni_host: api.example.com
  execute_ip: 203.0.113.10
  tls_handler: bouncycastle

best_http_request_end
  result: network_error
  leaf: cert_san_mismatch
```

排查时应核对 URL host、SNI、胜出 IP 所属 CDN/源站及该节点返回的证书 SAN，而不是只泛化为“TLS 失败”。

### 9.4 推荐查询口径

- 逻辑请求成功率：仅按 `best_http_request_end.best_http_result` 统计。
- 物理网络稳定性：按 `best_http_request_error` / attempt 数统计，并单独展示 `recovered_by_retry`。
- 根因分布：使用 `leaf_error_code`，缺失时再降级到 `state` + `exception_type`。
- 分阶段耗时：只使用 `request_end` 中的 timing 字段，不把 `attempt_elapsed_ms` 当总耗时。
- 业务 fallback 链：使用调用方的 `correlation_id`/扩展字段，不从 `attempt_index` 反推。

## 10. 不采集与不内置的字段

商业库明确不采集 URL query、header、请求/响应正文、凭证、证书内容、OCSP endpoint URL、异常 message 和 stack trace。

以下 Girl v2 字段没有被机械搬入商业库，因为底层库无法准确判断：

- 业务 fallback：主/备域名、round/candidate、业务命令名、业务链最终结果。
- Nova DoH：是否启用、provider、预热状态、缓存命中、DNS RCODE、注入 IP。
- Unity 网络环境：`Application.internetReachability`。
- 业务 timeout 参数来源、默认值与调用入口。
- response message、代理模式/host 类型等可能包含环境信息的上层描述。

需要这些关联信息时，应由 Nova 或业务请求创建处通过 `TelemetryContext.Set(...)` 加入经过脱敏的非保留字段；不要在商业库中反向依赖框架。

## 11. 验证范围

自动化验证覆盖事件常量、属性快照、无 sink 行为、sink 异常隔离、request/attempt 关联、重试恢复、HTTP 409 终态、超时去重、DNS/TLS/代理/HTTP 协议叶子映射，以及 Nova 开关、启动缓存和多插件扇出。

尚未覆盖真实 Android/iOS 弱网、真实代理服务器、TLS alert 注入和 HTTP/2 GOAWAY 故障注入；这些场景仍需设备或可控服务端验证字段是否在真实网络栈中如期到达。
