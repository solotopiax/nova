/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaNativeInAppReview.swift
 * author:    taoye
 * created:   2026/8/14
 * descrip:   iOS 应用内评价 StoreKit C ABI 桥接
 ***************************************************************/

import StoreKit
import UIKit

/// Unity 托管层接收应用内评价请求状态的 C ABI 回调类型。
public typealias NovaNativeInAppReviewCallback = @convention(c) (UInt64, Int32) -> Void

/// 在主线程向系统发起应用内评价请求；回调仅描述原生请求状态，不代表弹窗展示或用户评价结果。
@_cdecl("NovaNative_RequestInAppReview")
public func NovaNative_RequestInAppReview(
    _ requestId: UInt64,
    _ callback: @escaping NovaNativeInAppReviewCallback)
{
    Task { @MainActor in
        guard #available(iOS 16.0, *) else
        {
            callback(requestId, 0)
            return
        }

        guard let scene = UIApplication.shared.connectedScenes
            .compactMap({ $0 as? UIWindowScene })
            .first(where: { $0.activationState == .foregroundActive }) else
        {
            callback(requestId, 1)
            return
        }

        AppStore.requestReview(in: scene)
        callback(requestId, 2)
    }
}
