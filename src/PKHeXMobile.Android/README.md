# Experimental Android read-only shell

This is a **source-level prototype**, not a certified APK or a complete PKHeX editor. It uses the official pinned PKHeX.Core 26.10.18 through the existing read-only projection, not PKForge's fork.

- Native .NET for Android Activity with a real system ACTION_OPEN_DOCUMENT picker; the selected URI is read through ContentResolver.OpenAssetFileDescriptor in read-only mode twice, using a cancellation signal during each provider open, without converting it to a filesystem path.
- Requests only a transient read grant; no broad-storage, internet, or write permission. It does not persist grants or save anything.
- Rejects changed bytes and (when supplied) changed opaque provider/document identity; displays trainer and party, with a selectable, six-column read-only slot grid (including empty slots) and a per-slot detail view for Core-recognized inputs. Rejection is displayed without saving.
- An explicit cancel button requests cancellation, stale results are ignored after Activity destruction, and rotation requires reselecting a save. A blocking or non-cooperative provider may still ignore cancellation, and read cancellation is not guaranteed to interrupt a native provider. Provider fault injection and device validation remain pending.
- The grid is navigational only, uses text rather than third-party Pokémon artwork, and never modifies or exports a slot. Trainer and Pokémon labels filter Unicode bidirectional/format controls and truncate without splitting grapheme clusters.
- No verified real-game save fixtures, device/provider testing, accessibility audit, save export, bank support, or ROM-hack write profile. A successful projection is provisional and never authorizes writes.

Build on a development machine with .NET 10, the .NET Android workload, Java and Android SDK installed:

```sh
dotnet workload install android
dotnet build src/PKHeXMobile.Android/PKHeXMobile.Android.csproj -c Debug
```

The separate Android CI workflow runs for changes to the Android project, its transitive Domain/ReadOnly dependencies, build settings, or on manual dispatch, with a bounded job timeout to avoid imposing a full Android workload on every documentation commit. A successful Android CI run retains a short-lived **debug** APK artifact, a SHA-256 manifest and build provenance for manual testing; it is not a signed release or evidence of device compatibility. CI compilation alone does not prove that a picker, provider or UI works on a physical device. The document ID shown by the read-only boundary is derived from the selected URI, not an independently authenticated provider revision.

The independent UI strategy is **provisional** while Milestone 0 architecture and device gates remain open. Source references: [official .NET Android samples](https://github.com/dotnet/android-samples) and [Android ACTION_OPEN_DOCUMENT documentation](https://developer.android.com/training/data-storage/shared/documents-files).
