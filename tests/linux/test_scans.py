import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import applens_linux as linux


class InventoryTests(unittest.TestCase):
    def test_baseline_categories_and_new_report_envelope(self):
        fixture = json.loads((Path(__file__).parent / "fixtures/baseline.json").read_text())
        self.assertEqual(fixture["revision"], "970a0b9")
        with patch.object(linux, "diagnostics", side_effect=AssertionError("quick invoked diagnostics")):
            report = linux.scan([], lambda name: "python" in name.lower())
        self.assertEqual(report["SchemaVersion"], "3.0")
        self.assertEqual(report["Mode"], "Inventory")
        self.assertEqual(report["Diagnostics"], [])
        self.assertEqual(report["Machine"]["ComputerName"], "[computer]")
        for field in ("DesktopApplications", "Packages", "CliTools", "RuntimesAndFrameworks"):
            self.assertIn(field, report["Inventory"])

    def test_package_sources_and_duplicates(self):
        examples = [
            ("dpkg", "installed\tlibsample:amd64\t1.0\nconfig-files\told\t0\n", "libsample:amd64"),
            ("rpm", "editor.x86_64\t1.0-2\n", "editor.x86_64"),
            ("pacman", "package 1.0-1\n", "package"),
            ("apk", "libfoo-bar-1.0-r1\n", "libfoo-bar"),
            ("xbps", "ii editor-1.0_2 A description\n", "editor"),
            ("Snap", "Name Version Rev Tracking Publisher Notes\nexample 1.0 4 stable user -\n", "example"),
            ("Flatpak", "org.example.App\t1.0\n", "org.example.App"),
            ("Homebrew formula", "sample 1.0 1.1\n", "sample"),
            ("Homebrew cask", "sample 1.0\n", "sample"),
        ]
        for source, raw, expected in examples:
            with self.subTest(source=source):
                rows = linux.package_rows(source, raw * 2)
                self.assertEqual([row["Name"] for row in rows], [expected])

    def test_xdg_hidden_override_and_action_section(self):
        with tempfile.TemporaryDirectory() as directory:
            user, system = [Path(directory) / scope for scope in ("user", "system")]
            user.mkdir()
            system.mkdir()
            (user / "a.desktop").write_text("[Desktop Entry]\nType=Application\nName=Hidden\nHidden=true\n")
            (system / "a.desktop").write_text("[Desktop Entry]\nType=Application\nName=Overridden\n")
            (system / "b.desktop").write_text(
                "[Desktop Entry]\nType=Application\nName=Visible\n[Desktop Action New]\nName=New\n")
            self.assertEqual([row["Name"] for row in linux.launcher_entries([user, system])], ["Visible"])
            self.assertEqual([row["State"] for row in linux.launcher_entries([user, system], startup=True)],
                             ["Disabled", "Configured"])

    def test_disk_threshold(self):
        self.assertTrue(linux.low_disk(0, 100))
        self.assertTrue(linux.low_disk(9, 100))
        self.assertFalse(linux.low_disk(10, 100))
        self.assertIsNone(linux.low_disk(0, 0))


