"""Local Linux inventory and opt-in diagnostics. Probe output stays local."""

from __future__ import annotations

import argparse
import contextlib
import getpass
import json
import os
import platform
import re
import shutil
import signal
import socket
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path


class ProbeError(Exception):
    def __init__(self, state):
        self.state = state


def command(*args, timeout=4, accepted=(0,), empty_error=None):
    if not shutil.which(args[0]):
        raise ProbeError("Unavailable")
    env = {**os.environ, "LC_ALL": "C", "HOMEBREW_NO_AUTO_UPDATE": "1",
           "SYSTEMD_PAGER": "cat", "PYTHONDONTWRITEBYTECODE": "1"}
    # A private process group lets Ctrl+C and timeouts stop only the probe tree.
    with subprocess.Popen(args, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, env=env, text=True, errors="replace",
                          start_new_session=True) as child:
        try:
            output, error = child.communicate(timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt) as exc:
            with contextlib.suppress(ProcessLookupError):
                os.killpg(child.pid, signal.SIGKILL)
            child.communicate()
            if isinstance(exc, KeyboardInterrupt):
                raise
            raise ProbeError("TimedOut") from exc
    if child.returncode not in accepted:
        if child.returncode == 1 and not output.strip() and empty_error and re.fullmatch(empty_error, error.strip()):
            return ""
        if re.search(r"permission denied|operation not permitted|access denied|must be root|not root",
                     error + output, re.I):
            raise ProbeError("PermissionLimited")
        if re.search(r"not been booted|failed to connect to bus|no such file or directory",
                     error, re.I):
            raise ProbeError("Unavailable")
        raise ProbeError("Failed")
    return output


def capture(name, source, read):
    started = time.monotonic()
    try:
        rows = read()
        state = "Succeeded" if rows else "Empty"
        row_states = {row.get("State") for row in rows if isinstance(row, dict)}
        failed_states = row_states & {"Unavailable", "PermissionLimited", "Partial", "TimedOut", "Failed"}
        if failed_states:
            state = (next(iter(failed_states)) if len(row_states) == 1 else "Partial")
    except ProbeError as exc:
        state, rows = exc.state, []
    except PermissionError:
        state, rows = "PermissionLimited", []
    except FileNotFoundError:
        state, rows = "Unavailable", []
    except (OSError, ValueError, KeyError, IndexError, AttributeError):
        state, rows = "Failed", []
    return {"Name": name, "Source": source, "State": state, "Rows": rows,
            "DurationSeconds": round(time.monotonic() - started, 3)}


def rows_of(text, **fields):
    return [{key: parts[index] for key, index in fields.items()} for line in text.splitlines()
            if len(parts := line.split()) > max(fields.values())]


def read_text(path):
    return Path(path).read_text(encoding="utf-8", errors="replace")


def properties(path, sep="="):
    return dict((key.strip(), value.strip().strip('"')) for line in read_text(path).splitlines()
                if sep in line for key, value in [line.split(sep, 1)])


def launcher_entries(roots, startup=False):
    seen, rows = set(), []
    for root in roots:
        try:
            entries = sorted(root.iterdir())
        except FileNotFoundError:
            continue
        for path in entries:
            if path.suffix != ".desktop" or path.name in seen:
                continue
            seen.add(path.name)
            # Later sections can contain action names and commands.
            sections = re.split(r"^\[([^\]]+)\]\s*$", read_text(path), flags=re.M)
            if "Desktop Entry" not in sections[1::2]:
                continue
            content = sections[sections.index("Desktop Entry") + 1]
            values = dict(line.split("=", 1) for line in content.splitlines()
                          if "=" in line and not line.startswith("#"))
            hidden = values.get("Hidden", "").lower() == "true"
            if startup:
                rows.append({"Name": values.get("Name", path.stem), "State": "Disabled" if hidden or
                             values.get("X-GNOME-Autostart-enabled", "").lower() == "false"
                             else "DesktopConditional" if ("OnlyShowIn" in values or "NotShowIn" in values)
                             else "Configured"})
            elif (not hidden and values.get("NoDisplay", "").lower() != "true"
                  and values.get("Type") == "Application" and values.get("Name")):
                rows.append({"Name": values["Name"].replace(r"\s", " "), "Source": "Desktop entry",
                             "Version": "", "Kind": "Desktop"})
    return rows


