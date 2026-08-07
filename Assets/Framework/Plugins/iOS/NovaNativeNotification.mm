/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaNativeNotification.mm
 * author:    taoye
 * created:   2026/8/7
 * descrip:   iOS 通知权限与应用设置 C ABI 桥接
 ***************************************************************/

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#include <stdint.h>

typedef void (*NovaNativeNotificationStatusCallback)(uint64_t requestId, int32_t authorizationStatus);
typedef void (*NovaNativeNotificationRequestCallback)(
    uint64_t requestId,
    int32_t authorizationStatus,
    int64_t errorCode,
    const char *errorDomainUtf8,
    const char *errorMessageUtf8);
typedef void (*NovaNativeOpenSettingsCallback)(uint64_t requestId, int32_t opened);

/// 将回调统一派发到 iOS 主队列，避免后台 completion 直接进入 Unity 托管层。
static void NovaNativeDispatchToMain(dispatch_block_t block)
{
    if ([NSThread isMainThread])
    {
        block();
        return;
    }
    dispatch_async(dispatch_get_main_queue(), block);
}

/// 将框架授权选项映射为 UNAuthorizationOptions，不直接复用 Apple 枚举原始值。
static UNAuthorizationOptions NovaNativeMapAuthorizationOptions(uint64_t options)
{
    UNAuthorizationOptions nativeOptions = 0;
    if ((options & (1ULL << 0)) != 0) nativeOptions |= UNAuthorizationOptionAlert;
    if ((options & (1ULL << 1)) != 0) nativeOptions |= UNAuthorizationOptionSound;
    if ((options & (1ULL << 2)) != 0) nativeOptions |= UNAuthorizationOptionBadge;
    if ((options & (1ULL << 3)) != 0)
    {
        if (@available(iOS 12.0, *)) nativeOptions |= UNAuthorizationOptionProvisional;
    }
    return nativeOptions;
}

extern "C"
{
    /// 查询当前 UNNotificationSettings 授权状态。
    void NovaNative_GetNotificationPermissionStatus(
        uint64_t requestId,
        NovaNativeNotificationStatusCallback callback)
    {
        if (callback == nullptr) return;
        [[UNUserNotificationCenter currentNotificationCenter]
            getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings *settings)
            {
                int32_t status = (int32_t)settings.authorizationStatus;
                NovaNativeDispatchToMain(^{ callback(requestId, status); });
            }];
    }

    /// 请求通知权限，并在完成后重新查询 UNNotificationSettings 作为权威状态。
    void NovaNative_RequestNotificationPermission(
        uint64_t requestId,
        uint64_t options,
        NovaNativeNotificationRequestCallback callback)
    {
        if (callback == nullptr) return;
        UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
        [center requestAuthorizationWithOptions:NovaNativeMapAuthorizationOptions(options)
                              completionHandler:^(BOOL granted, NSError *error)
        {
            [center getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings *settings)
            {
                int32_t status = (int32_t)settings.authorizationStatus;
                int64_t errorCode = error == nil ? 0 : (int64_t)error.code;
                NSString *errorDomain = error == nil ? @"" : error.domain;
                NSString *errorMessage = error == nil ? @"" : error.localizedDescription;
                NovaNativeDispatchToMain(^{
                    callback(
                        requestId,
                        status,
                        errorCode,
                        errorDomain.UTF8String,
                        errorMessage.UTF8String);
                });
            }];
        }];
    }

    /// iOS 15.4 及以上打开通知设置，低版本回退应用设置。
    void NovaNative_OpenAppSettings(
        uint64_t requestId,
        NovaNativeOpenSettingsCallback callback)
    {
        if (callback == nullptr) return;
        NovaNativeDispatchToMain(^{
            NSString *settingsUrl = UIApplicationOpenSettingsURLString;
            if (@available(iOS 15.4, *))
            {
                settingsUrl = UIApplicationOpenNotificationSettingsURLString;
            }

            NSURL *url = [NSURL URLWithString:settingsUrl];
            if (url == nil)
            {
                callback(requestId, 0);
                return;
            }

            [[UIApplication sharedApplication]
                openURL:url
                options:@{}
                completionHandler:^(BOOL success)
                {
                    callback(requestId, success ? 1 : 0);
                }];
        });
    }
}

