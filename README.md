# AppLens — Pre-Audit App Scanner

A lightweight PowerShell script that scans installed applications on a Windows machine — no admin rights or installation required. Built for IT consultants running workflow audits who want to know what software employees actually have before sitting down with them.

## What It Does

- Scans Win32 desktop apps (registry) and Microsoft Store apps
- Filters out system components, drivers, updates, and framework junk
- Tags user-installed apps (potential shadow IT) with `[User-installed]`
- Groups Microsoft 365 apps together
- Outputs a clean text file to the Desktop, ready to paste into an AI prompt

## Usage

### Option 1: Double-click

Run `Run-AppLens.bat`. The results file appears on the Desktop.

### Option 2: PowerShell one-liner

```powershell
powershell -ExecutionPolicy Bypass -File AppLens.ps1
```

### Option 3: Remote execution

Host `AppLens.ps1` at a URL and have employees run:

```powershell
powershell -ExecutionPolicy Bypass -Command "irm https://your-url/AppLens.ps1 | iex"
```

## Output

Results are saved to `Desktop\AppLens_Results_<ComputerName>.txt`:

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

- Windows 10 or 11
- PowerShell 5.1+ (built into Windows)
- No admin rights needed