class DiagnosticsTests(unittest.TestCase):
    def test_listeners_exclude_established_tcp_and_connected_udp(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "tcp").write_text(
                "sl local_address rem_address st\n"
                "0: 0100007F:0016 00000000:0000 0A\n"
                "1: 0100007F:A001 0100007F:0016 01\n")
            (root / "udp").write_text(
                "sl local_address rem_address st\n"
                "0: 00000000:0035 00000000:0000 07\n"
                "1: 0100007F:A002 0100007F:0035 01\n")
            with patch.object(linux, "Path", side_effect=lambda path: root if path == "/proc/net" else Path(path)):
                rows = linux.listeners()
            self.assertEqual({row["Transport"]: row["Count"] for row in rows}, {"TCP": 1, "UDP": 1})

    def test_cron_keeps_system_jobs_when_user_crontab_is_denied(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "crontab").write_text("* * * * * root fixture\n")
            binary_dir = root / "bin"
            binary_dir.mkdir()
            binary = binary_dir / "crontab"
            with patch.dict(os.environ, PATH=str(binary_dir)), \
                    patch.object(linux, "Path", side_effect=lambda path: path if isinstance(path, Path)
                                 else root / Path(path).relative_to("/etc")):
                for message, user_state, state in (
                        ("no crontab for fixture", "Empty", "Succeeded"),
                        ("permission denied", "PermissionLimited", "Partial")):
                    with self.subTest(message=message):
                        binary.write_text(f"#!/bin/sh\nprintf '%s\\n' '{message}' >&2\nexit 1\n")
                        binary.chmod(0o700)
                        result = linux.capture("Scheduled jobs", "cron", linux.cron)
                        self.assertEqual(result["State"], state)
                        self.assertEqual(result["Rows"][0]["JobCount"], 1)
                        self.assertEqual(result["Rows"][0]["UserCrontabState"], user_state)

    def test_probe_statuses_and_partial_rows(self):
        probes = [
            ("Empty", lambda: []),
            ("Unavailable", lambda: linux.command("applens-missing-tool-fixture")),
            ("PermissionLimited", lambda: linux.command(
                sys.executable, "-c", "import sys; sys.exit('permission denied')")),
            ("TimedOut", lambda: linux.command(sys.executable, "-c", "import time; time.sleep(20)", timeout=.1)),
            ("Partial", lambda: [{"Count": 1}, {"State": "Failed"}]),
        ]
        for state, action in probes:
            with self.subTest(state=state):
                self.assertEqual(linux.capture("Probe", "Fixture", action)["State"], state)
        with self.assertRaises(KeyboardInterrupt):
            linux.capture("Probe", "Fixture", lambda: self.raise_cancelled())

    @staticmethod
    def raise_cancelled():
        raise KeyboardInterrupt()

    def test_disk_health_and_encryption_fixture(self):
        devices = [{"name": "nvme0n1", "type": "disk", "fstype": "crypto_LUKS"},
                   {"name": "sdz", "type": "disk", "fstype": None}]
        smart = {"smart_status": {"passed": False},
                 "nvme_smart_health_information_log": {"media_errors": 2}}
        with patch.object(linux, "block_devices", return_value=devices), \
                patch.object(linux.shutil, "which", return_value="/fixture/smartctl"), \
                patch.object(linux, "command", side_effect=[json.dumps(smart),
                                                            linux.ProbeError("PermissionLimited")]):
            result = linux.capture("Disk health", "Fixture", linux.smart_health)
            self.assertEqual(result["State"], "Partial")
            self.assertFalse(result["Rows"][0]["Passed"])
            self.assertEqual(result["Rows"][0]["MediaErrors"], 2)
            self.assertEqual(result["Rows"][1]["State"], "PermissionLimited")
            self.assertEqual(linux.encryption()[0]["DetectedLuksContainers"], 1)

    def test_battery_capacity_cycles_and_inapplicability(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.assertEqual(linux.capture("Battery", "Fixture", lambda: linux.battery(root))["State"],
                             "Inapplicable")
            battery = root / "BAT0"
            battery.mkdir()
            for name, value in {"type": "Battery", "energy_full": "34000000", "energy_full_design": "40000000",
                                "cycle_count": "145"}.items():
                (battery / name).write_text(value)
            result = linux.battery(root)[0]
            self.assertEqual(result["HealthPercent"], 85)
            self.assertEqual(result["Cycles"], 145)
            (battery / "cycle_count").unlink()
            self.assertEqual(linux.battery(root)[0]["CycleCountState"], "Unavailable")

    def test_backup_outcomes_preserve_partial_observations(self):
        with patch.object(linux, "systemd", side_effect=[
                "restic-backup.service enabled\nborg-backup.service enabled",
                "ExecMainExitTimestamp=Mon 2026-11-02 09:00:00 UTC\nResult=success\nExecMainStatus=0",
                linux.ProbeError("PermissionLimited")]):
            result = linux.capture("Backup outcomes", "Fixture", lambda: linux.backup_outcomes("system"))
        self.assertEqual(result["State"], "Partial")
        self.assertEqual(result["Rows"][0]["Result"], "success")
        self.assertEqual(result["Rows"][1]["State"], "PermissionLimited")

    def test_llama_enumeration_does_not_claim_inference_capacity(self):
        with patch.object(linux.shutil, "which", return_value="/fixture/llama-cli"), \
                patch.object(linux, "command", return_value="Available devices:\n  CUDA0: Fixture GPU\n"):
            self.assertEqual(linux.llama_devices(), [{"Device": "CUDA0", "Description": "Fixture GPU",
                                                     "Backend": "CUDA", "VerifiedUsableInferenceBytes": None}])

    def test_report_redacts_identifiers_without_rewriting_status(self):
        value = {"Machine": {"Path": "/home/fixture/file"}, "Inventory": {},
                 "Diagnostics": [{"Rows": [{"Name": "fixture@host", "State": "Succeeded"}]}]}
        report = linux.redact(value, user="fixture", host="host", home="/home/fixture")
        self.assertEqual(report["Machine"]["Path"], "%USERPROFILE%/file")
        self.assertEqual(report["Diagnostics"][0]["Rows"][0],
                         {"Name": "[user]@[computer]", "State": "Succeeded"})

    def test_cli_smoke_both_modes(self):
        for args in ([], ["--diagnostics"]):
            result = subprocess.run(["sh", "Run-AppLens.sh", *args], text=True,
                                    capture_output=True, check=True, timeout=90)
            report = json.loads(result.stdout)
            self.assertEqual(report["Mode"], "Diagnostics" if args else "Inventory")
            if args:
                by_name = {probe["Name"]: probe for probe in report["Diagnostics"]}
                self.assertEqual(by_name["WSL"]["State"], "Inapplicable")
                self.assertEqual(by_name["DNS"]["State"], "Succeeded")
                self.assertTrue(by_name["Processes"]["Rows"])
            else:
                self.assertEqual(report["Diagnostics"], [])
