#!/usr/bin/env python3
"""
AppLens-Tune read-only workstation audit for macOS and Linux.

This is audit mode only: it observes process, startup, service, storage, and
repo-placement signals. It does not change the machine.
"""

from __future__ import annotations

import getpass
import os
import platform
import shutil
import socket
import subprocess
import sys
from datetime import datetime
from pathlib import Path


SKIP_DIRS = {
    ".cache",
    ".git",
    ".hg",
    ".svn",
    "Library",
    "node_modules",
    "venv",
    ".venv",
    "__pycache__",
}


def run_command(command: list[str], timeout: int = 15) -> list[str]:
    if not shutil.which(command[0]):
        return [f"{command[0]} not found"]

    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            errors="replace",
            timeout=timeout,
            check=False,
        )
    except subprocess.TimeoutExpired:
        return [f"{command[0]} timed out"]
    except Exception as exc:
        return [f"Unavailable: {exc}"]

    text = (result.stdout or "") + (result.stderr or "")
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return lines or ["(no output)"]


def output_path(file_name: str) -> Path:
    override = os.environ.get("APPLENS_OUTPUT_DIR")
    base = Path(override).expanduser() if override else Path.home() / "Desktop"
    if not base.exists():
        base = Path.home()
    base.mkdir(parents=True, exist_ok=True)
    return base / file_name


def format_size(bytes_value: int | None) -> str:
    if bytes_value is None:
        return "(missing)"
    value = float(bytes_value)
    for suffix in ("B", "KB", "MB", "GB", "TB"):
        if value < 1024 or suffix == "TB":
            if suffix == "B":
                return f"{int(value)} B"
            return f"{value:.2f} {suffix}"
        value /= 1024
    return f"{value:.2f} TB"


def directory_size(path: Path) -> int | None:
    if not path.exists():
        return None

    if shutil.which("du"):
        lines = run_command(["du", "-sk", str(path)], timeout=20)
        if lines and not lines[0].startswith(("du not found", "du timed out", "Unavailable")):
            first = lines[0].split()[0]
            if first.isdigit():
                return int(first) * 1024

    total = 0
    try:
        for root, dirs, files in os.walk(path):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for file_name in files:
                try:
                    total += (Path(root) / file_name).stat().st_size
                except OSError:
                    continue
    except OSError:
        return None
    return total


def table(rows: list[dict[str, object]], columns: list[str]) -> list[str]:
    if not rows:
        return ["(none)"]

    widths = {
        column: max(len(column), *(len(str(row.get(column, ""))) for row in rows))
        for column in columns
    }
    lines = ["  ".join(column.ljust(widths[column]) for column in columns)]
    lines.append("  ".join("-" * widths[column] for column in columns))
    for row in rows:
        lines.append("  ".join(str(row.get(column, "")).ljust(widths[column]) for column in columns))
    return lines


def section(title: str, lines: list[str]) -> list[str]:
    return ["", title, *(lines if lines else ["(none)"])]


def total_ram_bytes() -> int | None:
    system = platform.system()
    if system == "Darwin":
        lines = run_command(["sysctl", "-n", "hw.memsize"])
        return int(lines[0]) if lines and lines[0].isdigit() else None

    meminfo = Path("/proc/meminfo")
    if meminfo.exists():
        for line in meminfo.read_text(errors="replace").splitlines():
            if line.startswith("MemTotal:"):
                parts = line.split()
                if len(parts) >= 2 and parts[1].isdigit():
                    return int(parts[1]) * 1024
    return None


def top_processes() -> list[dict[str, object]]:
    lines = run_command(["ps", "-axo", "pid=,comm=,rss=,pcpu="], timeout=10)
    rows: list[dict[str, object]] = []
    for line in lines:
        parts = line.split(None, 3)
        if len(parts) != 4 or not parts[0].isdigit():
            continue
        pid, command, rss_kb, cpu = parts
        try:
            rss_mb = int(rss_kb) / 1024
        except ValueError:
            continue
        rows.append(
            {
                "Name": Path(command).name[:36],
                "PID": pid,
                "RSS_MB": f"{rss_mb:.1f}",
                "CPU_%": cpu,
            }
        )
    rows.sort(key=lambda row: float(row["RSS_MB"]), reverse=True)
    return rows[:15]


