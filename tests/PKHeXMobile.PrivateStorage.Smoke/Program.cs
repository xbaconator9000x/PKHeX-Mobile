// Synthetic-only diagnostic persistence refusal against actual adapted infrastructure.
using System.Reflection;
using PKForge.Infrastructure;

var root = Path.Combine(Path.GetTempPath(), "pkforge-private-smoke-" + Guid.NewGuid().ToString("N"));
try
{
    var bankRoot = Path.Combine(root, "bank");
    var bank = new FileBankService(bankRoot);
    var ensure = typeof(FileBankService).GetMethod("EnsureWritable", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new Exception("Missing bank mutation boundary");
    try
    {
        ensure.Invoke(bank, null);
        throw new Exception("Bank mutation was not refused");
    }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException reason
        && reason.Message.Contains("Diagnostic preview"))
    {
    }

    var backup = new FileBackupService(Path.Combine(root, "backups"));
    try
    {
        await backup.CreateAsync(null!);
        throw new Exception("Raw-save backup was not refused");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Diagnostic preview"))
    {
    }
    if (Directory.Exists(Path.Combine(root, "backups")) ||
        Directory.EnumerateFiles(bankRoot).Any())
        throw new Exception("Diagnostic preview wrote private backup/bank data");
    Console.WriteLine("Diagnostic bank and raw-save backup persistence refusals passed.");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}