PACKAGE_PROBES = (
    ("dpkg", ("dpkg-query", "-W", "-f=${db:Status-Status}\t${binary:Package}\t${Version}\n")),
    ("rpm", ("rpm", "-qa", "--qf", "%{NAME}.%{ARCH}\t%{VERSION}-%{RELEASE}\n")),
    ("pacman", ("pacman", "-Q")),
    ("apk", ("apk", "info", "-v")),
    ("xbps", ("xbps-query", "-l")),
    ("Flatpak", ("flatpak", "list", "--columns=application,version")),
    ("Snap", ("snap", "list")),
    ("Homebrew formula", ("brew", "list", "--formula", "--versions")),
    ("Homebrew cask", ("brew", "list", "--cask", "--versions")),
)


def package_rows(source, output):
    packages = {}
    for line in output.splitlines():
        if not line.strip():
            continue
        parts = line.strip().split("\t") if "\t" in line else line.split()
        if source in {"dpkg", "xbps"}:
            if len(parts) < 2 or parts[0] != ("installed" if source == "dpkg" else "ii"):
                continue
            parts = parts[1:3] if source == "dpkg" else [parts[1]]
        if source == "Snap" and (parts[0] == "Name" or line.startswith("No snaps")):
            continue
        if source in {"apk", "xbps"}:
            match = re.match(r"^(.+?)-(\d[^\s]*)$", parts[0])
            if match:
                parts = list(match.groups())
        name, version = parts[0], " ".join(parts[1:] if source.startswith("Homebrew") else parts[1:2])
        packages[(name, version)] = {"Name": name, "Version": version, "Source": source, "Kind": "Package"}
    return list(packages.values())


def low_disk(free, total):
    return free * 10 < total if total > 0 else None


def mount_rows():
    rows = []
    for line in read_text("/proc/self/mountinfo").splitlines():
        left, right = line.split(" - ", 1)
        major, fstype = left.split()[2], right.split()[0]
        mount = re.sub(r"\\([0-7]{3})", lambda match: chr(int(match[1], 8)), left.split()[4])
        if (mount != "/" and major.startswith("0:") and fstype not in {"btrfs", "zfs", "fuseblk", "tmpfs"}) or (
                fstype == "tmpfs" and mount != "/dev/shm"):
            continue
        # Never stat mounted network shares. A report contains no mount paths.
        if fstype in {"nfs", "nfs4", "cifs", "smb3", "9p", "ceph", "fuse.sshfs"}:
            continue
        try:
            stats = os.statvfs(mount)
        except OSError:
            rows.append({"Volume": f"Mount {len(rows) + 1}", "FileSystem": fstype, "State": "Unavailable"})
            continue
        size, free = stats.f_blocks * stats.f_frsize, stats.f_bavail * stats.f_frsize
        if size:
            rows.append({"Volume": f"Mount {len(rows) + 1}", "FileSystem": fstype,
                         "TotalBytes": size, "FreeBytes": free, "UsedBytes": size - free,
                         "UsedPercent": round(100 * (size - free) / size, 1),
                         "LowSpace": low_disk(free, size)})
    return rows


def gpu_rows():
    rows = []
    drm = [(card / "device").resolve() for card in sorted(Path("/sys/class/drm").glob("card[0-9]*"))
           if re.fullmatch(r"card\d+", card.name)]
    pci = [device.resolve() for device in sorted(Path("/sys/bus/pci/devices").glob("*"))
           if (device / "class").is_file() and read_text(device / "class").strip().startswith("0x03")]
    for device in dict.fromkeys([*drm, *pci]):
        if not (device / "vendor").exists():
            continue
        rows.append({"VendorId": read_text(device / "vendor").strip(),
                     "DeviceId": read_text(device / "device").strip(),
                     "Backend": (device / "driver").resolve().name if (device / "driver").exists() else None,
                     "ReportedVramBytes": optional_int(device / "mem_info_vram_total"),
                     "ReportedGttApertureBytes": optional_int(device / "mem_info_gtt_total"),
                     "SharedOrReservedBytes": None,
                     "VerifiedUsableInferenceBytes": None})
    if not rows and Path("/dev/dxg").exists():
        rows.append({"Name": "Virtual GPU exposed to Linux", "Backend": "dxgkrnl",
                     "VerifiedUsableInferenceBytes": None})
    return rows


def optional_int(path):
    try:
        return int(read_text(path))
    except FileNotFoundError:
        return None