def mac_startup_entries() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    roots = (
        (Path.home() / "Library/LaunchAgents", "User LaunchAgent"),
        (Path("/Library/LaunchAgents"), "LaunchAgent"),
        (Path("/Library/LaunchDaemons"), "LaunchDaemon"),
    )
    for root, source in roots:
        if not root.exists():
            continue
        for plist in sorted(root.glob("*.plist"))[:100]:
            rows.append({"Name": plist.stem, "State": "Present", "Source": source})
    login_items = run_command(
        ["osascript", "-e", 'tell application "System Events" to get the name of every login item'],
        timeout=6,
    )
    if login_items and not login_items[0].startswith(("osascript not found", "Unavailable")):
        for item in ", ".join(login_items).split(","):
            name = item.strip()
            if name:
                rows.append({"Name": name, "State": "Present", "Source": "Login item"})
    return rows


def linux_startup_entries() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for root, source in (
        (Path.home() / ".config/autostart", "User autostart"),
        (Path("/etc/xdg/autostart"), "System autostart"),
    ):
        if not root.exists():
            continue
        for desktop in sorted(root.glob("*.desktop"))[:100]:
            rows.append({"Name": desktop.stem, "State": "Present", "Source": source})

    for scope, command in (
        ("User systemd", ["systemctl", "--user", "list-unit-files", "--type=service", "--state=enabled", "--no-pager", "--no-legend"]),
        ("System systemd", ["systemctl", "list-unit-files", "--type=service", "--state=enabled", "--no-pager", "--no-legend"]),
    ):
        for line in run_command(command, timeout=8)[:80]:
            parts = line.split()
            if len(parts) >= 2 and parts[0].endswith(".service"):
                rows.append({"Name": parts[0], "State": parts[1], "Source": scope})
    return rows


def startup_entries() -> list[dict[str, str]]:
    return mac_startup_entries() if platform.system() == "Darwin" else linux_startup_entries()


def key_services() -> list[dict[str, str]]:
    names = ("docker", "colima", "podman", "ollama", "onedrive", "dropbox", "tailscale", "cloudflared")
    rows: list[dict[str, str]] = []
    for name in names:
        installed = "Yes" if shutil.which(name) else "No"
        process_lines = run_command(["pgrep", "-fl", name], timeout=5)
        running = "Yes" if process_lines and not process_lines[0].startswith(("pgrep not found", "Unavailable", "(no output)")) else "No"
        rows.append({"Name": name, "Installed": installed, "Running": running})
    return rows


def storage_hotspots() -> list[dict[str, object]]:
    home = Path.home()
    candidates = [
        (".ollama", home / ".ollama"),
        (".codex", home / ".codex"),
        (".claude", home / ".claude"),
        (".cache", home / ".cache"),
        (".docker", home / ".docker"),
        ("npm cache", home / ".npm"),
        ("pip cache", home / ".cache/pip"),
    ]

    if platform.system() == "Darwin":
        candidates.extend(
            [
                ("Library/Caches", home / "Library/Caches"),
                ("Docker Desktop", home / "Library/Containers/com.docker.docker"),
                ("Application Support/Ollama", home / "Library/Application Support/Ollama"),
            ]
        )
    else:
        candidates.extend(
            [
                ("/tmp", Path("/tmp")),
                ("containers", home / ".local/share/containers"),
                ("/var/lib/docker", Path("/var/lib/docker")),
            ]
        )

    rows: list[dict[str, object]] = []
    for label, path in candidates:
        if not path.exists():
            continue
        size = directory_size(path)
        rows.append({"Location": label, "Bytes": size or 0, "Size": format_size(size), "Path": str(path)})
    rows.sort(key=lambda row: int(row["Bytes"]), reverse=True)
    return rows


def repo_roots() -> list[Path]:
    home = Path.home()
    roots = [
        home / "Documents",
        home / "Desktop",
        home / "Projects",
        home / "Developer",
        home / "source",
        home / "src",
        home / "dev",
        home / "OneDrive",
        home / "Dropbox",
        home / "Google Drive",
        home / "Library/CloudStorage",
        Path("/workspaces"),
    ]
    return [root for root in roots if root.exists()]


def count_repos(root: Path, max_depth: int = 4, max_count: int = 20) -> tuple[int, str, bool]:
    count = 0
    sample = ""
    truncated = False
    root_depth = len(root.parts)

    for current, dirs, _files in os.walk(root):
        current_path = Path(current)
        depth = len(current_path.parts) - root_depth
        if depth > max_depth:
            dirs[:] = []
            continue
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        if ".git" in dirs:
            count += 1
            if not sample:
                sample = str(current_path)
            dirs.remove(".git")
            if count >= max_count:
                truncated = True
                break
    return count, sample, truncated


