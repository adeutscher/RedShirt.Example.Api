#!/usr/bin/env bash
#
# Scan stage: secret detection and NuGet vulnerability audit for the solution.
#
# No registry credentials. Safe on untrusted pull requests when only gitleaks and
# dotnet list package run against the checkout.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

# Use SLN_FILE when set. Otherwise require exactly one root .sln or .slnx.
resolve_sln_file() {
  if [[ -n "${SLN_FILE:-}" ]]; then
    printf '%s\n' "$SLN_FILE"
    return
  fi

  local matches=()
  shopt -s nullglob
  matches=( *.sln *.slnx )
  shopt -u nullglob

  if [[ ${#matches[@]} -eq 0 ]]; then
    echo "ERROR: no .sln or .slnx file in the repository root. Set SLN_FILE." >&2
    exit 1
  fi
  if [[ ${#matches[@]} -gt 1 ]]; then
    echo "ERROR: multiple solution files in the repository root. Set SLN_FILE." >&2
    printf '  %s\n' "${matches[@]}" >&2
    exit 1
  fi
  printf '%s\n' "${matches[0]}"
}

sln="$(resolve_sln_file)" || exit 1
report_dir="artifacts/scan"
mkdir -p "$report_dir"

if command -v gitleaks >/dev/null 2>&1; then
  echo "==> gitleaks: secret detection"
  gitleaks detect \
    --source="." \
    --report-format=json \
    --report-path="$report_dir/gitleaks.json" \
    --verbose \
    --redact
else
  echo "ERROR: gitleaks not on PATH (install on the runner to enable secret scanning)." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet not on PATH." >&2
  exit 1
fi

echo "==> dotnet list package: vulnerable dependencies"
dotnet list "$sln" package --vulnerable --include-transitive \
  | tee "$report_dir/dotnet-vulnerable-packages.txt"

echo "==> scan clean, reports in $report_dir"