def inventory(tool_checks, is_runtime):
    statuses = []

    def read(name, source, action):
        result = capture(name, source, action)
        statuses.append({key: value for key, value in result.items() if key != "Rows"})
        return result["Rows"]

    os_info = read("Operating system", "os-release", lambda: [properties("/etc/os-release")])
    cpu = read("Processors", "procfs", lambda: sorted(set(
        match.strip() for match in re.findall(r"^(?:model name|Hardware)\s*:\s*(.*)$",
                                             read_text("/proc/cpuinfo"), re.M))))
    ram = read("Memory", "procfs", lambda: [int(re.search(rf"^{key}:\s+(\d+)", read_text(
        "/proc/meminfo"), re.M).group(1)) * 1024 for key in ("MemTotal", "MemAvailable")])
    uptime = read("Uptime", "procfs", lambda: [float(read_text("/proc/uptime").split()[0])])
    graphics = read("Graphics", "DRM sysfs", gpu_rows)
    disks = read("Mounts", "mountinfo / statvfs", mount_rows)
    data_home = Path(os.environ.get("XDG_DATA_HOME", Path.home() / ".local/share"))
    data_dirs = os.environ.get("XDG_DATA_DIRS", "/usr/local/share:/usr/share").split(":")
    apps = read("Desktop applications", "XDG", lambda: launcher_entries(
        [root / "applications" for root in [data_home, *map(Path, filter(None, data_dirs))]]))
    packages = [row for source, args in PACKAGE_PROBES
                for row in read(source, source, lambda: package_rows(source, command(*args)))]
    tools = [{"Name": label, "Command": tool, "State": "Succeeded" if shutil.which(tool) else "Unavailable"}
             for label, tool, _ in tool_checks]
    used = (ram[0] - ram[1]) / ram[0] if ram and ram[0] and 0 <= ram[1] <= ram[0] else None
    machine = {"ComputerName": "[computer]", "UserName": "[user]", "OSDescription":
               os_info[0].get("PRETTY_NAME", "Linux") if os_info else "Linux",
               "OSArchitecture": platform.machine(), "Processors": cpu, "Graphics": [
                   row["Name"] if "Name" in row else f"{row['VendorId']}:{row['DeviceId']}" for row in graphics],
               "TotalMemoryBytes": ram[0] if ram else None, "FreeMemoryBytes": ram[1] if ram else None,
               "MemoryUsedPercent": round(100 * used, 1) if used is not None else None,
               "HighMemoryUse": used >= .85 if used is not None else None,
               "UptimeSeconds": uptime[0] if uptime else None, "Disks": disks}
    items = {"DesktopApplications": sorted(apps, key=lambda row: row["Name"].casefold()),
             "Packages": packages, "RuntimesAndFrameworks": [row for row in [*apps, *packages]
                                                           if is_runtime(row["Name"])],
             "CliTools": tools, "StoreApplications": [], "SystemComponents": []}
    return machine, items, statuses


def startup():
    home = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
    system = os.environ.get("XDG_CONFIG_DIRS", "/etc/xdg").split(":")
    return launcher_entries([root / "autostart" for root in [home, *map(Path, filter(None, system))]],
                            startup=True)


def directory_summary(label, root, repos=False, pattern=None):
    """Bound the walk and omit filenames, crossing mount points or following links."""
    if not root.exists() or root.is_symlink() or root.is_mount():
        return []
    key = "RepositoryCount" if repos else "ModelCount" if pattern else "Bytes"
    deadline = time.monotonic() + 1.5
    filesystem = root.stat().st_dev
    total = visited = 0
    state = "Succeeded"

    def failed(error):
        nonlocal state
        state = "PermissionLimited" if isinstance(error, PermissionError) else "Partial"

    for path, dirs, files in os.walk(root, onerror=failed):
        if repos and (".git" in dirs or ".git" in files):
            total += 1
            dirs.clear()
        if repos and Path(path).relative_to(root).parts[4:]:
            dirs.clear()
        for name in dirs[:]:
            entry = Path(path, name)
            try:
                if entry.is_symlink() or entry.stat().st_dev != filesystem or entry.is_mount():
                    dirs.remove(name)
            except OSError as exc:
                failed(exc)
                dirs.remove(name)
        for name in (dirs if repos else [*dirs, *files]):
            if visited >= 50000 or time.monotonic() >= deadline:
                return [{"Location": label, key: total, "State": "Partial"}]
            visited += 1
            if not repos:
                try:
                    entry = Path(path, name)
                    if not entry.is_symlink() and entry.is_file():
                        if not pattern:
                            total += entry.stat().st_size
                        elif entry.match(pattern):
                            total += 1
                except OSError as exc:
                    failed(exc)
    return [{"Location": label, key: total, "State": state}]


