# JNI 全名、代理接口与系统清单 receiver 必须保留。
-keep class com.solotopia.nova.nativebridge.NovaNativeAppInstallerBridge { *; }
-keep class com.solotopia.nova.nativebridge.NovaNativeAppInstallerBridge$Callback { *; }
-keep class com.solotopia.nova.nativebridge.NovaNativeAppInstallerBridge$ResultReceiver { *; }
# apksig 内部含算法 provider/反射边界，首版保留完整验签库；不保留构建主机工具 jar。
-keep class com.android.apksig.** { *; }
