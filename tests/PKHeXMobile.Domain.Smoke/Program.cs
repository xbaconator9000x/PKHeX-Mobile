using PKHeXMobile.Domain;

static void MustBlock(Action attempt)
{
    try { attempt(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected this write to be blocked.");
}

MustBlock(() => SaveWriteGuard.RequireAuthorized(new("unknown", SaveCompatibility.Unknown, null), 128, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedReadOnly, "game:v1"), 128, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, null), 128, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, "game:v1"), 0, 128));
MustBlock(() => SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, "game:v1"), 128, 64));

SaveWriteGuard.RequireAuthorized(new("known", SaveCompatibility.SupportedWrite, "game:v1"), 128, 128);
Console.WriteLine("SaveWriteGuard smoke checks passed (contract only; no real save parsing or writes).");