def storage():
    home = Path.home()
    return [row for label, root in (("Ollama", home / ".ollama"), ("Package cache", home / ".cache"),
                                     ("Docker rootless", home / ".local/share/docker"),
                                     ("Docker system", Path("/var/lib/docker")))
            for row in directory_summary(label, root)]


def repositories():
    return [row for name in ("Projects", "src", "Desktop", "Documents")
            for row in directory_summary(name, Path.home() / name, repos=True)]


def battery(root=Path("/sys/class/power_supply")):
    if not root.is_dir():
        raise ProbeError("Unavailable")
    rows = []
    for device in sorted(root.iterdir()):
        if read_text(device / "type").strip() != "Battery":
            continue
        unit = "energy" if (device / "energy_full_design").exists() else "charge"
        full = optional_int(device / f"{unit}_full")
        design = optional_int(device / f"{unit}_full_design")
        cycles = optional_int(device / "cycle_count")
        rows.append({"FullCapacity": full, "DesignCapacity": design,
                     "CapacityUnit": "µWh" if unit == "energy" else "µAh",
                     "HealthPercent": round(100 * full / design, 1) if full is not None and design else None,
                     "Cycles": cycles, "CycleCountState": "Succeeded" if cycles is not None else "Unavailable",
                     "State": "Partial" if None in (full, design) else "Succeeded"})
    if not rows:
        raise ProbeError("Inapplicable")
    return rows


def block_devices():
    return json.loads(command("lsblk", "--json", "--list", "--output",
                              "NAME,TYPE,FSTYPE"))["blockdevices"]


def smart_health():
    devices = [row for row in block_devices() if row["type"] == "disk"
               and re.fullmatch(r"(?:sd[a-z]+|nvme\d+n\d+|hd[a-z]+|mmcblk\d+)", row["name"])]
    if not devices:
        raise ProbeError("Inapplicable")
    rows = []
    for device in devices:
        name = device["name"]
        node = "/dev/" + name
        row = {"Device": f"Disk {len(rows) + 1}", "Type": "NVMe" if name.startswith("nvme") else "ATA"}
        try:
            if name.startswith("nvme") and not shutil.which("smartctl"):
                info = json.loads(command("nvme", "smart-log", node, "--output-format=json"))
                warning = info.get("critical_warning")
                row.update(State="Succeeded" if warning is not None else "Partial", CriticalWarning=warning,
                           Passed=warning == 0 if warning is not None else None,
                           TemperatureKelvin=info.get("temperature"), MediaErrors=info.get("media_errors"),
                           PercentageUsed=info.get("percent_used"))
                rows.append(row)
                continue
            info = json.loads(command("smartctl", "-j", "-n", "standby", "-H", "-A", node,
                                      accepted=tuple(range(256))))
            messages = " ".join(m.get("string", "") for m in info.get("smartctl", {}).get("messages", []))
            if re.search(r"permission|operation not permitted", messages, re.I):
                raise ProbeError("PermissionLimited")
            passed = info.get("smart_status", {}).get("passed")
            nvme = info.get("nvme_smart_health_information_log", {})
            row.update(State="Succeeded" if passed is not None else "Unavailable", Passed=passed,
                       TemperatureC=info.get("temperature", {}).get("current"),
                       PercentageUsed=nvme.get("percentage_used"), MediaErrors=nvme.get("media_errors"))
        except ProbeError as exc:
            row["State"] = exc.state
        except ValueError:
            row["State"] = "Failed"
        rows.append(row)
    return rows


def encryption():
    devices = block_devices()
    filesystem_metadata = any(row.get("fstype") for row in devices)
    return [{"DetectedLuksContainers": sum(row.get("fstype") == "crypto_LUKS" for row in devices)
             if filesystem_metadata else None,
             "ActiveEncryptedMappings": sum(row["type"] == "crypt" for row in devices),
             "State": "Succeeded" if filesystem_metadata else "Partial"}]


def network():
    return [{"InterfaceCount": len(os.listdir("/sys/class/net")),
             "UpInterfaceCount": sum(read_text(path).strip() == "up"
                                     for path in Path("/sys/class/net").glob("*/operstate"))}]


