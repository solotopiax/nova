#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>

typedef void (*NovaThirdPayStorefrontRegionCallback)(const char *, const char *);

extern "C" {
void NovaThirdPayGetStorefrontRegion(NovaThirdPayStorefrontRegionCallback callback) {
    if (callback == NULL) {
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *countryCode = @"";
        NSString *identifier = @"";

        if (@available(iOS 11.0, *)) {
            SKStorefront *storefront = [SKPaymentQueue defaultQueue].storefront;
            if (storefront != nil) {
                countryCode = storefront.countryCode ?: @"";
                identifier = storefront.identifier ?: @"";
            } else {
                countryCode = [[NSLocale currentLocale] countryCode] ?: @"";
            }
        } else {
            countryCode = [[NSLocale currentLocale] countryCode] ?: @"";
        }

        if (countryCode.length == 0) {
            countryCode = @"unknown";
        }

        if (identifier.length == 0) {
            identifier = @"0";
        }

        callback([countryCode UTF8String], [identifier UTF8String]);
    });
}
}

