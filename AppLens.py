#!/usr/bin/env python3
"""
AppLens app inventory scanner for macOS and Linux.

Read-only. Writes a categorized plain-text report to the user's Desktop when
available, or to the home directory when no Desktop folder exists.
"""

from __future__ import annotations

import getpass
import os
import platform
import plistlib
import re
import shutil
import socket
import subprocess
import sys
from datetime import datetime
from pathlib import Path


RUNTIME_PATTERNS = (
    r"\bpython\b",
    r"\bnode\.?js\b",
    r"\bjava\b",
    r"\bdotnet\b",
    r"\.net",
    r"\bgolang\b",
    r"\brust\b",
    r"\bruby\b",
    r"\bphp\b",
    r"\bperl\b",
    r"\bpwsh\b",
    r"\bpowershell\b",
)

TOOL_CHECKS = (
    ("Git", "git", ["--version"]),
    ("Python 3", "python3", ["--version"]),
    ("Node.js", "node", ["--version"]),
    ("npm", "npm", ["--version"]),
    ("Docker", "docker", ["--version"]),
    ("Ollama", "ollama", ["--version"]),
    ("PowerShell", "pwsh", ["--version"]),
    ("Homebrew", "brew", ["--version"]),
    ("VS Code", "code", ["--version"]),
    ("Cursor", "cursor", ["--version"]),
    ("uv", "uv", ["--version"]),
    ("pipx", "pipx", ["--version"]),
    ("conda", "conda", ["--version"]),
    ("Go", "go", ["version"]),
    ("Rust", "rustc", ["--version"]),
    ("Java", "java", ["-version"]),
    ("dotnet", "dotnet", ["--version"]),
)


def run_command(command: list[str], timeout: int = 15) -> list[str]:
    if not shutil.which(command[0]):
        return []

    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            errors="replace",
            timeout=timeout,
            check=False,
        )
    except Exception:
        return []

    text = (result.stdout or "") + (result.stderr or "")
    return [line.strip() for line in text.splitlines() if line.strip()]


def output_path(file_name: str) -> Path:
    override = os.environ.get("APPLENS_OUTPUT_DIR")
    base = Path(override).expanduser() if override else Path.home() / "Desktop"
    if not base.exists():
        base = Path.home()
    base.mkdir(parents=True, exist_ok=True)
    return base / file_name


def format_app(name: str, version: str = "", source: str = "") -> str:
    line = name.strip()
    if version:
        line += f" (Version {version.strip()})"
    if source:
        line += f"     [{source.strip()}]"
    return line


def is_runtime(name: str) -> bool:
    return any(re.search(pattern, name, re.IGNORECASE) for pattern in RUNTIME_PATTERNS)


def normalize_key(name: str) -> str:
    return re.sub(r"\s+", " ", name.strip().lower())


def dedupe(items: list[dict[str, str]]) -> list[dict[str, str]]:
    seen: set[str] = set()
    kept: list[dict[str, str]] = []
    for item in items:
        key = normalize_key(item["name"])
        if not key or key in seen:
            continue
        seen.add(key)
        kept.append(item)
    return sorted(kept, key=lambda item: item["name"].lower())


def parse_desktop_file(path: Path) -> dict[str, str] | None:
    fields: dict[str, str] = {}
    try:
        for raw_line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            fields[key.strip()] = value.strip()
    except OSError:
        return None

    if fields.get("Type") and fields["Type"] != "Application":
        return None
    if fields.get("NoDisplay", "").lower() == "true":
        return None
    if fields.get("Hidden", "").lower() == "true":
        return None

    name = fields.get("Name")
    if not name:
        return None

    return {
        "name": name.replace(r"\s", " "),
        "version": "",
        "source": "Desktop entry",
    }


def collect_macos_apps() -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    apps: list[dict[str, str]] = []
    package_apps: list[dict[str, str]] = []

    for root in (Path("/Applications"), Path.home() / "Applications"):
        if not root.exists():
            continue
        for app_path in sorted(root.glob("*.app")):
            info_path = app_path / "Contents" / "Info.plist"
            name = app_path.stem
            version = ""
            try:
                with info_path.open("rb") as handle:
                    info = plistlib.load(handle)
                name = (
                    info.get("CFBundleDisplayName")
                    or info.get("CFBundleName")
                    or app_path.stem
                )
                version = (
                    info.get("CFBundleShortVersionString")
                    or info.get("CFBundleVersion")
                    or ""
                )
            except Exception:
                pass
            apps.append({"name": str(name), "version": str(version), "source": "Application"})

    for line in run_command(["brew", "list", "--cask", "--versions"]):
        parts = line.split()
        if not parts:
            continue
        name = parts[0].replace("-", " ").title()
        version = " ".join(parts[1:])
        package_apps.append({"name": name, "version": version, "source": "Homebrew cask"})

    return apps, package_apps