def listeners():
    rows = []
    for path in Path("/proc/net").iterdir():
        if path.name not in {"tcp", "tcp6", "udp", "udp6"}:
            continue
        tcp = path.name.startswith("tcp")
        entries = rows_of("\n".join(read_text(path).splitlines()[1:]), Peer=2, State=3)
        count = sum(entry["State"] == "0A" if tcp else entry["Peer"].endswith(":0000") for entry in entries)
        rows.append({"Transport": "TCP" if tcp else "UDP",
                     "IPVersion": 6 if path.name.endswith("6") else 4, "Count": count})
    return rows


def firewall():
    if shutil.which("nft"):
        rules = json.loads(command("nft", "-j", "list", "ruleset"))["nftables"]
        return [{"RuleCount": sum("rule" in item for item in rules),
                 "BaseChainPolicies": sorted(item["chain"]["policy"] for item in rules
                                             if "chain" in item and "policy" in item["chain"])}]
    return [{"RuleCount": sum(line.startswith("-A ") for line in command("iptables", "-S").splitlines())}]


def cron():
    sources = [Path("/etc/crontab"), *Path("/etc/cron.d").glob("*")]
    entries = []
    for path in sources:
        if path.is_file():
            entries.extend(read_text(path).splitlines())
    try:
        user_entries = command("crontab", "-l", empty_error=r"(?:crontab: )?no crontab for .+").splitlines()
        entries.extend(user_entries)
        user_state = "Succeeded" if user_entries else "Empty"
    except ProbeError as exc:
        user_state = exc.state
    periodic = sum(path.is_file() and os.access(path, os.X_OK)
                   for interval in ("hourly", "daily", "weekly", "monthly")
                   for path in Path(f"/etc/cron.{interval}").glob("*"))
    return [{"JobCount": sum(bool(re.match(r"^\s*(?:@\w+\s|[\d*/])", line)) for line in entries),
             "PeriodicScriptCount": periodic, "UserCrontabState": user_state,
             "State": "Succeeded" if user_state in {"Succeeded", "Empty"} else "Partial"}]


def systemd(scope, *args):
    return command("systemctl", f"--{scope}", "--no-pager", "--no-legend", *args)


def services(scope):
    units = rows_of(systemd(scope, "list-units", "--type=service", "--all", "--plain"),
                    Load=1, Active=2, SubState=3)
    counts = {}
    for row in units:
        key = tuple(row.values())
        counts[key] = counts.get(key, 0) + 1
    return [{"Load": load, "Active": active, "SubState": sub, "Count": count}
            for (load, active, sub), count in sorted(counts.items())]


def backup_outcomes(scope):
    configured = rows_of(systemd(scope, "list-unit-files", "--type=service"), Unit=0)
    units = [row["Unit"] for row in configured if re.search(
        r"(?:backup|restic|borg|kopia|snapper|timeshift)", row["Unit"], re.I)
        and re.fullmatch(r"[\w.@-]+\.service", row["Unit"]) and "@." not in row["Unit"]]
    rows = []
    for index, unit in enumerate(units[:8], 1):
        try:
            details = dict(line.split("=", 1) for line in systemd(scope, "show",
                "--property=Result,ExecMainExitTimestamp,ExecMainStatus", "--", unit).splitlines() if "=" in line)
        except ProbeError as exc:
            rows.append({"Job": f"Backup service {index}", "State": exc.state})
            continue
        observed = bool(details.get("ExecMainExitTimestamp"))
        rows.append({"Job": f"Backup service {index}", "Observed": observed,
                     "LastExitAt": details.get("ExecMainExitTimestamp") if observed else None,
                     "Result": details.get("Result") if observed else None,
                     "ExitStatus": details.get("ExecMainStatus") if observed else None})
    if len(units) > 8:
        rows.append({"State": "Partial"})
    return rows


def updates():
    output = command("apt", "list", "--upgradable", timeout=6)
    candidates = [line for line in output.splitlines() if "[upgradable from:" in line]
    return [{"CachedCandidateCount": len(candidates),
             "SecurityCandidateCount": sum("-security" in line.split()[0] for line in candidates),
             "MetadataRefreshed": False}]


def reboot():
    if not Path("/etc/debian_version").exists():
        raise ProbeError("Unavailable")
    return [{"RebootRequired": Path("/run/reboot-required").exists()}]


