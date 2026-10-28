"""Synthetic rewrite-contract tests; not PKHeX functional certification."""
import sys
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from scripts.pkforge_official_core_probe import ProbeRefusal, rewrite, VERSION

SOURCE = """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
<ProjectReference Include="../PKForge.Domain/PKForge.Domain.csproj" />
<ProjectReference Include="../../external/PKHeX/PKHeX.Core/PKHeX.Core.csproj" />
</ItemGroup></Project>"""


class CoreProbeTests(unittest.TestCase):
    def test_exact_package_and_other_reference_preserved(self):
        root = ET.fromstring(rewrite(SOURCE))
        package = root.find(".//PackageReference")
        self.assertEqual(("PKHeX.Core", VERSION),
                         (package.get("Include"), package.get("Version")))
        refs = root.findall(".//ProjectReference")
        self.assertEqual(1, len(refs))
        self.assertIn("PKForge.Domain", refs[0].get("Include"))

    def test_backslash_path(self):
        self.assertIn("PKHeX.Core", rewrite(SOURCE.replace("/", "\\")))

    def test_missing_and_duplicate_rejected(self):
        for source in (SOURCE.replace("external/PKHeX", "external/Other"),
                       SOURCE.replace("</ItemGroup>", '<ProjectReference Include="../../external/PKHeX/PKHeX.Core/PKHeX.Core.csproj" /></ItemGroup>')):
            with self.assertRaises(ProbeRefusal):
                rewrite(source)

    def test_existing_package_and_metadata_rejected(self):
        for source in (SOURCE.replace("</ItemGroup>", '<PackageReference Include="PKHeX.Core" Version="26.7.7" /></ItemGroup>'),
                       SOURCE.replace('PKHeX.Core.csproj" />', 'PKHeX.Core.csproj" PrivateAssets="all" />')):
            with self.assertRaises(ProbeRefusal):
                rewrite(source)

    def test_malformed_or_second_rewrite_refused(self):
        for source in ("<Project>", "<Other />", rewrite(SOURCE)):
            with self.assertRaises(ProbeRefusal):
                rewrite(source)


if __name__ == "__main__":
    unittest.main()
