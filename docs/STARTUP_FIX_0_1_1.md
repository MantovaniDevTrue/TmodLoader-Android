# Diagnostic APK 0.1.1 — Android startup registration

## Observed failure

On the user's Xiaomi/Android 14, 0.1.0 terminated before displaying the diagnostic interface:

`java.lang.UnsatisfiedLinkError: No implementation found for void crc645f428b8dbfb46340.MainActivity.n_onCreate(android.os.Bundle)`

This happens at the Java-to-managed Activity callback boundary, before our C# `OnCreate` body, FNA initialization or any Terraria/mod loading. The screenshot identifies the missing callback but does not by itself establish why the runtime failed to register it.

## Change

- Set `AndroidEnableMarshalMethods=false` explicitly. The original Release build evaluated this property to `True` and used the SDK's native marshal-method registration path.
- A clean rebuild now generates `__md_methods` descriptors and `mono.android.Runtime.register(...)` in both the Activity and custom View wrappers.
- Preserve .NET 10, Mono JIT, the ARM64 ABI, dynamic DLL loading and the existing controls.
- Increase versionCode to 2 and versionName to 0.1.1, so it updates the previous installation.
- Add startup, first draw, first touch and runtime-probe log markers.
- Add a regression check against generated Java and the actual packaged DEX registration strings.
- Add an x86_64 Android emulator startup job, separate from the ARM64 release build.

Dynamic registration is a compatibility workaround for the failed native registration route. A successful metadata check is not proof of a successful device launch. The SDK documentation describes marshal methods as a startup optimization and documents disabling them when startup fails: [official .NET 10 notes](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10).

## Completed local checks

- Clean Release compilation completed.
- 22 shared input/runtime checks passed.
- New JNI registration metadata check passes for the 0.1.1 APK's Activity and View.
- The same check rejects the old 0.1.0 APK because its DEX does not include the required dynamic registration descriptor for `n_onCreate`.
- APK v2/v3 signatures verified.
- All 10 packaged native libraries are AArch64 ELF64, with 16 KiB-compatible LOAD/ZIP alignment.

ARM64 APK SHA-256:

`5085cc4e76709a570b94e2bbe4c55e29d168282050d4b17ce3ab0b2800a1a804`

## Runtime validation

Emulator execution is pending in the new workflow. The smoke test waits for actual Activity creation, first draw and successful external DLL/Reflection.Emit probes, sends a touch, moves the app into the background, resumes it and checks that the process survives without fatal exceptions. It retains its log and a screenshot as workflow artifacts.

No ARM64 device is attached to the local build environment. Confirmation on the user's phone remains necessary. This remains a controls/runtime diagnostic APK; it does not start Terraria yet.