def collect_linux_apps() -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    apps: list[dict[str, str]] = []
    package_apps: list[dict[str, str]] = []
    desktop_roots = (
        Path("/usr/share/applications"),
        Path("/usr/local/share/applications"),
        Path.home() / ".local/share/applications",
    )

    for root in desktop_roots:
        if not root.exists():
            continue
        for desktop_file in root.glob("*.desktop"):
            parsed = parse_desktop_file(desktop_file)
            if parsed:
                apps.append(parsed)

    for line in run_command(["flatpak", "list", "--app", "--columns=name,application,version"]):
        parts = line.split("\t")
        if parts and parts[0]:
            package_apps.append(
                {
                    "name": parts[0],
                    "version": parts[2] if len(parts) > 2 else "",
                    "source": "Flatpak",
                }
            )

    snap_lines = run_command(["snap", "list"])
    for line in snap_lines[1:]:
        parts = line.split()
        if len(parts) >= 2:
            package_apps.append({"name": parts[0], "version": parts[1], "source": "Snap"})

    for line in run_command(["brew", "list", "--cask", "--versions"]):
        parts = line.split()
        if parts:
            package_apps.append(
                {
                    "name": parts[0].replace("-", " ").title(),
                    "version": " ".join(parts[1:]),
                    "source": "Homebrew cask",
                }
            )

    return apps, package_apps


def collect_developer_tools() -> list[dict[str, str]]:
    tools: list[dict[str, str]] = []
    for label, command, args in TOOL_CHECKS:
        lines = run_command([command, *args], timeout=8)
        if not lines:
            continue
        version = lines[0]
        if len(version) > 120:
            version = version[:117] + "..."
        tools.append({"name": label, "version": version, "source": "Detected"})
    return dedupe(tools)


def build_report() -> str:
    system = platform.system()
    if system not in {"Darwin", "Linux"}:
        raise SystemExit("AppLens.py is for macOS/Linux. Use AppLens.ps1 on Windows.")

    if system == "Darwin":
        desktop_apps, package_apps = collect_macos_apps()
        package_title = "--- Homebrew Casks ---"
    else:
        desktop_apps, package_apps = collect_linux_apps()
        package_title = "--- Flatpak/Snap/Cask Apps ---"

    developer_tools = collect_developer_tools()

    runtimes = [item for item in desktop_apps + package_apps + developer_tools if is_runtime(item["name"])]
    desktop_apps = [item for item in desktop_apps if not is_runtime(item["name"])]
    package_apps = [item for item in package_apps if not is_runtime(item["name"])]

    lines: list[str] = []
    lines.append("=== AppLens Scan Results ===")
    lines.append(f"Computer: {socket.gethostname()}")
    lines.append(f"User: {getpass.getuser()}")
    lines.append(f"OS: {platform.platform()}")
    lines.append(f"Scan Date: {datetime.now().strftime('%Y-%m-%d')}")
    lines.append("")
    lines.append("--- Desktop Applications ---")
    desktop_apps = dedupe(desktop_apps)
    if desktop_apps:
        lines.extend(format_app(**item) for item in desktop_apps)
    else:
        lines.append("(none detected)")

    lines.append("")
    lines.append(package_title)
    package_apps = dedupe(package_apps)
    if package_apps:
        lines.extend(format_app(**item) for item in package_apps)
    else:
        lines.append("(none detected)")

    lines.append("")
    lines.append("--- Developer/CLI Tools (detected) ---")
    if developer_tools:
        lines.extend(format_app(**item) for item in developer_tools)
    else:
        lines.append("(none detected)")

    lines.append("")
    lines.append("--- Runtimes & Frameworks (for reference) ---")
    runtimes = dedupe(runtimes)
    if runtimes:
        lines.extend(format_app(**item) for item in runtimes)
    else:
        lines.append("(none detected)")

    return "\n".join(lines) + "\n"


def main() -> int:
    report = build_report()
    path = output_path(f"AppLens_Results_{socket.gethostname()}.txt")
    path.write_text(report, encoding="utf-8")
    print(report, end="")
    print("")
    print("============================================")
    print("  Scan complete!")
    print("  Results saved to:")
    print(f"  {path}")
    print("============================================")
    return 0


if __name__ == "__main__":
    sys.exit(main())
