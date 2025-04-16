#!/bin/bash
# scripts/ubuntu/capture_essential_benchmark_logs.sh

# This script extracts essential log information for a specific benchmark
# from a BenchmarkDotNet log file or standard input.

# --- Configuration ---
LOG_FILE="$1"
BENCHMARK_NAME="AsyncLockBenchmark.AcquireRelease_Contended"
DEBUG_KEYWORD="AsyncLockMonitor-DEBUG"
ERROR_KEYWORD="AsyncLockMonitor-ERROR"
START_MARKER_REGEX="^// Benchmark: ${BENCHMARK_NAME}"
END_MARKER_REGEX="^// \\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*\\*/"

# --- Script Logic ---

if [ -z "$LOG_FILE" ] || [ "$LOG_FILE" = "-" ]; then
  # Read from standard input
  INPUT_SOURCE="stdin"
  LOG_CONTENT=$(cat -)
else
  # Read from the specified file
  INPUT_SOURCE="file: $LOG_FILE"
  if [ ! -f "$LOG_FILE" ]; then
    echo "Error: Log file not found: $LOG_FILE"
    exit 1
  fi
  LOG_CONTENT=$(cat "$LOG_FILE")
fi

echo "Processing logs from: $INPUT_SOURCE"

start_line=$(echo "$LOG_CONTENT" | grep -nE "$START_MARKER_REGEX" | head -n 1 | cut -d':' -f1)

if [ -n "$start_line" ]; then
  end_line=$(echo "$LOG_CONTENT" | awk "NR > $start_line && match(\$0, \"$END_MARKER_REGEX\")" | head -n 1 | cut -d':' -f1)

  echo "## Essential Logs for $BENCHMARK_NAME"
  echo "--- Benchmark Block ---"
  if [ -n "$end_line" ]; then
    echo "$LOG_CONTENT" | sed -n "${start_line},${end_line}p"
  else
    echo "Warning: Could not find the end marker for $BENCHMARK_NAME. Printing from start marker."
    echo "$LOG_CONTENT" | sed -n "${start_line},$p"
  fi
  echo ""

  echo "--- Debug and Error Messages ---"
  echo "$LOG_CONTENT" | grep -E "($DEBUG_KEYWORD|$ERROR_KEYWORD)"
else
  echo "Warning: Could not find the start marker for $BENCHMARK_NAME."
  echo "--- Entire Log (Limited to Debug/Error) ---"
  echo "$LOG_CONTENT" | grep -E "($DEBUG_KEYWORD|$ERROR_KEYWORD)"
fi

exit 0
