#!/usr/bin/env pwsh
# Thin wrapper — the single source of truth is perf-baseline.sh (requires Git Bash on Windows).
& bash "$PSScriptRoot/perf-baseline.sh" @args
exit $LASTEXITCODE
