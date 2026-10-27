using System.Text.Json;
using System.Text.Json.Serialization;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

// Diagnostic harness only. Android UI and verified game-format support are not yet implemented.
if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: PKHeXMobile.Inspect <save-file>");
    Console.WriteLine("Experimental read-only inspection; no save modification or export is possible.");
    Console.WriteLine("Only use saves you own or have permission to inspect.");
    return 0;
}
if (args.Length != 1)
{
    Console.Error.WriteLine("Expected exactly one save-file path. Use --help for usage.");
    return 64;
}

try
{
    var path = args[0];
    var session = await SaveDocumentSession.OpenAsync(cancellationToken =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult(stream);
    });

    if (!ReadOnlySaveInspector.TryInspect(session.Original, out var view, out var reason))
    {
        Console.Error.WriteLine($"Read-only inspection rejected the input: {reason}");
        return 2;
    }

    // Detect changes during parsing before publishing even a read-only projection.
    await session.VerifySourceUnchangedAsync();
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    Console.WriteLine(JsonSerializer.Serialize(view, options));
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine("Could not inspect this document, or its contents changed during inspection.");
    return 3;
}
