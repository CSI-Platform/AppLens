# AppLens — Pre-Audit App Scanner

A lightweight read-only scanner for pre-audit workstation inventory. Built for IT consultants running workflow audits who want to know what software employees actually have before sitting down with them.

AppLens now has a sibling audit surface, `AppLens-Tune`, for read-only workstation tuning checks. The scanner stays focused on installed software; the tune audit focuses on what starts, runs, and consumes space.

## What It Does

- Scans installed desktop apps on Windows, macOS, and Linux
- Includes Windows Store apps, macOS `.app` bundles/Homebrew casks, and Linux desktop entries/Flatpak/Snap apps where available
- Filters out system components, drivers, updates, and framework junk
- Tags user-installed apps (potential shadow IT) with `[User-installed]`
- Groups Microsoft 365 apps together
- Outputs a clean text file to the Desktop, ready to paste into an AI prompt

## Usage

### Windows

#### Option 1: Double-click

Run `Run-AppLens.bat`. The results file appears on the Desktop.

#### Option 2: PowerShell one-liner

```powershell
powershell -ExecutionPolicy Bypass -File AppLens.ps1
```

#### Option 3: Remote execution

Host `AppLens.ps1` at a URL and have employees run:

```powershell
powershell -ExecutionPolicy Bypass -Command "irm https://your-url/AppLens.ps1 | iex"
```

### macOS and Linux

Run the shell launcher from this folder:

```sh
chmod +x Run-AppLens.sh
./Run-AppLens.sh
```

Or call the Python script directly:

```sh
python3 AppLens.py
```

## Output

Results are saved to the Desktop as `AppLens_Results_<ComputerName>.txt`:

```
=== AppLens Scan Results ===
Computer: FRONT-DESK-01
User: sarah.jones
Scan Date: 2026-03-14

--- Desktop Applications ---
Microsoft 365 (Office)
  - Microsoft Excel (Version 2402)
  - Microsoft Outlook (Version 2402)
  - Microsoft Word (Version 2402)
Google Chrome (Version 122.0)
Dropbox (Version 198.3)          [User-installed]

--- Microsoft Store Apps ---
Microsoft Whiteboard

--- Runtimes & Frameworks (for reference) ---
.NET Runtime 8.0.2
```

## Requirements

- Windows 10/11 with PowerShell 5.1+, or macOS/Linux with Python 3
- No admin rights needed

## AppLens-Tune

`AppLens-Tune` is the read-only workstation audit companion. It is meant to answer: what is running, what starts automatically, what consumes storage/memory, and what should be reviewed before a paid tuning or workflow engagement.

It collects:

- top memory processes
- startup commands
- key workstation services
- WSL, Docker, Ollama, and equivalent macOS/Linux dev-tool state where available
- storage hotspots for common local AI/dev caches
- repo placement checks for common synced/dev roots

### Usage

### Windows double-click

Run `Run-AppLens-Tune.bat`. The results file appears on the Desktop.

### Windows PowerShell one-liner

```powershell
powershell -ExecutionPolicy Bypass -File AppLens-Tune.ps1
```

### macOS and Linux

```sh
chmod +x Run-AppLens-Tune.sh
./Run-AppLens-Tune.sh
```

Or:

```sh
python3 AppLens-Tune.py
```

### Output

Results are saved to the Desktop as `AppLens_Tune_Results_<ComputerName>.txt`.
