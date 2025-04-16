#!/bin/bash
# scripts/ubuntu/capture_essential_benchmark_logs.sh

# This script extracts essential log information for a specific benchmark
# from a BenchmarkDotNet log file.

# --- Configuration ---
LOG_FILE="$1"             # Path to the BenchmarkDotNet log file (passed as argument)
BENCHMARK_NAME="AsyncLockBenchmark.AcquireRelease_Contended"
DEBUG_KEYWORD="AsyncLockMonitor-DEBUG"
ERROR_KEYWORD="AsyncLockMonitor-ERROR"
START_MARKER_REGEX="^// Benchmark: ${BENCHMARK_NAME}"
END_MARKER_REGEX="^// \\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*/"

# --- Script Logic ---

if [ -z "$LOG_FILE" ]; then
  echo "Error: Log file path not provided as the first argument."
  exit 1
fi

if [ ! -f "$LOG_FILE" ]; then
  echo "Error: Log file not found: $LOG_FILE"
  exit 1
fi

start_line=$(grep -nE "$START_MARKER_REGEX" "$LOG_FILE" | head -n 1 | cut -d':' -f1)

if [ -n "$start_line" ]; then
  end_line=$(awk "NR > $start_line && match(\$0, \"$END_MARKER_REGEX\")" "$LOG_FILE" | head -n 1 | cut -d':' -f1)

  echo "## Essential Logs for $BENCHMARK_NAME from $LOG_FILE"
  echo "--- Benchmark Block ---"
  if [ -n "$end_line" ]; then
    sed -n "${start_line},${end_line}p" "$LOG_FILE"
  else
    echo "Warning: Could not find the end marker for $BENCHMARK_NAME. Printing from start marker."
    sed -n "${start_line},$p" "$LOG_FILE"
  fi
  echo ""

  echo "--- Debug and Error Messages ---"
  grep -E "($DEBUG_KEYWORD|$ERROR_KEYWORD)" "$LOG_FILE"
else
  echo "Warning: Could not find the start marker for $BENCHMARK_NAME in $LOG_FILE."
  echo "--- Entire Log (Limited to Debug/Error) ---"
  grep -E "($DEBUG_KEYWORD|$ERROR_KEYWORD)" "$LOG_FILE"
fi

exit 0