def docker_usage():
    sockets = [Path(f"/run/user/{os.getuid()}/docker.sock"), Path("/var/run/docker.sock")]
    endpoint = next((candidate for candidate in sockets if candidate.is_socket()), None)
    if endpoint is None:
        raise ProbeError("Unavailable")
    return [json.loads(line) for line in command(
        "docker", "--host", f"unix://{endpoint}", "system", "df", "--format",
        '{"Type":{{json .Type}},"Total":{{json .TotalCount}},"Active":{{json .Active}},"Size":{{json .Size}}}'
    ).splitlines() if line.strip()]


LLAMA_DEVICE = re.compile(r"^\s*((?:CUDA|Vulkan|ROCm|HIP|SYCL|Metal|CPU)[\w.-]*):\s*(.+)$", re.I)


def llama_devices():
    binary = next((name for name in ("llama-cli", "llama-server") if shutil.which(name)), None)
    if not binary:
        raise ProbeError("Unavailable")
    return [{"Device": match[1], "Description": match[2],
             "Backend": re.match(r"[A-Za-z]+", match[1])[0],
             "VerifiedUsableInferenceBytes": None}
            for line in command(binary, "--list-devices").splitlines() if (match := LLAMA_DEVICE.match(line))]


TORCH_PROBE = (
    "import importlib.util, json; "
    "s=importlib.util.find_spec('torch'); "
    "exec(\"import torch\\na=torch.cuda.is_available()\\n"
    "print(json.dumps([{'Version':torch.__version__,'Backend':'ROCm' if torch.version.hip else "
    "'CUDA' if torch.version.cuda else 'CPU','DeviceCount':torch.cuda.device_count() if a else 0,"
    "'VerifiedUsableInferenceBytes':None}]))\") if s else print('[]')"
)


def diagnostics(machine):
    probes = [
        ("Processes", "ps", lambda: rows_of(command("ps", "-eo", "pid=,rss=,time=,comm=", "--sort=-rss"),
                                             Pid=0, WorkingSetKiB=1, CpuTime=2, Name=3)[:15]),
        ("Startup", "XDG autostart", startup),
        ("Storage", "Selected folders", storage),
        ("Git placement", "Selected folders", repositories),
        ("Docker storage", "Local Docker socket", docker_usage),
        ("AI tools", "PATH", lambda: [{"Tool": name} for name in
                                     ("ollama", "llama-cli", "llama-server", "lmstudio", "nvcc")
                                     if shutil.which(name)]),
        ("Local models", "Selected model stores", lambda: [
            *directory_summary("Ollama manifests", Path.home() / ".ollama/models/manifests", pattern="*"),
            *directory_summary("LM Studio GGUF", Path.home() / ".lmstudio/models", pattern="*.gguf")]),
        ("Graphics memory", "DRM sysfs", gpu_rows),
        ("Graphics memory", "nvidia-smi", lambda: [
            {"Device": cells[0], "Backend": "NVIDIA driver", "ReportedVramMiB": cells[1],
             "ReportedFreeVramMiB": cells[2], "VerifiedUsableInferenceBytes": None}
            for line in command("nvidia-smi", "--query-gpu=name,memory.total,memory.free",
                                "--format=csv,noheader,nounits").splitlines()
            if len(cells := [part.strip() for part in line.split(",")]) == 3]),
        ("llama.cpp", "Local device enumeration", llama_devices),
        ("CUDA toolkit", "nvcc", lambda: [
            {"Release": match[1]} for match in re.finditer(r"release ([\d.]+)", command("nvcc", "--version"))]),
        ("PyTorch", "Local Python runtime", lambda: json.loads(command(
            sys.executable, "-I", "-B", "-c", TORCH_PROBE, timeout=8))),
        ("Disk health", "SMART / NVMe", smart_health),
        ("Battery", "Power supply sysfs", battery),
        ("Disk encryption", "lsblk", encryption),
        ("Network", "Network sysfs", network),
        ("DNS", "resolv.conf", lambda: [{"ResolverCount": sum(
            line.strip().startswith("nameserver ") for line in read_text("/etc/resolv.conf").splitlines())}]),
        ("Listeners", "procfs", listeners),
        ("Firewall", "nftables / iptables", firewall),
        ("Scheduled jobs", "cron", cron),
        ("Security updates", "Cached APT metadata", updates),
        ("Reboot", "Debian reboot marker", reboot),
    ]
    for scope in ("system", "user"):
        probes.extend([
            ("Services", f"systemd {scope}", lambda scope=scope: services(scope)),
            ("Scheduled jobs", f"systemd {scope}", lambda scope=scope: [
                {"TimerCount": sum(".timer" in line for line in systemd(scope, "list-timers", "--all").splitlines())}]),
            ("Backup outcomes", f"systemd {scope}", lambda scope=scope: backup_outcomes(scope)),
        ])
    results = [capture(*probe) for probe in probes]
    results.append({"Name": "WSL", "Source": "Windows subsystem", "State": "Inapplicable",
                    "Rows": [], "DurationSeconds": 0})
    findings = [{"Category": "Storage", "Observation": "Volume is below the free space threshold."}
                for disk in machine["Disks"] if disk.get("LowSpace")]
    if machine.get("HighMemoryUse"):
        findings.append({"Category": "Memory", "Observation": "At least 85% of RAM was in use at capture."})
    findings.extend({"Category": "Disk health", "Observation": "A disk reported a SMART health failure."}
                    for result in results if result["Name"] == "Disk health"
                    for row in result["Rows"] if row.get("Passed") is False)
    return results + [capture("Findings", "Local rules", lambda: findings)]


