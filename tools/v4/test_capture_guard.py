"""Capture safety checks that never launch Unity or a player."""
from pathlib import Path
import json
import shutil
import subprocess
import tempfile
import unittest


class CaptureGuardTests(unittest.TestCase):
    def invoke(self, states, stale=False, old_build=False):
        repo = Path(__file__).resolve().parents[2]
        with tempfile.TemporaryDirectory(prefix="g101-capture-guard-") as temp:
            output = Path(temp) / "output"
            if stale:
                output.mkdir()
                (output / "previous.png").write_bytes(b"previous capture")
            build = Path(temp) / "missing-build"
            if old_build:
                build.mkdir()
                (build / "build-report.json").write_text(json.dumps({"result": "Succeeded", "configuration": "Development"}), encoding="utf-8")
            result = subprocess.run(
                [shutil.which("pwsh") or "pwsh", "-NoLogo", "-NoProfile", "-File",
                 str(repo / "scripts/Capture-G101States.ps1"), "-States", states,
                 "-BuildDirectory", str(build),
                 "-OutputDirectory", str(output)],
                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=20,
            )
            self.assertNotEqual(result.returncode, 0, result.stdout)
            if stale:
                self.assertEqual((output / "previous.png").read_bytes(), b"previous capture")
            else:
                self.assertFalse(output.exists(), "invalid requests must fail before creating output")
            return result.stdout + result.stderr

    def test_all_is_rejected_before_build_or_player_start(self):
        self.assertIn("at most 4", self.invoke("all"))

    def test_large_batch_is_rejected_before_build_or_player_start(self):
        self.assertIn("at most 4", self.invoke("01,02,03,04,05"))

    def test_previous_capture_is_never_mixed_into_a_new_run(self):
        self.assertIn("must be empty", self.invoke("01", stale=True))

    def test_old_build_without_window_guard_is_rejected_before_player_start(self):
        self.assertIn("capture window guard", self.invoke("01", old_build=True))


if __name__ == "__main__":
    unittest.main()
