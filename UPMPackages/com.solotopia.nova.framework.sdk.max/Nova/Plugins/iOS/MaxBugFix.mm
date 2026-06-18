//
//  WindowSceneFix.mm
//  UnityFramework
//
//  Fixes UIWindowScene association for third-party SDK windows on Unity 6.
//  Without this, windows created by SDKs (e.g. AppLovin consent alert) appear
//  behind Unity's rendering surface because they lack a UIWindowScene association.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <objc/runtime.h>

@interface UIWindow (WindowSceneFix)
- (void)wsf_makeKeyAndVisible;
@end

@implementation UIWindow (WindowSceneFix)

+ (void)load
{
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Method originalMethod = class_getInstanceMethod([UIWindow class], @selector(makeKeyAndVisible));
        Method swizzledMethod = class_getInstanceMethod([UIWindow class], @selector(wsf_makeKeyAndVisible));
        if (originalMethod && swizzledMethod)
        {
            method_exchangeImplementations(originalMethod, swizzledMethod);
        }
    });
}

- (void)wsf_makeKeyAndVisible
{
    if (@available(iOS 13.0, *))
    {
        if (self.windowScene == nil)
        {
            // Find the active foreground UIWindowScene
            for (UIScene *scene in [UIApplication sharedApplication].connectedScenes)
            {
                if (scene.activationState == UISceneActivationStateForegroundActive &&
                    [scene isKindOfClass:[UIWindowScene class]])
                {
                    self.windowScene = (UIWindowScene *)scene;
                    NSLog(@"[WindowSceneFix] Assigned active windowScene to window: %@", self);
                    break;
                }
            }

            // Fallback: use any connected UIWindowScene
            if (self.windowScene == nil)
            {
                for (UIScene *scene in [UIApplication sharedApplication].connectedScenes)
                {
                    if ([scene isKindOfClass:[UIWindowScene class]])
                    {
                        self.windowScene = (UIWindowScene *)scene;
                        NSLog(@"[WindowSceneFix] Assigned fallback windowScene to window: %@", self);
                        break;
                    }
                }
            }
        }
    }

    // Call the original makeKeyAndVisible (method is swizzled, so this calls the real impl)
    [self wsf_makeKeyAndVisible];
}

@end