def scan(tool_checks, is_runtime, diagnostic=False):
    start = time.monotonic()
    timestamp = datetime.now(timezone.utc).isoformat()
    machine, items, statuses = inventory(tool_checks, is_runtime)
    details = diagnostics(machine) if diagnostic else []
    covered = {"Succeeded", "Empty", "Inapplicable"}
    report = {"SchemaVersion": "3.0", "Mode": "Diagnostics" if diagnostic else "Inventory", "Platform": "Linux",
            "GeneratedAt": timestamp, "Machine": machine, "Inventory": items,
            "ProbeStatuses": statuses, "Diagnostics": details, "Actions": [],
            "DurationSeconds": round(time.monotonic() - start, 3),
            "IsPartial": any(row["State"] not in covered and not (
                row["State"] == "Unavailable" and row["Name"] in {name for name, _ in PACKAGE_PROBES})
                for row in [*statuses, *details])}
    return redact(report)


def redact(report, *, user=None, host=None, home=None):
    user = getpass.getuser() if user is None else user
    host = socket.gethostname() if host is None else host
    home = str(Path.home()) if home is None else home
    replacements = {"profile": "%USERPROFILE%", "computer": "[computer]", "user": "[user]"}
    identifiers = [(field, value) for field, value in (("profile", home), ("computer", host), ("user", user))
                   if value]
    if not identifiers:
        return report
    pattern = re.compile("|".join(
        rf"(?P<{field}>{re.escape(value)})" if field == "profile" else
        rf"(?<![\w-])(?P<{field}>{re.escape(value)})(?![\w-])" for field, value in identifiers), re.I)
    # Redact observations without rewriting schema values such as Mode and State.
    def clean(value):
        if isinstance(value, str):
            return pattern.sub(lambda match: replacements[match.lastgroup], value)
        if isinstance(value, list):
            return [clean(item) for item in value]
        if isinstance(value, dict):
            return {key: item if key in {"State", "Source", "Kind"} else clean(item)
                    for key, item in value.items()}
        return value
    report["Machine"] = clean(report["Machine"])
    report["Inventory"] = clean(report["Inventory"])
    report["Diagnostics"] = [{**probe, "Rows": clean(probe["Rows"])} for probe in report["Diagnostics"]]
    return report


def main(tool_checks, is_runtime):
    parser = argparse.ArgumentParser(description="Scan local inventory and optionally run diagnostics.")
    parser.add_argument("--diagnostics", action="store_true", help="Run the slower local diagnostics scan.")
    parser.add_argument("--output", type=Path, help="Save the redacted JSON to this path. Default is stdout.")
    args = parser.parse_args()
    try:
        report = json.dumps(scan(tool_checks, is_runtime, args.diagnostics), indent=2) + "\n"
        if args.output:
            with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=args.output.parent,
                                             delete=False) as output:
                temp = Path(output.name)
                try:
                    output.write(report)
                    output.close()
                    temp.replace(args.output)
                finally:
                    temp.unlink(missing_ok=True)
        else:
            sys.stdout.write(report)
        return 0
    except KeyboardInterrupt:
        return 130
    except OSError:
        print("Could not write the report.", file=sys.stderr)
        return 1
