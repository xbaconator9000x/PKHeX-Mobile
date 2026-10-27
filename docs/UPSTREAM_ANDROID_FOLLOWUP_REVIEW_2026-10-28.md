# Android follow-up audit: cancellation, box navigation and parity boundaries — pass E (2026-10-28)

**Evidence tier: source-inspected; not device-executed.** This is a delta audit after the Android presentation and CI changes at immutable PKHeX-Mobile commit [5f5a0daa3277eef50af7203e6efd50178c9406f8](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/5f5a0daa3277eef50af7203e6efd50178c9406f8). It does not replace the earlier [pass D](UPSTREAM_ANDROID_PROTOTYPE_REVIEW_2026-10-28.md). It reports source facts and explicit tests, not observed device failures or claims of certified formats.

## Inputs and provenance

| Repository | Inspected immutable commit | Tracked blobs in recursive Git tree | Purpose |
| --- | --- | ---: | --- |
| PKHeX-Mobile | [5f5a0daa3277eef50af7203e6efd50178c9406f8](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/5f5a0daa3277eef50af7203e6efd50178c9406f8) | 54 (tree rechecked for this pass; do not infer test coverage from file count) | Current native Android UI, Domain, Core adapter, CI, tests |
| Official PKHeX | [7e26bc53215e4f7bc0041ca375f1042539cb81b3](https://github.com/kwsch/PKHeX/tree/7e26bc53215e4f7bc0041ca375f1042539cb81b3) | 11,704 | Canonical Core source and save metadata/export boundaries |
| PKForge | [2df04430e532b78c247f45b936f3068d8f824987](https://github.com/sofianeelhor/PKForge/tree/2df04430e532b78c247f45b936f3068d8f824987) | 3,786 | Android SAF, picker and box UI candidate |
| PKHeX MAUI | [a6f7cfb78e1c96155baa67660fd7ae97895e375f](https://github.com/santacrab2/PKHeXMAUI/tree/a6f7cfb78e1c96155baa67660fd7ae97895e375f) | 5,955 | MAUI box editor and packaging candidate |

**Important version separation:** mobile executes exact NuGet PKHeX.Core [26.10.18], associated with release-tag commit bfe89c74a3f5265805e364b79bc04ad38ebbc5ea. The inspected official source revision above is newer; Forge uses its fork and MAUI references 26.7.7. Neither a cross-version source comparison nor this document is a same-version desktop parity test. NuGet package-to-source byte identity remains unproven.

At the inspected HEAD, [Foundation checks](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40540543652) and [Android compilation](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40540543579) completed successfully. The Android job retains a debug APK and hash/provenance artifacts; it does **not** install or run the APK. No independently verified real-save fixture, physical provider run, format certification, or write gate is complete.

## Source-backed findings and implementer handoff

### E-CI-01 — Android transitive-code trigger is now covered in source (P1)

The Android workflow now includes Android, ReadOnly, Domain, build-property, NuGet and global.json paths for push and pull_request. This resolves the previously documented static path-filter omission. It is not an independently exercised path-only PR gate.

Evidence: [android-build.yml](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/.github/workflows/android-build.yml#L3-L24).
**Acceptance / implementer task:** Run isolated ReadOnly-only, Domain-only and dependency-only PRs and capture the actual Android job dispatch. Keep the trigger list synchronized with new transitive project dependencies.

### E-CI-02 — Debug artifact provenance improved but not reproducible (P1)

The workflow emits commit, runner, dotnet workload and Java versions, plus SHA-256 for built APKs, and retains the debug artifact for seven days. It still uses floating 10.0.x, workload installation, major-version GitHub actions and no tracked SDK/NuGet lockfile. A hash of one build is not a reproducibility proof.

Evidence: [android-build.yml](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/.github/workflows/android-build.yml#L33-L73); [PKHeXMobile.ReadOnly.csproj](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/PKHeXMobile.ReadOnly.csproj#L7-L11).
**Acceptance / implementer task:** Record resolved Android SDK/build-tools, dotnet SDK and workload manifests, package-lock hashes, Java, action SHAs and build flags. Rebuild from clean independent runners and compare package content after accounting for nondeterministic metadata.

### E-CI-03 — Foundation tests are managed smoke checks, not Android runtime checks (P1)

The foundation workflow builds Domain and runs Domain, ReadOnly and Verify smoke programs and two CLI help paths; the Android workflow only compiles and archives an APK. Neither job installs an APK, launches the Activity or exercises SAF picker results.

Evidence: [repository-validation.yml](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/.github/workflows/repository-validation.yml#L11-L33); [android-build.yml](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/.github/workflows/android-build.yml#L33-L73).
**Acceptance / implementer task:** Add a separately reported emulator install/launch/picker-cancel smoke gate; do not rename compilation to device certification.

### E-LIFE-01 — Cancellation control exists but provider open is synchronous (P1)

A visible Cancel inspection button cancels a linked token and a lifetime token is cancelled on destruction. However the provider delegate calls ContentResolver.OpenInputStream synchronously, after only a pre-open token check; a blocked provider open has no cancellation token passed into the Android call. Task.Run avoids UI-thread blocking but cannot guarantee prompt cancellation of the blocked open.

Evidence: [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L125-L134); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L151-L177); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L284-L291).
**Acceptance / implementer task:** Fault-inject a provider that stalls opening, reading and second-open separately; measure cancellation latency, descriptor release and ability to reselect. Do not claim bounded provider latency without actual evidence.

### E-LIFE-02 — Legacy picker state is transient across recreation (P1)

The Activity uses StartActivityForResult and a private _pickerPending flag; it does not persist the pending request generation. Recreated instances explicitly ask for reselection and avoid retaining save bytes. Source alone does not establish how Android returns an outstanding picker result after recreation, rapid Home/Back or process death.

Evidence: [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L20-L35); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L98-L122); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L136-L161).
**Acceptance / implementer task:** Instrument rotation while picker is open and while both provider opens are pending; assert no stale result and no duplicate picker, and document intentional reselect behavior.

### E-LIFE-03 — Typed error messages are improved but Android security exceptions need coverage (P1)

The Activity now separates System.UnauthorizedAccessException, InvalidDataException and IOException from generic failure. Android providers may signal access failure through Java/Android exception types; the code does not explicitly classify those types. Generic refusal is safer than crashing but does not establish useful provider-specific diagnosis.

Evidence: [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L211-L245); [AndroidSafFileAccess.cs](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Platforms/Android/AndroidSafFileAccess.cs#L14-L20).
**Acceptance / implementer task:** Inject Java.Lang.SecurityException, Java.Lang.IllegalArgumentException, provider null stream, cancellation and I/O errors; verify redacted, actionable UI messages without URI or save-data logging.

### E-UI-01 — Single-box browsing closes the all-box text-dump defect, not full UI parity (P1)

A Spinner now selects one box, while a pure presenter preserves occupied source slot indices and limits output to one selected box. Party and box content remain text rather than a slot grid or Pokémon inspector; selection and accessibility are untested on Android.

Evidence: [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L72-L95); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L188-L209); [ReadOnlyProjectionPresenter.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/ReadOnlyProjectionPresenter.cs#L38-L74).
**Acceptance / implementer task:** Keep the source slot index stable in a portrait-first, accessible per-slot grid; verify sparse and full boxes, TalkBack, focus and rotation before adding editing affordances.

### E-UI-02 — Nickname sanitization misses Unicode formatting and bidirectional controls (P2)

The presenter replaces char.IsControl characters and caps rendered names at 80 UTF-16 characters. Format characters such as U+202E and U+2066 are not ordinary control characters; they can visually reorder or isolate text in a label. This is a source-level display-spoofing test gap, not evidence of a malicious save.

Evidence: [ReadOnlyProjectionPresenter.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/ReadOnlyProjectionPresenter.cs#L76-L93).
**Acceptance / implementer task:** Add tests for Unicode categories Format, surrogate pairs, direction overrides/isolates, long grapheme clusters and newline spoofing; display escaped or safely normalized user-controlled strings.

### E-IO-01 — Two-open identity remains URI-derived, not provider revision authentication (P1)

GetDocumentIdentity reads authority and document ID from the selected URI on each open; no independent provider etag, generation or version is retrieved. The session compares that hint and a SHA-256 snapshot. Same-byte replacement behind the same URI can remain undetectable, and none of this authorizes writing.

Evidence: [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L164-L177); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L274-L282); [SaveDocumentSession.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Domain/SaveDocumentSession.cs#L54-L68).
**Acceptance / implementer task:** Retain the current read-only claim; for any future write path, require provider-specific revision semantics or a separately safe publication strategy with independently verified backup and readback.

### E-IO-02 — The reader is not a positive retail/hack authenticator (P1)

The risk analyzer rejects some known compressed/custom layouts, then official Core detection and ChecksumsValid gate the provisional projection. A retail-shaped ROM hack without known markers can still be parsed as retail. The result explicitly has no verified profile and cannot authorize save export.

Evidence: [SaveInputRiskAnalyzer.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Domain/SaveInputRiskAnalyzer.cs#L45-L103); [ReadOnlySaveInspector.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs#L41-L80); [ReadOnlySaveInspector.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs#L109-L132).
**Acceptance / implementer task:** Create independent positive and negative fixtures for each claimed game/layout and mutated/near-miss sector cases; leave all custom and ambiguous formats read-only.

### E-TEST-01 — The only positive parser smoke fixture is same-Core-generated Gen 1 (P1)

The ReadOnly smoke program constructs SAV1, writes it with the same pinned Core and compares its own projection; negative tests use synthetic markers and in-memory providers. This establishes a smoke check, not independent desktop parity or device/provider validation.

Evidence: [Program.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/tests/PKHeXMobile.ReadOnly.Smoke/Program.cs#L119-L156); [Program.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/tests/PKHeXMobile.ReadOnly.Smoke/Program.cs#L301-L336).
**Acceptance / implementer task:** Build a private manifest with independently authored expectations and source SHA, then compare same-version desktop 26.10.18 and Android output across consented Gen 1–9 variants; do not commit saves.

### E-CORE-01 — Official Core detection includes custom readers and envelope handlers (P1)

The newer pinned official SaveUtil first tries registered custom readers, then raw save detection, then envelope handlers; handler success attaches header/footer metadata. The current mobile reader calls its packaged Core without passing a path, and projects inner fields only. Envelope identification and write-preserving metadata must be tested separately, not inferred from a successful projection.

Evidence: [SaveUtil.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/SaveUtil.cs#L489-L505); [SaveUtil.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/SaveUtil.cs#L537-L604); [ReadOnlySaveInspector.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs#L58-L70).
**Acceptance / implementer task:** For each wrapper and footer, compare exact input bytes and desktop metadata on the same Core version; never discard outer bytes or infer a safe round-trip from parsed inner Pokémon.

### E-CORE-02 — Serialization can alter checksums and envelope bytes even on a no-op (P0)

Official SaveFile.Write invokes GetFinalData, which calls SetChecksums, then Metadata.Finalize. The current mobile code correctly never calls Write. A future editing/export path must independently validate the entire envelope, permitted differences and untouched regions; a parser success or same-Core synthetic test is insufficient.

Evidence: [SaveFile.cs](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/SaveFile.cs#L43-L56); [ReadOnlySaveInspector.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs#L34-L39).
**Acceptance / implementer task:** Keep write authorization disabled; require no-op byte comparisons, per-block checksum verification, Save As staging, independent reparse, readback and restore proof for each profile.

### E-UP-01 — Do not inherit Forge's in-place SAF truncate/write path (P0)

Forge's picker requests write/persistable grants; AndroidSafFileAccess.WriteAtomicallyAsync opens the existing URI in rwt mode, writes and flushes directly. The method name does not establish atomic replacement; a provider failure can leave partial content. Our app requests read-only access and must preserve that boundary.

Evidence: [AndroidDocumentPicker.cs](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Platforms/Android/AndroidDocumentPicker.cs#L14-L33); [AndroidSafFileAccess.cs](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Platforms/Android/AndroidSafFileAccess.cs#L24-L39); [MainActivity.cs](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f5a0daa3277eef50af7203e6efd50178c9406f8/src/PKHeXMobile.Android/MainActivity.cs#L98-L108).
**Acceptance / implementer task:** Do not copy this write path. Prototype new-document Save As and failure-injected publication with verified original-byte backup before considering overwrite.

### E-UP-02 — MAUI drag/drop is not a safe reusable box transaction (P1)

The MAUI BoxTab renders a six-column CollectionView, but its drop handler falls back to a newly constructed boxsprite when a destination is not found; IndexOf on that fallback can be -1 and is passed to SetBoxSlotAtIndex. Its catch branch then attempts another write without rollback of any preceding mutation. This is a source-level hazard requiring reproduction, not a demonstrated runtime corruption incident.

Evidence: [BoxTab.xaml.cs](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BoxTab.xaml.cs#L48-L105); [BoxTab.xaml.cs](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/boxtab/BoxTab.xaml.cs#L144-L185).
**Acceptance / implementer task:** For future mobile box operations, use validated stable indices and a transaction/snapshot with failure-injection tests for missing targets, partial swaps and remote injection failures; do not port this method unchanged.

### E-LIC-01 — Code licence and embedded font/art rights are separate (P1)

The official Core project declares GPL-3.0-or-later, and both candidate repos include GPLv3 licence texts. Forge separately identifies NDS12 as CC BY-NC-SA 3.0, which is not a blanket permission for commercial redistribution. MAUI bundles sprite resource collections and other dependencies. No upstream art or candidate UI source was imported in this audit.

Evidence: [PKHeX.Core.csproj](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/PKHeX.Core.csproj#L9-L19); [NDS12-license.txt](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Resources/Fonts/NDS12-license.txt#L1-L6); [PKHeX.MAUI.csproj](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/PKHeX.MAUI.csproj#L90-L116).
**Acceptance / implementer task:** Maintain per-file provenance and source availability; avoid importing NC fonts, proprietary sprites, compiled binaries or candidate UI until each asset and licence is independently cleared.

## Reconciled pass D status

| Prior finding | Source disposition at pass E | Evidence limit |
| --- | --- | --- |
| D-CI-01 missing shared-code Android triggers | Addressed in YAML; real isolated PR scheduling not exercised here | E-CI-01 |
| D-CI-02 build reproducibility | APK hash and environment text added; full dependency/toolchain lock and two-run reproducibility still missing | E-CI-02 |
| D-LIFE-03 explicit cancellation | Cancel button and linked token added; blocked provider open and device cancellation unverified | E-LIFE-01 |
| D-LIFE-02 error classification | System exception categories added; Android provider exception mapping unverified | E-LIFE-03 |
| D-UI-01 all-box text dump | One-box selector and pure formatter added; grid, details, accessibility and device tests still missing | E-UI-01 |
| D-IO-02 provider identity | Still URI-derived, not an independent revision; no change to write gate | E-IO-01 |
| D-CI-04 README status | Generic heading preserved, but body still describes the pre-Android foundation; implementation owner should correct prose without changing the heading | Existing ticket remains open |

## Priority and architecture disposition

**P0:** keep all save writes, bank replacement and ROM-hack editing disabled; do not copy Forge's direct SAF overwrite or MAUI's nontransactional box drag/drop. **P1:** independently verify same-version desktop fixtures, Android provider cancellation/recreation, per-file rights, transitive CI dispatch and build reproducibility. **P2:** Unicode-safe labels and accessible slot browsing.

Provisional architecture remains official pinned PKHeX.Core + independent Android UI + path-free read-only provider boundary. No source or assets were copied from candidates; no decision to adapt their UI wholesale has been authorized. Milestone 0's reproducible upstream builds and architecture gate, and Milestone 1's independent read-only format/device verification, remain **open**. See the [24-case execution matrix](PARITY_ANDROID_FOLLOWUP_GATES_2026-10-28.md).
