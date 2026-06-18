/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPTrackEvents.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   IAP 埋点事件名与字段名常量
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 模块埋点事件名常量，与服务端 nova_iap_* 事件表对齐。
    /// </summary>
    public static class IAPTrackEvents
    {
        /// <summary>
        /// IAP store 初始化完成/失败事件。
        /// </summary>
        public const string Init = "nova_iap_init";

        /// <summary>
        /// 用户发起购买事件。
        /// </summary>
        public const string Buy = "nova_iap_buy";

        /// <summary>
        /// 平台侧确认支付成功（尚未验单）事件。
        /// </summary>
        public const string LocalPaySuccess = "nova_iap_local_pay_success";

        /// <summary>
        /// 平台侧拒绝支付事件。
        /// </summary>
        public const string LocalPayFail = "nova_iap_local_pay_fail";

        /// <summary>
        /// 服务器验单失败（可重试）事件。
        /// </summary>
        public const string ValidateFail = "nova_iap_validate_fail";

        /// <summary>
        /// 验单彻底失败（超出重试次数）事件。
        /// </summary>
        public const string ValidateFailFinish = "nova_iap_validate_fail_finish";

        /// <summary>
        /// 验单成功并已发货事件。
        /// </summary>
        public const string ValidateSuccess = "nova_iap_validate_success";

        /// <summary>
        /// 首次下单验单失败专用事件（用于区分首验 vs 重试验单的失败统计）。
        /// </summary>
        public const string FirstPayOrderValidate = "nova_iap_first_pay_order_validate";

        /// <summary>
        /// 验单已成功但发货失败事件。
        /// </summary>
        public const string DeliverFail = "nova_iap_deliver_fail";

        /// <summary>
        /// 创建第三方订单失败事件。
        /// </summary>
        public const string CreateOrderFail = "nova_iap_create_order_fail";

        /// <summary>
        /// 创建第三方订单成功事件。
        /// </summary>
        public const string CreateOrderSuccess = "nova_iap_create_order_success";

        /// <summary>
        /// 第三方收银台被关闭事件。
        /// </summary>
        public const string ThirdPayCloseOrder = "nova_iap_third_pay_close_order";
    }

    /// <summary>
    /// IAP 埋点事件字段名常量，与 nova_iap_* 事件属性表对齐。
    /// </summary>
    public static class IAPTrackFields
    {
        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        public const string TableId = "nova_table_id";

        /// <summary>
        /// 平台商品 ID。
        /// </summary>
        public const string ProductId = "nova_product_id";

        /// <summary>
        /// 是否测试模式（AlwaysPaySucceed）。
        /// </summary>
        public const string Debug = "nova_debug";

        /// <summary>
        /// 商品本地化价格（数值，币种由各平台决定）。
        /// </summary>
        public const string Price = "nova_price";

        /// <summary>
        /// 支付渠道名称。
        /// </summary>
        public const string Channel = "nova_channel";

        /// <summary>
        /// 订单 ID。
        /// </summary>
        public const string OrderId = "nova_order_id";

        /// <summary>
        /// 是否为恢复/补单订单。
        /// </summary>
        public const string AddOrder = "nova_add_order";

        /// <summary>
        /// 验单尝试次数。
        /// </summary>
        public const string ValidateCount = "nova_validate_count";

        /// <summary>
        /// 是否为网络错误导致的失败。
        /// </summary>
        public const string NetError = "nova_net_error";

        /// <summary>
        /// 失败原因枚举值。
        /// </summary>
        public const string Reason = "nova_reason";

        /// <summary>
        /// 失败原因补充描述。
        /// </summary>
        public const string ReasonDetail = "nova_reason_detail";

        /// <summary>
        /// 服务端返回的错误码。
        /// </summary>
        public const string ProtocolCode = "nova_protocol_code";

        /// <summary>
        /// 服务端返回的错误信息描述。
        /// </summary>
        public const string ProtocolMessage = "nova_protocol_message";

        /// <summary>
        /// 第三方支付渠道 ID。
        /// </summary>
        public const string ThirdPayTypeId = "nova_third_pay_type_id";

        /// <summary>
        /// 第三方支付方式名称。
        /// </summary>
        public const string ThirdPayMethod = "nova_third_pay_method";

        /// <summary>
        /// 业务层透传自定义字符串参数，调度层不解析。
        /// </summary>
        public const string CustomData = "nova_custom_data";

        /// <summary>
        /// IAP 初始化结果（success/failed）。
        /// </summary>
        public const string InitResult = "nova_iap_init_result";

        /// <summary>
        /// IAP 初始化失败原因。
        /// </summary>
        public const string InitFailureReason = "nova_iap_init_failure_reason";
    }
}
