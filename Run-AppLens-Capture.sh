#!/usr/bin/env sh
set -u

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PYTHON_BIN=${PYTHON:-}

if [ -z "$PYTHON_BIN" ]; then
  for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1 &&
      "$candidate" -c 'import sys; raise SystemExit(0 if sys.version_info[0] >= 3 else 1)' >/dev/null 2>&1; then
      PYTHON_BIN=$candidate
      break
    fi
  done
fi

if [ -z "$PYTHON_BIN" ]; then
  echo "Python 3 is required to run AppLens on macOS/Linux." >&2
  exit 1
fi

HOST_NAME=$(hostname 2>/dev/null || echo "unknown")
DESKTOP_DIR="$HOME/Desktop"
if [ ! -d "$DESKTOP_DIR" ]; then
  DESKTOP_DIR="$HOME"
fi

CAPTURE_DIR="$DESKTOP_DIR/AppLens-Capture-$HOST_NAME"
mkdir -p "$CAPTURE_DIR"
export APPLENS_OUTPUT_DIR="$CAPTURE_DIR"

CAPTURE_EXIT=0

run_script() {
  app_name=$1
  script_name=$2
  log_name=$3
  log_path="$CAPTURE_DIR/$log_name"

  echo "Running $app_name..."
  echo "[$(date)] Running $script_name" >"$log_path"
  if "$PYTHON_BIN" "$SCRIPT_DIR/$script_name" >>"$log_path" 2>&1; then
    echo "[$(date)] Exit code: 0" >>"$log_path"
    echo "$app_name finished."
  else
    echo "[$(date)] Exit code: 1" >>"$log_path"
    echo "$app_name had an issue. See $log_path"
    CAPTURE_EXIT=1
  fi
}

echo "AppLens Capture"
echo
echo "Output folder:"
echo "$CAPTURE_DIR"
echo

run_script "AppLens" "AppLens.py" "AppLens_Run_Log.txt"
run_script "AppLens-Tune" "AppLens-Tune.py" "AppLens_Tune_Run_Log.txt"

cat >"$CAPTURE_DIR/README-What-To-Send.txt" <<EOF
Send this entire folder back for AppLens intake.

Expected report files:
- AppLens_Results_$HOST_NAME.txt
- AppLens_Tune_Results_$HOST_NAME.txt

Include the log files if either report is missing.
Do not include serial numbers or UUIDs unless explicitly requested.
EOF

if command -v open >/dev/null 2>&1; then
  open "$CAPTURE_DIR" >/dev/null 2>&1 || true
elif command -v xdg-open >/dev/null 2>&1; then
  xdg-open "$CAPTURE_DIR" >/dev/null 2>&1 || true
fi

echo
if [ "$CAPTURE_EXIT" -eq 0 ]; then
  echo "Capture finished."
else
  echo "Capture finished with at least one issue. Include the log files."
fi
echo "Folder: $CAPTURE_DIR"
exit "$CAPTURE_EXIT"
