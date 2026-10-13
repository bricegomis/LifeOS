#import <Foundation/Foundation.h>
#import <Capacitor/Capacitor.h>

CAP_PLUGIN(LifeosNativeBridgePlugin, "LifeosNativeBridge",
    CAP_PLUGIN_METHOD(saveWidgetSnapshot, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(clearWidgetSnapshot, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(getCookingSession, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(saveCookingSession, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(clearCookingSession, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(startLiveActivity, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(updateLiveActivity, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(endLiveActivity, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(reloadWidgetTimelines, CAPPluginReturnPromise);
)
