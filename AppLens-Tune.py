#!/usr/bin/env python3
"""
AppLens-Tune read-only workstation audit for macOS and Linux.

This is audit mode only: it observes process, startup, service, storage, and
repo-placement signals. It does not change the machine.
"""

from __future__ import annotations

import getpass
import csv
import json
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
    heading = title.strip().strip("-").strip()
    return ["", f"## {heading}", *(lines if lines else ["(none)"])]


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


def first_version_line(lines: list[str]) -> str:
    for line in lines:
        if line and not line.lower().startswith("warning"):
            return line[:120]
    for line in lines:
        if "version" in line.lower():
            return line[:120]
    return lines[0][:120] if lines else ""


def local_llm_tools() -> list[dict[str, str]]:
    checks = (
        ("git", "git", ["--version"]),
        ("python3", "python3", ["--version"]),
        ("pip3", "pip3", ["--version"]),
        ("uv", "uv", ["--version"]),
        ("cmake", "cmake", ["--version"]),
        ("make", "make", ["--version"]),
        ("gcc", "gcc", ["--version"]),
        ("g++", "g++", ["--version"]),
        ("docker", "docker", ["--version"]),
        ("ollama", "ollama", ["--version"]),
        ("nvidia-smi", "nvidia-smi", ["--version"]),
        ("nvcc", "nvcc", ["--version"]),
    )
    rows: list[dict[str, str]] = []
    for label, command, args in checks:
        if not shutil.which(command):
            rows.append({"Tool": label, "Status": "Missing", "Detail": ""})
            continue
        lines = run_command([command, *args], timeout=8)
        rows.append({"Tool": label, "Status": "Present", "Detail": first_version_line(lines)})
    return rows


def llama_cpp_builds() -> list[dict[str, str]]:
    root = Path.home() / "local-llm/src/llama.cpp"
    rows: list[dict[str, str]] = []
    if not root.exists():
        return [{"Build": "llama.cpp source", "Status": "Missing", "Path": str(root)}]

    commit = ""
    if (root / ".git").exists():
        lines = run_command(["git", "-C", str(root), "rev-parse", "--short", "HEAD"], timeout=5)
        commit = lines[0] if lines else ""

    rows.append({"Build": "llama.cpp source", "Status": f"Present {commit}".strip(), "Path": str(root)})
    for build_name in ("build-cpu", "build-cuda", "build-cuda-mmq", "build-vulkan"):
        build_dir = root / build_name
        bin_dir = build_dir / "bin"
        built = [
            name
            for name in ("llama-cli", "llama-server", "llama-bench")
            if (bin_dir / name).exists()
        ]
        status = "Built: " + ", ".join(built) if built else "Missing"
        rows.append({"Build": build_name, "Status": status, "Path": str(build_dir)})
    return rows


def ollama_cached_models() -> list[dict[str, str]]:
    manifests = Path.home() / ".ollama/models/manifests"
    if not manifests.exists():
        return [{"Model": "(none)", "Size": "", "Manifest": str(manifests)}]

    rows: list[dict[str, str]] = []
    for manifest in sorted(manifests.rglob("*")):
        if not manifest.is_file():
            continue
        try:
            rel = manifest.relative_to(manifests)
            parts = rel.parts
            model = "/".join(parts[:-1]) + ":" + parts[-1] if len(parts) >= 2 else rel.as_posix()
            payload = json.loads(manifest.read_text(encoding="utf-8", errors="replace"))
            size = sum(int(layer.get("size", 0)) for layer in payload.get("layers", []))
            rows.append({"Model": model, "Size": format_size(size), "Manifest": str(manifest)})
        except Exception:
            rows.append({"Model": manifest.name, "Size": "", "Manifest": str(manifest)})
    return rows or [{"Model": "(none)", "Size": "", "Manifest": str(manifests)}]


def nvidia_gpus() -> list[dict[str, str]]:
    if not shutil.which("nvidia-smi"):
        return []

    lines = run_command(
        [
            "nvidia-smi",
            "--query-gpu=name,driver_version,memory.total,memory.used,compute_cap,power.limit",
            "--format=csv,noheader,nounits",
        ],
        timeout=10,
    )
    rows: list[dict[str, str]] = []
    for fields in csv.reader(lines):
        if len(fields) < 6:
            continue
        rows.append(
            {
                "Name": fields[0].strip(),
                "Driver": fields[1].strip(),
                "VRAM_MB": fields[2].strip(),
                "Used_MB": fields[3].strip(),
                "Compute": fields[4].strip(),
                "Power_W": fields[5].strip(),
            }
        )
    return rows


def parse_int(value: str) -> int | None:
    try:
        return int(float(value.strip()))
    except (TypeError, ValueError):
        return None


