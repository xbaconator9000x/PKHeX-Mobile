"""Synthetic negative/positive tests for the pinned rights-neutral adaptation."""
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch
import zipfile

from scripts.pkforge_neutral_assets import (
    ABOUT, BOX, GAME_ART, HOME, ICON, MAUI, PIXEL, PROJECT, SPLASH,
    NeutralAssetRefusal, transform, verify_apks,
)

class NeutralAssetsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.sources = {
            PROJECT: '''<Project><ItemGroup>
<MauiIcon Include="Resources/AppIcon/pkforge.png" Color="#14121D" />
<MauiSplashScreen Include="Resources/Splash/splash.png" Color="#14121D" BaseSize="180,180" />
<MauiFont Include="Resources/Fonts/*.ttf" />
''' + ''.join(f'<MauiAsset Include="Resources/UI/{n}.png" LogicalName="ui/{n}.png" />\n' for n in range(18)) + '''</ItemGroup></Project>''',
            GAME_ART: '''public static Task<string?> GetIconAsync(string gameLabel) => GetAsync("gameart", gameLabel);
public static Task<string?> GetHeroAsync(string gameLabel) => GetAsync("gamehero", gameLabel);
public static Task<string?> GetLogoAsync(string gameLabel) => GetAsync("gamelogo", gameLabel);
private static async Task<string?> GetBySlugAsync(string folder, string slug)
    {
var cache = FileSystem.CacheDirectory;''',
            HOME: '''private async Task DownloadSpritePackAsync()
    {
if (await Task.Run(downloader.CountMissing) == 0)''',
            ABOUT: '''_ = BadAppleEgg.PlayAsync(host);''',
            MAUI: '''builder.UseMauiApp<App>().UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("MPLUSRounded1c-Regular.ttf", "Rounded");
                fonts.AddFont("MPLUSRounded1c-Bold.ttf", "RoundedBold");
                fonts.AddFont("MPLUSRounded1c-ExtraBold.ttf", "RoundedBlack");
                // The DS-menu voice: NDS12, a recreation of the Nintendo DS system font.
                // Best displayed at font-size 16 or multiples (it is a pixel font).
                fonts.AddFont("NDS12.ttf", "PixelUI");
            });''',
            PIXEL: "NDS12.ttf and MPLUSRounded1c-Regular.ttf",
            BOX: """internal static SKTypeface PixelTypeface()\n    {\nvar cache = FileSystem.CacheDirectory;\nprivate static string? RibbonIconPath(string id)\n    {\nvar ribbonCache = FileSystem.CacheDirectory;\nprivate static string? BallIconPath(int ball)\n    {\nvar ballCache = FileSystem.CacheDirectory;""",
        }
        for path, content in self.sources.items():
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content)
        for rel in (ICON, SPLASH):
            (self.root / rel).parent.mkdir(parents=True, exist_ok=True)

    def test_stages_complete_cutover_without_touching_originals(self):
        staged = transform(self.root)
        self.assertEqual(len(staged), 9)
        self.assertEqual((self.root / PROJECT).read_text(), self.sources[PROJECT])
        project = staged[self.root / PROJECT]
        self.assertNotIn("<MauiAsset", project)
        self.assertNotIn("MauiFont Include=", project)
        self.assertIn("MauiFont Remove=", project)
        self.assertIn("neutral-preview.svg", project)
        self.assertNotIn("pkforge.png", project)
        self.assertNotIn("NDS12", staged[self.root / PIXEL])
        self.assertNotIn("OpenAppPackageFileAsync", staged[self.root / PIXEL])

    def test_cached_art_is_refused_before_lookup(self):
        art = transform(self.root)[self.root / GAME_ART]
        for name in ("Icon", "Hero", "Logo"):
            self.assertIn(f"Get{name}Async(string gameLabel) => Task.FromResult<string?>(null)", art)
        self.assertLess(art.index("if (PreviewRuntimePolicy.Offline) return null;"), art.index("FileSystem.CacheDirectory"))

    def test_box_font_and_art_cache_guards_precede_old_cache_reads(self):
        box = transform(self.root)[self.root / BOX]
        self.assertIn("if (PreviewRuntimePolicy.Offline) return SKTypeface.Default;", box)
        self.assertEqual(box.count("if (PreviewRuntimePolicy.Offline) return null;"), 2)
        self.assertLess(box.index("return SKTypeface.Default;"), box.index("var cache"))
        self.assertLess(box.index("return null;"), box.index("var ribbonCache"))

    def test_sprite_ui_truth_and_hidden_media_refusal(self):
        staged = transform(self.root)
        self.assertIn("Unavailable in offline preview", staged[self.root / HOME])
        self.assertIn("if (!PreviewRuntimePolicy.Offline) _ = BadAppleEgg.PlayAsync(host);", staged[self.root / ABOUT])

    def test_no_silent_unknown_media_inventory(self):
        path = self.root / PROJECT
        path.write_text(path.read_text() + '\n<MauiImage Include="Resources/UI/unknown.png" />')
        with self.assertRaises(NeutralAssetRefusal): transform(self.root)

    def test_refuse_missing_pinned_font_registration(self):
        path = self.root / MAUI
        path.write_text("builder.UseMauiApp<App>().UseSkiaSharp();")
        with self.assertRaises(NeutralAssetRefusal): transform(self.root)

    def test_refuse_reused_neutral_icon(self):
        (self.root / ICON).write_text("stale")
        with self.assertRaises(NeutralAssetRefusal): transform(self.root)

    def test_refuse_ambiguous_art_getter(self):
        path = self.root / GAME_ART
        path.write_text(path.read_text() + '\n' + self.sources[GAME_ART].splitlines()[0])
        with self.assertRaises(NeutralAssetRefusal): transform(self.root)

    def test_refuse_symlink_source(self):
        path = self.root / PIXEL
        path.unlink()
        path.symlink_to(self.root / PROJECT)
        with self.assertRaises(Exception): transform(self.root)

    def test_compiled_apk_refuses_cached_game_art_and_noncommercial_font(self):
        folder = self.root / "src/PKForge.App/bin/Debug/net10.0-android"
        folder.mkdir(parents=True)
        apk = folder / "diagnostic.apk"
        for path in ("assets/gameart/gameart-v10-firered.png", "assets/NDS12.ttf",
                     "assets/badapple/audio.m4a"):
            with zipfile.ZipFile(apk, "w") as archive:
                archive.writestr("AndroidManifest.xml", "synthetic")
                archive.writestr(path, "synthetic")
            with patch("scripts.pkforge_neutral_assets.verify_pins"):
                with self.assertRaises(NeutralAssetRefusal): verify_apks(self.root)

    def test_compiled_apk_allows_only_neutral_synthetic_media(self):
        folder = self.root / "src/PKForge.App/bin/Debug/net10.0-android"
        folder.mkdir(parents=True)
        apk = folder / "diagnostic.apk"
        with zipfile.ZipFile(apk, "w") as archive:
            archive.writestr("AndroidManifest.xml", "synthetic")
            archive.writestr("res/drawable/neutral_preview.png", "synthetic")
        with patch("scripts.pkforge_neutral_assets.verify_pins"):
            self.assertEqual(verify_apks(self.root), [str(apk)])

    def test_vector_assets_are_original_simple_geometry(self):
        staged = transform(self.root)
        for rel in (ICON, SPLASH):
            content = staged[self.root / rel]
            self.assertIn("<svg", content)
            self.assertNotIn("<image", content)

if __name__ == "__main__":
    unittest.main()
