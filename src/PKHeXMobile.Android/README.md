# Experimental Android read-only shell

This is a **source-level prototype**, not a certified APK or a complete PKHeX editor. It uses the official pinned PKHeX.Core 26.10.18 through the existing read-only projection, not PKForge's fork.

- Native .NET for Android Activity with a real system ACTION_OPEN_DOCUMENT picker; the selected URI is read through ContentResolver.OpenInputStream twice, without converting it to a filesystem path.
- Requests only a transient read grant; no broad-storage, internet, or write permission. It does not persist grants or save anything.
- Rejects changed bytes and (when supplied) changed opaque provider/document identity; displays trainer, party and occupied box slots for Core-recognized inputs. Rejection is displayed without saving.
- No verified real-game save fixtures, device/provider testing, accessibility audit, save export, bank support, or ROM-hack write profile. A successful projection is provisional and never authorizes writes.

Build on a development machine with .NET 10, the .NET Android workload, Java and Android SDK installed:

```sh
dotnet workload install android
dotnet build src/PKHeXMobile.Android/PKHeXMobile.Android.csproj -c Debug
```

The separate Android CI workflow runs only for changes to this project or on manual dispatch, with a bounded job timeout to avoid imposing a full Android workload on every documentation commit. CI compilation alone does not prove that a picker, provider or UI works on a physical device.

The independent UI strategy is **provisional** while Milestone 0 architecture and device gates remain open. Source references: [official .NET Android samples](https://github.com/dotnet/android-samples) and [Android ACTION_OPEN_DOCUMENT documentation](https://developer.android.com/training/data-storage/shared/documents-files).