def pytorch_probe() -> list[dict[str, str]]:
    python = shutil.which("python3") or shutil.which("python")
    if not python:
        return [{"Component": "PyTorch", "Status": "Missing", "Detail": "Python 3 not found"}]

    script = """
import json
try:
    import torch
    payload = {
        "installed": True,
        "version": getattr(torch, "__version__", ""),
        "cuda_available": bool(torch.cuda.is_available()),
        "cuda_version": getattr(torch.version, "cuda", None),
        "device": torch.cuda.get_device_name(0) if torch.cuda.is_available() else "",
        "vram": torch.cuda.get_device_properties(0).total_memory if torch.cuda.is_available() else 0,
    }
except Exception as exc:
    payload = {"installed": False, "error": f"{type(exc).__name__}: {exc}"}
print(json.dumps(payload, sort_keys=True))
""".strip()

    lines = run_command([python, "-c", script], timeout=20)
    try:
        payload = json.loads(lines[-1])
    except Exception:
        return [{"Component": "PyTorch", "Status": "Error", "Detail": "; ".join(lines)[:160]}]

    if not payload.get("installed"):
        return [{"Component": "PyTorch", "Status": "Missing", "Detail": str(payload.get("error", ""))[:160]}]

    status = "CUDA ready" if payload.get("cuda_available") else "Installed, CUDA unavailable"
    detail = f"{payload.get('version', '')}; CUDA {payload.get('cuda_version') or 'n/a'}"
    if payload.get("device"):
        detail += f"; {payload['device']} ({format_size(int(payload.get('vram') or 0))})"
    return [{"Component": "PyTorch", "Status": status, "Detail": detail[:160]}]


def max_vram_mb(gpu_rows: list[dict[str, str]]) -> int:
    values = [parse_int(row["VRAM_MB"]) for row in gpu_rows]
    return max([value for value in values if value is not None], default=0)


def local_llm_profile(
    gpu_rows: list[dict[str, str]],
    tool_rows: list[dict[str, str]],
    torch_rows: list[dict[str, str]],
    service_rows: list[dict[str, str]],
    llama_rows: list[dict[str, str]],
) -> tuple[list[dict[str, str]], list[str], list[str]]:
    tool_status = {row["Tool"]: row["Status"] for row in tool_rows}
    service_status = {row["Name"]: row for row in service_rows}
    vram_mb = max_vram_mb(gpu_rows)
    llama_status = {row["Build"]: row["Status"] for row in llama_rows}
    review: list[str] = []
    optional: list[str] = []

    if vram_mb <= 0:
        gpu_tier = "CPU or non-NVIDIA profile"
        backend = "CPU llama.cpp/Ollama inference; avoid GPU training assumptions."
        model_target = "1B-4B quantized models for interactive work."
        context_target = "2k-8k context unless benchmarks prove more headroom."
        training_target = "CPU-only dataset prep, evals, and tiny smoke tests."
    elif vram_mb < 8 * 1024:
        gpu_tier = "Small NVIDIA GPU profile (under 8 GB VRAM)"
        backend = "GGUF inference through Ollama, Jan, or llama.cpp; PyTorch only after CUDA smoke passes."
        model_target = "3B-8B Q4/IQ4-class models; avoid 27B-31B interactive local-agent loops here."
        context_target = "4k-16k for inference; 256-512 tokens for training/autoresearch experiments."
        training_target = "Tiny from-scratch models, classifiers, eval sweeps, and very small LoRA tests."
        review.append("- NVIDIA VRAM is under 8 GB; tune for small-model workloads, not large local fine-tunes.")
    elif vram_mb < 16 * 1024:
        gpu_tier = "Mid NVIDIA GPU profile"
        backend = "GGUF inference plus selective PyTorch fine-tune experiments."
        model_target = "7B-14B quantized inference; small LoRA experiments after benchmarking."
        context_target = "8k-32k for inference; benchmark before larger context."
        training_target = "Small LoRA/QLoRA experiments with conservative batch and sequence length."
    else:
        gpu_tier = "Large local-GPU profile"
        backend = "llama.cpp/Ollama/Jan for inference and PyTorch for broader fine-tune experiments."
        model_target = "14B+ quantized inference and larger LoRA experiments, subject to benchmarks."
        context_target = "16k-64k after prompt-eval and memory tests."
        training_target = "LoRA/QLoRA and longer autoresearch sweeps with checkpointing."

    torch_status = torch_rows[0]["Status"] if torch_rows else "Missing"
    if "CUDA ready" not in torch_status:
        review.append("- PyTorch CUDA is not ready; training experiments should wait for a CUDA smoke test.")
    if tool_status.get("uv") == "Missing":
        optional.append("- uv is missing; Python ML environments will be slower to create and reproduce.")
    if tool_status.get("cmake") == "Missing":
        optional.append("- cmake is missing; local llama.cpp builds will fail until it is installed.")
    if tool_status.get("nvcc") == "Missing":
        optional.append("- nvcc is missing; CUDA extension builds are not available, but prebuilt PyTorch wheels can still work.")
    gpu_builds = ("build-cuda", "build-cuda-mmq", "build-vulkan")
    if all("Built:" not in llama_status.get(build_name, "") for build_name in gpu_builds):
        optional.append("- llama.cpp GPU build is missing; current llama.cpp binaries are CPU-only.")

    ollama = service_status.get("ollama", {})
    if ollama.get("Installed") == "Yes" and ollama.get("Running") != "Yes":
        optional.append("- Ollama is installed but not running; start it before runtime benchmarks.")

    rows = [
        {"Signal": "GPU tier", "Recommendation": gpu_tier},
        {"Signal": "Backend", "Recommendation": backend},
        {"Signal": "Model target", "Recommendation": model_target},
        {"Signal": "Context target", "Recommendation": context_target},
        {"Signal": "Training target", "Recommendation": training_target},
        {"Signal": "Safe overnight jobs", "Recommendation": "read-only scans, llama.cpp/Ollama benchmarks, eval sweeps, dataset prep"},
        {"Signal": "Manual-gated jobs", "Recommendation": "driver/CUDA changes, service changes, firmware/RF/Wi-Fi actions, large downloads"},
    ]
    return rows, review, optional


