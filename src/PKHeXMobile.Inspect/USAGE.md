# Read-only diagnostic harness (not an Android app)

From the repository root with the .NET 10 SDK installed:

```sh
dotnet run --project src/PKHeXMobile.Inspect -- --help
dotnet run --project src/PKHeXMobile.Inspect -- "/path/to/consented-save.sav"
```

The program reads a bounded, immutable snapshot, inspects it with the pinned PKHeX.Core, reopens the source to detect changed bytes, and prints a JSON trainer/party/box projection. **It never writes or exports a save.** JSON output may contain private trainer/Pokémon names; do not post it publicly without review.

This is an offline diagnostic tool for independently owned or consented fixtures. It does not certify vanilla save identity, ROM-hack support, legality, desktop parity, Android SAF behavior, or any write capability. A rejected save is not proof the underlying game is unsupported by PKHeX.Core.
