/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaThirdPaySafeBrowsing.mm
 * author:    yingzheng
 * created:   2026/11/16
 * descrip:   ThirdPay 独立系统浏览器页面桥接
 ***************************************************************/

#import <UIKit/UIKit.h>
#import <SafariServices/SafariServices.h>
#include <stdint.h>

extern "C" UIViewController *UnityGetGLViewController();
typedef void (*NovaThirdPayBrowserCallback)(int64_t sessionId, int eventCode);

/// <summary>
/// 强持有页面与 delegate，展示和关闭期间只允许一个原生支付页面。
/// </summary>
@interface NovaThirdPayBrowser : NSObject <SFSafariViewControllerDelegate, UIAdaptivePresentationControllerDelegate>
@property(nonatomic, strong) SFSafariViewController *controller;
@property(nonatomic, assign) int64_t sessionId;
@property(nonatomic, assign) NovaThirdPayBrowserCallback callback;
@property(nonatomic, assign) BOOL presenting;
@property(nonatomic, assign) BOOL closing;
@property(nonatomic, assign) BOOL dismissRequested;
- (void)closePage;
- (void)finishUserDismissal;
- (void)releasePage;
@end

static NovaThirdPayBrowser *s_CurrentBrowser;

@implementation NovaThirdPayBrowser

/// <summary>
/// 展示动画期间登记关闭请求，展示完成后再 dismiss，避免 UIKit 丢弃关闭操作。
/// </summary>
- (void)closePage
{
    self.dismissRequested = YES;
    self.callback = nullptr;
    if (self.presenting || self.closing) return;
    self.closing = YES;
    if (self.controller.presentingViewController == nil) { [self releasePage]; return; }
    [self.controller dismissViewControllerAnimated:YES completion:^{
        [self releasePage];
    }];
}

/// <summary>
/// 系统手势已完成关闭时同步释放，避免等待不会再次触发的 dismiss 动画回调。
/// </summary>
- (void)releasePage
{
    self.controller.delegate = nil;
    self.controller.presentationController.delegate = nil;
    if (s_CurrentBrowser == self) s_CurrentBrowser = nil;
    self.controller = nil;
}

/// <summary>
/// 完成按钮是用户关闭；先关闭页面再通知托管层，且只通知一次。
/// </summary>
- (void)safariViewControllerDidFinish:(SFSafariViewController *)controller
{
    [self finishUserDismissal];
}

/// <summary>
/// 兼容系统交互式手势关闭；主动关闭已清空 callback，因此不会重复报告用户取消。
/// </summary>
- (void)presentationControllerDidDismiss:(UIPresentationController *)presentationController
{
    [self finishUserDismissal];
}

/// <summary>
/// 保存用户事件后移除回调，再调用幂等关闭；通知不携带支付状态或 URL。
/// </summary>
- (void)finishUserDismissal
{
    NovaThirdPayBrowserCallback callback = self.callback;
    self.callback = nullptr;
    [self closePage];
    if (callback != nullptr) callback(self.sessionId, 3);
}
@end

/// <summary>
/// 主队列展示系统网页；参数与模态冲突均显式失败，不修改 Unity AppController。
/// </summary>
extern "C" void NovaThirdPaySafeBrowsingShow(const char *rawUrl, int64_t sessionId, NovaThirdPayBrowserCallback callback)
{
    NSString *urlString = rawUrl == nullptr ? nil : [NSString stringWithUTF8String:rawUrl];
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL *url = urlString == nil ? nil : [NSURL URLWithString:urlString];
        UIViewController *presenter = UnityGetGLViewController();
        BOOL validScheme = [url.scheme.lowercaseString isEqualToString:@"https"] || [url.scheme.lowercaseString isEqualToString:@"http"];
        if (sessionId <= 0 || url.host.length == 0 || !validScheme || s_CurrentBrowser != nil
            || presenter == nil || presenter.view.window == nil || presenter.presentedViewController != nil
            || presenter.isBeingDismissed || presenter.isBeingPresented)
        {
            if (callback != nullptr) callback(sessionId, 2);
            return;
        }
        NovaThirdPayBrowser *browser = [NovaThirdPayBrowser new];
        browser.sessionId = sessionId;
        browser.callback = callback;
        browser.presenting = YES;
        browser.controller = [[SFSafariViewController alloc] initWithURL:url];
        browser.controller.delegate = browser;
        s_CurrentBrowser = browser;
        [presenter presentViewController:browser.controller animated:YES completion:^{
            browser.presenting = NO;
            if (browser.dismissRequested) { [browser closePage]; return; }
            if (browser.callback != nullptr) browser.callback(sessionId, 1);
        }];
        browser.controller.presentationController.delegate = browser;
    });
}

/// <summary>
/// 只关闭对应会话；旧页面的迟到关闭请求不能影响新页面。
/// </summary>
extern "C" void NovaThirdPaySafeBrowsingDismiss(int64_t sessionId)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (s_CurrentBrowser.sessionId == sessionId) [s_CurrentBrowser closePage];
    });
}
