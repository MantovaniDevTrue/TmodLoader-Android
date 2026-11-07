#!/usr/bin/env python3
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOL = Path(__file__).with_name("find_android_control.py")

class AndroidControlLookupTests(unittest.TestCase):
    def find(self, xml, name):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "window.xml"
            path.write_text(xml, encoding="utf-8")
            return subprocess.run(
                [sys.executable, str(TOOL), str(path), name],
                capture_output=True, text=True, check=False
            )

    def test_spinner_takes_precedence_over_label(self):
        xml = """<hierarchy>
            <node text="Renderizador" class="android.widget.TextView" bounds="[10,10][110,40]" />
            <node content-desc="Renderizador" class="android.widget.Spinner"
                clickable="true" bounds="[120,10][420,70]" />
            </hierarchy>"""
        found = self.find(xml, "Renderizador")
        self.assertEqual(found.returncode, 0, found.stderr)
        self.assertEqual(found.stdout.strip(), "270 40")

    def test_dropdown_choice(self):
        xml = """<hierarchy>
            <node text="Vulkan · experimental" class="android.widget.CheckedTextView"
                clickable="true" bounds="[120,100][420,160]" />
            </hierarchy>"""
        found = self.find(xml, "Vulkan · experimental")
        self.assertEqual(found.returncode, 0, found.stderr)
        self.assertEqual(found.stdout.strip(), "270 130")

    def test_hidden_control_rejected(self):
        xml = """<hierarchy>
            <node text="Vulkan · experimental" visible-to-user="false"
                bounds="[120,100][420,160]" />
            </hierarchy>"""
        self.assertNotEqual(self.find(xml, "Vulkan · experimental").returncode, 0)

    def test_missing_control_rejected(self):
        self.assertNotEqual(self.find("<hierarchy />", "Renderizador").returncode, 0)

    def test_invalid_xml_rejected(self):
        self.assertNotEqual(self.find("<hierarchy", "Renderizador").returncode, 0)

if __name__ == "__main__":
    unittest.main()
