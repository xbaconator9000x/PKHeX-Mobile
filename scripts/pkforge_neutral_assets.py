#!/usr/bin/env python3
"""Rights-neutral diagnostic PKForge asset cutover, applied only to a pinned checkout.

Removes explicit upstream MAUI media/font declarations and substitutes original
vector icon/splash. This is NOT redistribution clearance: compiled APK contents
and dependencies still require independent review.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re

from scripts.pkforge_offline_preview import checked_file, verify_pins, OfflineRefusal

APP = "src/PKForge.App/"
PROJECT = APP + "PKForge.App.csproj"
GAME_ART = APP + "Services/GameArt.cs"
HOME = APP + "Views/HomePage.cs"
ABOUT = APP + "Views/AboutPopup.cs"
MAUI = APP + "MauiProgram.cs"
PIXEL = APP + "Views/PixelFont.cs"
ICON = APP + "Resources/AppIcon/neutral-preview.svg"
SPLASH = APP + "Resources/Splash/neutral-preview.svg"

class NeutralAssetRefusal(OfflineRefusal):
    pass

# Original geometric primitives; no third-party font, icon, sprite or game asset.
ICON_SVG = '''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<rect width="256" height="256" rx="48" fill="#1b2636"/>
<rect x="54" y="54" width="148" height="148" rx="18" fill="#f4f5f7"/>
<rect x="79" y="83" width="98" height="16" rx="8" fill="#27374a"/>
<rect x="79" y="120" width="98" height="16" rx="8" fill="#27374a"/>
<rect x="79" y="157" width="65" height="16" rx="8" fill="#27374a"/>
</svg>
'''
SPLASH_SVG = '''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<rect width="256" height="256" rx="48" fill="#1b2636"/>
<rect x="62" y="52" width="132" height="152" rx="16" fill="#f4f5f7"/>
<rect x="84" y="88" width="88" height="14" rx="7" fill="#27374a"/>
<rect x="84" y="126" width="88" height="14" rx="7" fill="#27374a"/>
<rect x="84" y="164" width="55" height="14" rx="7" fill="#27374a"/>
</svg>
'''
PIXEL_SOURCE = '''using SkiaSharp;

namespace PKForge.App.Views;

/// <summary>System-font fallback for rights-neutral offline diagnostics.
/// No bundled font or cached font bytes are opened or written.</summary>
public static class PixelFont
{
    public static SKTypeface Face => SKTypeface.Default;
    public static SKTypeface FallbackFace => SKTypeface.Default;
    public static Task WarmAsync() => Task.CompletedTask;
    public static Task WarmFallbackAsync() => Task.CompletedTask;
    public static SKFont At(float size) => new(SKTypeface.Default, size)
    {
        Edging = SKFontEdging.Antialias, Embolden = true,
    };
    public static SKFont For(string text, float size) => At(size);
}
'''

def once(source: str, before: str, after: str, label: str) -> str:
    if source.count(before) != 1 or after in source:
        raise NeutralAssetRefusal("Missing, ambiguous or already changed pinned anchor: " + label)
    return source.replace(before, after, 1)

def transform(root: Path) -> dict[Path, str]:
    root = root.resolve(strict=True)
    files = [PROJECT, GAME_ART, HOME, ABOUT, MAUI, PIXEL]
    originals = {p: checked_file(root, p).read_text(encoding="utf-8") for p in files}
    for rel in (ICON, SPLASH):
        path = root / rel
        if path.exists() or path.is_symlink():
            raise NeutralAssetRefusal("Neutral vector asset already exists: " + rel)
        if not path.parent.is_dir() or path.parent.is_symlink():
            raise NeutralAssetRefusal("Missing or unsafe vector asset directory: " + rel)

    project = originals[PROJECT]
    project = once(project, '<MauiIcon Include="Resources/AppIcon/pkforge.png" Color="#14121D" />',
                   '<MauiIcon Include="Resources/AppIcon/neutral-preview.svg" Color="#14121D" />', "icon")
    project = once(project, '<MauiSplashScreen Include="Resources/Splash/splash.png" Color="#14121D" BaseSize="180,180" />',
                   '<MauiSplashScreen Include="Resources/Splash/neutral-preview.svg" Color="#14121D" BaseSize="180,180" />', "splash")
    project = once(project, '<MauiFont Include="Resources/Fonts/*.ttf" />',
                   '<MauiFont Remove="Resources/Fonts/**" />', "fonts")
    # Remove all explicitly packaged media, including PKHeX sprites, Bad Apple,
    # events, game art, Showdown, Poképark and proprietary game wallpapers.
    # The attestation must still inspect implicit/embedded assets and assemblies.
    project, removed = re.subn(r'(?m)^\s*<MauiAsset\b[^>]*?/>\s*', '\n', project, flags=re.DOTALL)
    if removed < 15 or "<MauiAsset " in project:
        raise NeutralAssetRefusal(f"Unexpected pinned MauiAsset inventory: {removed}")
    if re.search(r'<(?:MauiImage|MauiAudio|MauiVideo)\b', project):
        raise NeutralAssetRefusal("Unreviewed resource declaration")

    game = originals[GAME_ART]
    for kind in ("Icon", "Hero", "Logo"):
        folder = "gameart" if kind == "Icon" else "game" + kind.lower()
        game = once(game,
            f'public static Task<string?> Get{kind}Async(string gameLabel) => GetAsync("{folder}", gameLabel);',
            f'public static Task<string?> Get{kind}Async(string gameLabel) => Task.FromResult<string?>(null);',
            "game art getter " + kind)
    game = once(game, "private static async Task<string?> GetBySlugAsync(string folder, string slug)\n    {",
        "private static async Task<string?> GetBySlugAsync(string folder, string slug)\n    {\n        if (PreviewRuntimePolicy.Offline) return null;", "game art cache guard")

    home = once(originals[HOME], "private async Task DownloadSpritePackAsync()\n    {",
        'private async Task DownloadSpritePackAsync()\n    {\n        if (PreviewRuntimePolicy.Offline)\n        {\n            await PadMenu.ShowAsync(_hostGrid, "Sprite pack", "Unavailable in offline preview.", "OK");\n            return;\n        }', "sprite pack truth")
    about = once(originals[ABOUT], "_ = BadAppleEgg.PlayAsync(host);",
        "if (!PreviewRuntimePolicy.Offline) _ = BadAppleEgg.PlayAsync(host);", "hidden media trigger")
    maui = originals[MAUI]
    font_block = '''builder.UseMauiApp<App>().UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("MPLUSRounded1c-Regular.ttf", "Rounded");
                fonts.AddFont("MPLUSRounded1c-Bold.ttf", "RoundedBold");
                fonts.AddFont("MPLUSRounded1c-ExtraBold.ttf", "RoundedBlack");
                // The DS-menu voice: NDS12, a recreation of the Nintendo DS system font.
                // Best displayed at font-size 16 or multiples (it is a pixel font).
                fonts.AddFont("NDS12.ttf", "PixelUI");
            });'''
    maui = once(maui, font_block, "builder.UseMauiApp<App>().UseSkiaSharp();", "font registration")
    if "NDS12" not in originals[PIXEL] or "MPLUSRounded1c" not in originals[PIXEL]:
        raise NeutralAssetRefusal("Unexpected original Skia font loader")
    return {
        root / PROJECT: project, root / GAME_ART: game, root / HOME: home,
        root / ABOUT: about, root / MAUI: maui, root / PIXEL: PIXEL_SOURCE,
        root / ICON: ICON_SVG, root / SPLASH: SPLASH_SVG,
    }

def prepare(root: Path) -> None:
    verify_pins(root)
    staged = transform(root)  # Validate every input before changing any source.
    for path, content in staged.items():
        path.write_text(content, encoding="utf-8")

def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    args = parser.parse_args()
    prepare(args.repo)

if __name__ == "__main__":
    main()