def repo_placement() -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for root in repo_roots():
        count, sample, truncated = count_repos(root)
        rows.append(
            {
                "Root": str(root),
                "RepoCount": f"{count}+" if truncated else count,
                "Sample": sample,
            }
        )
    return rows


def build_findings(
    startup_rows: list[dict[str, str]],
    service_rows: list[dict[str, str]],
    storage_rows: list[dict[str, object]],
    repo_rows: list[dict[str, object]],
) -> tuple[list[str], list[str], list[str]]:
    stable = ["- Audit mode only; no changes were made."]
    review: list[str] = []
    optional: list[str] = []

    running = {row["Name"] for row in service_rows if row.get("Running") == "Yes"}
    if {"docker", "colima", "podman"} & running:
        review.append("- Container tooling is running in the background.")
    if "onedrive" in running or "dropbox" in running:
        review.append("- Cloud sync tooling is active during the snapshot.")

    startup_names = [row["Name"].lower() for row in startup_rows]
    if any("docker" in name or "colima" in name for name in startup_names):
        review.append("- Docker or container tooling appears in startup entries.")

    cloud_terms = ("onedrive", "icloud", "dropbox", "google drive", "cloudstorage")
    for row in repo_rows:
        root = str(row["Root"]).lower()
        count = str(row["RepoCount"])
        if count not in {"0", ""} and any(term in root for term in cloud_terms):
            review.append("- Git repos were detected under a cloud-synced root.")
            break

    for row in storage_rows:
        label = str(row["Location"])
        bytes_value = int(row["Bytes"])
        if label in {".ollama", "Application Support/Ollama"} and bytes_value >= 15 * 1024**3:
            optional.append(f"- Ollama model storage is {format_size(bytes_value)}.")
        if label in {".cache", "Library/Caches", "/tmp"} and bytes_value >= 2 * 1024**3:
            optional.append(f"- {label} is {format_size(bytes_value)}.")

    free = shutil.disk_usage(Path.home().anchor or "/").free
    if free < 100 * 1024**3:
        optional.append(f"- Root volume free space is down to {format_size(free)}.")

    return stable, review, optional


def build_report() -> str:
    system = platform.system()
    if system not in {"Darwin", "Linux"}:
        raise SystemExit("AppLens-Tune.py is for macOS/Linux. Use AppLens-Tune.ps1 on Windows.")

    disk = shutil.disk_usage(Path.home().anchor or "/")
    startup_rows = startup_entries()
    service_rows = key_services()
    storage_rows = storage_hotspots()
    repo_rows = repo_placement()
    stable, review, optional = build_findings(startup_rows, service_rows, storage_rows, repo_rows)

    lines: list[str] = []
    lines.append("=== AppLens-Tune Audit Results ===")
    lines.append(f"Computer: {socket.gethostname()}")
    lines.append(f"User: {getpass.getuser()}")
    lines.append(f"Scan Date: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append("Mode: Audit (read-only)")
    lines.append("")
    lines.append(f"Machine: {platform.machine()}")
    lines.append(f"OS: {platform.platform()}")
    lines.append(f"RAM: {format_size(total_ram_bytes())}")
    lines.append(f"Root Free: {format_size(disk.free)}")
    lines.extend(section("--- Stability Checks ---", stable))
    lines.extend(section("--- Review Items ---", review))
    lines.extend(section("--- Optional Improvements ---", optional))
    lines.extend(section("--- Top Memory Processes ---", table(top_processes(), ["Name", "PID", "RSS_MB", "CPU_%"])))
    lines.extend(section("--- Startup Entries ---", table(startup_rows, ["Name", "State", "Source"])))
    lines.extend(section("--- Key Services/Processes ---", table(service_rows, ["Name", "Installed", "Running"])))
    lines.extend(section("--- Storage Hotspots ---", table(storage_rows, ["Location", "Size", "Path"])))
    lines.extend(section("--- Repo Placement ---", table(repo_rows, ["Root", "RepoCount", "Sample"])))
    lines.extend(section("--- Docker Summary ---", run_command(["docker", "system", "df"], timeout=12)))
    lines.extend(section("--- Ollama Summary ---", run_command(["ollama", "list"], timeout=12)))
    return "\n".join(lines) + "\n"


def main() -> int:
    report = build_report()
    path = output_path(f"AppLens_Tune_Results_{socket.gethostname()}.txt")
    path.write_text(report, encoding="utf-8")
    print(report, end="")
    print("")
    print("============================================")
    print("  Audit complete!")
    print("  Results saved to:")
    print(f"  {path}")
    print("============================================")
    return 0


if __name__ == "__main__":
    sys.exit(main())