def autoresearch_queue(
    llama_rows: list[dict[str, str]],
    ollama_model_rows: list[dict[str, str]],
    torch_rows: list[dict[str, str]],
) -> list[dict[str, str]]:
    llama_status = {row["Build"]: row["Status"] for row in llama_rows}
    has_mmq = "Built:" in llama_status.get("build-cuda-mmq", "")
    has_cuda = "Built:" in llama_status.get("build-cuda", "") or has_mmq
    models = [row["Model"] for row in ollama_model_rows if row.get("Model") and row.get("Model") != "(none)"]
    torch_status = torch_rows[0]["Status"] if torch_rows else "Missing"

    runtime = "llama.cpp CUDA-MMQ" if has_mmq else "llama.cpp CUDA" if has_cuda else "llama.cpp CPU/Ollama"
    model = models[0] if models else "no cached model detected"
    training_gate = "closed" if "CUDA ready" not in torch_status else "manual approval required"

    return [
        {"Queue": "Runtime", "State": runtime, "Boundary": "read-only inference and benchmarks"},
        {"Queue": "Seed model", "State": model, "Boundary": "use cached models unless a user approves downloads"},
        {"Queue": "Unattended OK", "State": "AppLens scans, llama.cpp benchmarks, eval sweeps, dataset prep", "Boundary": "no service/system changes"},
        {"Queue": "Training", "State": training_gate, "Boundary": "wait for PyTorch CUDA smoke test and user approval"},
        {"Queue": "Stop conditions", "State": "capture metrics, cap run time, keep logs", "Boundary": "abort on OOM, thermal issues, or failed smoke tests"},
    ]


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
    llm_review: list[str],
    llm_optional: list[str],
) -> tuple[list[str], list[str], list[str]]:
    stable = ["- Audit mode only; no changes were made."]
    review: list[str] = [*llm_review]
    optional: list[str] = [*llm_optional]

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
    llm_tool_rows = local_llm_tools()
    llama_rows = llama_cpp_builds()
    ollama_model_rows = ollama_cached_models()
    gpu_rows = nvidia_gpus()
    torch_rows = pytorch_probe()
    llm_rows, llm_review, llm_optional = local_llm_profile(gpu_rows, llm_tool_rows, torch_rows, service_rows, llama_rows)
    autoresearch_rows = autoresearch_queue(llama_rows, ollama_model_rows, torch_rows)
    stable, review, optional = build_findings(startup_rows, service_rows, storage_rows, repo_rows, llm_review, llm_optional)

    lines: list[str] = []
    lines.append("# AppLens-Tune Audit Results")
    lines.append(f"- **Computer:** {socket.gethostname()}")
    lines.append(f"- **User:** {getpass.getuser()}")
    lines.append(f"- **Scan Date:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append("- **Mode:** Audit (read-only)")
    lines.append("")
    lines.append(f"- **Machine:** {platform.machine()}")
    lines.append(f"- **OS:** {platform.platform()}")
    lines.append(f"- **RAM:** {format_size(total_ram_bytes())}")
    lines.append(f"- **Root Free:** {format_size(disk.free)}")
    lines.extend(section("--- Stability Checks ---", stable))
    lines.extend(section("--- Review Items ---", review))
    lines.extend(section("--- Optional Improvements ---", optional))
    lines.extend(section("--- Local LLM Profile ---", table(llm_rows, ["Signal", "Recommendation"])))
    lines.extend(section("--- Auto-Research Queue ---", table(autoresearch_rows, ["Queue", "State", "Boundary"])))
    lines.extend(section("--- NVIDIA GPU Profile ---", table(gpu_rows, ["Name", "Driver", "VRAM_MB", "Used_MB", "Compute", "Power_W"])))
    lines.extend(section("--- PyTorch CUDA Probe ---", table(torch_rows, ["Component", "Status", "Detail"])))
    lines.extend(section("--- Local LLM Toolchain ---", table(llm_tool_rows, ["Tool", "Status", "Detail"])))
    lines.extend(section("--- llama.cpp Builds ---", table(llama_rows, ["Build", "Status", "Path"])))
    lines.extend(section("--- Ollama Cached Models ---", table(ollama_model_rows, ["Model", "Size", "Manifest"])))
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
    path = output_path(f"AppLens_Tune_Results_{socket.gethostname()}.md")
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
