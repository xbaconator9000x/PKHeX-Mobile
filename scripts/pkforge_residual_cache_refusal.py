#!/usr/bin/env python3
"""Pinned PKForge diagnostic preview: refuse residual data sharing; keep QR pixels in memory.

No save edits or export permissions are introduced. All replacements are staged before writes.
"""
from pathlib import Path
from scripts.pkforge_offline_preview import checked_file, OfflineRefusal

BOX = "src/PKForge.App/Views/BoxBrowserPage.cs"
QR = "src/PKForge.App/Views/QrPopup.cs"
PARK = "src/PKForge.App/Views/PokeparkAlbumOverlay.cs"

SHOWDOWN_START = """    private async Task ExportBoxShowdownAsync()
    {
"""
SHOWDOWN_END = "    /// <summary>Fills the entire PC with a legal living dex"
SHOWDOWN_GUARD = """        if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
        {
            _viewModel.Status = "Diagnostic preview: Showdown sharing and clipboard export disabled.";
            return;
        }
"""

AUDIT_START = """    private static async Task ShareAuditReportAsync(string text)
    {
"""
AUDIT_END = "    /// <summary>Mass egg generation:"
AUDIT_GUARD = """        if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
            return;
"""

BINARY_START = """    public static Task ShowBinaryAsync(Grid host, string title, byte[] payload)
    {
"""
BINARY_END = "    private static Task ShowAsync(Grid host, string title, BitMatrix matrix)"
BINARY_GUARD = """        // A scannable .pk QR is an entity export, even without a share intent.
        if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
            return Task.CompletedTask;
"""

PARK_START = """    private async Task ShareAsync()
    {
"""
PARK_END = "    private void Turn(int delta)"
PARK_GUARD = """        if (PKForge.App.Services.PreviewRuntimePolicy.Offline)
            return;
"""

QR_DISK_START = '        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, "qr-latest.png");\n'
QR_DISK_END = "        Grid overlay = null!;"
QR_MEMORY = """        // Per-overlay immutable pixels: no shared cache file or cross-overlay overwrite.
        byte[] png;
        using (var bitmap = new SKBitmap(matrix.Width, matrix.Height))
        {
            for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < matrix.Width; x++)
                bitmap.SetPixel(x, y, matrix[x, y] ? SKColors.Black : SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            png = encoded.ToArray();
        }

        // The stream factory may run after Close on the MAUI image pipeline.
        // Never zero or mutate pixels captured by an outstanding stream.
        var qrImage = new Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(png, writable: false)),
            WidthRequest = 240,
            HeightRequest = 240,
            HorizontalOptions = LayoutOptions.Center,
        };

"""
QR_FILE_IMAGE = "Source = ImageSource.FromFile(path),"
QR_MEMORY_IMAGE = "Source = ImageSource.FromStream(() => new MemoryStream(png, writable: false)),"
QR_IMAGE_BLOCK = """Kit.LcdPanel(new Image
                {
                    Source = ImageSource.FromFile(path),
                    WidthRequest = 240,
                    HeightRequest = 240,
                    HorizontalOptions = LayoutOptions.Center,
                }, padding: 8),"""
QR_IMAGE_CALL = "Kit.LcdPanel(qrImage, padding: 8),"
QR_CLOSE_START = "        void Close()\n        {\n"
QR_CLOSE_START_NEW = "        var closed = false;\n        void Close()\n        {\n            if (closed) return;\n            closed = true;\n"
QR_CLOSE = "            done.TrySetResult();\n"
QR_RELEASE = "            qrImage.Source = null;\n            done.TrySetResult();\n"


def guard_method(source: str, start: str, end: str, required: tuple[str, ...], guard: str) -> str:
    if source.count(start) != 1 or source.count(end) != 1 or start + guard in source:
        raise OfflineRefusal("Pinned residual sharing method drift or repeated adaptation")
    a, b = source.index(start), source.index(end)
    if a >= b or any(token not in source[a:b] for token in required):
        raise OfflineRefusal("Pinned residual sharing body drift")
    return source.replace(start, start + guard, 1)


def replace_once(source: str, before: str, after: str) -> str:
    if source.count(before) != 1 or after in source:
        raise OfflineRefusal("Pinned QR source drift or repeated adaptation")
    return source.replace(before, after, 1)


def adapt_box(source: str) -> str:
    source = guard_method(source, SHOWDOWN_START, SHOWDOWN_END,
                          ("Clipboard.Default.SetTextAsync(text)", 'case "Share as file":',
                           "File.WriteAllTextAsync(path, text)", "Share.Default.RequestAsync"),
                          SHOWDOWN_GUARD)
    return guard_method(source, AUDIT_START, AUDIT_END,
                        ("File.WriteAllTextAsync(path, text)", "Share.Default.RequestAsync"),
                        AUDIT_GUARD)


def adapt_qr(source: str) -> str:
    source = guard_method(source, BINARY_START, BINARY_END,
                          ("Encoding.Latin1.GetString(payload)", "QRCodeWriter().encode"),
                          BINARY_GUARD)
    if source.count(QR_DISK_START) != 1 or source.count(QR_DISK_END) != 1:
        raise OfflineRefusal("Pinned QR disk method drift")
    a, b = source.index(QR_DISK_START), source.index(QR_DISK_END)
    if a >= b or any(x not in source[a:b] for x in
                     ("File.Create(path)", "encoded.SaveTo(file)", "SKImage.FromBitmap(bitmap)")):
        raise OfflineRefusal("Pinned QR disk encoder changed")
    source = source[:a] + QR_MEMORY + source[b:]
    source = replace_once(source, QR_IMAGE_BLOCK, QR_IMAGE_CALL)
    source = replace_once(source, QR_CLOSE_START, QR_CLOSE_START_NEW)
    return replace_once(source, QR_CLOSE, QR_RELEASE)


def adapt_park(source: str) -> str:
    return guard_method(source, PARK_START, PARK_END,
                        ("File.Copy(photo.Path, copy, overwrite: true)",
                         "Share.Default.RequestAsync"), PARK_GUARD)


def extend(root: Path, staged: dict[Path, str]) -> dict[Path, str]:
    """Merge into the already staged private-storage adapter, without partial writes."""
    root = root.resolve(strict=True)
    box = root / BOX
    if box not in staged:
        raise OfflineRefusal("Private-storage BoxBrowserPage stage missing")
    qr, park = checked_file(root, QR), checked_file(root, PARK)
    staged[box] = adapt_box(staged[box])
    staged[qr] = adapt_qr(qr.read_text(encoding="utf-8"))
    staged[park] = adapt_park(park.read_text(encoding="utf-8"))
    return staged
