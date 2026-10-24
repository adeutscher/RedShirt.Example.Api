#!/usr/bin/env bash
#
# Scaffold (or remove) the API controller, CQRS use cases, request models, authorization wiring,
# and test skeletons for a data store resource. This is the recommended entrypoint for
# tools/RedShirt.Example.Api.Scaffolding. See docs/patterns/scaffolding.md.
#
# Usage:
#   scripts/scaffold.sh create <Resource> [options]
#   scripts/scaffold.sh remove <Resource> [options]
#   scripts/scaffold.sh --help
#
# Before running the tool, this script builds the resource's data store project(s) so that
# source-generated service contracts (I<Resource>Service and its request types) are available.
# Data store projects are found the same way the tool finds them:
#   * src/<Base>.DataStores.<Resource>/ (a single, usually source-generated, project), or
#   * src/<Base>.DataStores.<Resource>.Core/ and .Implementation/ (a hand-written pair),
#   * or whatever --data-store and --registration-project point at.
# <Base> is the name of the single .slnx in the repository root.
#
# Script-only options (everything else is passed to the tool):
#   * --no-build: skip building the data store project(s) first.
#
# Exit codes (from the tool):
#   * 0: success.
#   * 1: failure (for example a missing anchor comment). Nothing is changed when create fails its checks.
#   * 2: usage error, or remove needs confirmation but there is no terminal (use -y).
#   * 3: remove was cancelled at the confirmation prompt.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

shopt -s nullglob
solutions=(*.slnx)
shopt -u nullglob
if [[ "${#solutions[@]}" -ne 1 ]]; then
  echo "Expected exactly one .slnx in ${repo_root}, found ${#solutions[@]}." >&2
  exit 1
fi
base_namespace="${solutions[0]%.slnx}"
tool_project="tools/${base_namespace}.Scaffolding/${base_namespace}.Scaffolding.csproj"

build=1
tool_args=()
data_store_projects=()
for ((i = 1; i <= $#; i++)); do
  arg="${!i}"
  case "$arg" in
    --no-build)
      build=0
      continue
      ;;
    --data-store | --registration-project)
      next=$((i + 1))
      if [[ $next -le $# ]]; then
        data_store_projects+=("${!next}")
      fi
      ;;
  esac
  tool_args+=("$arg")
done

if [[ "${#tool_args[@]}" -eq 0 ]]; then
  dotnet run --project "$tool_project" -- --help
  exit 2
fi

mode="${tool_args[0]:-}"
resource="${tool_args[1]:-}"

if [[ $build -eq 1 && ( "$mode" == "create" || "$mode" == "remove" ) && -n "$resource" ]]; then
  if [[ "${#data_store_projects[@]}" -eq 0 ]]; then
    for candidate in \
      "src/${base_namespace}.DataStores.${resource}/${base_namespace}.DataStores.${resource}.csproj" \
      "src/${base_namespace}.DataStores.${resource}.Core/${base_namespace}.DataStores.${resource}.Core.csproj" \
      "src/${base_namespace}.DataStores.${resource}.Implementation/${base_namespace}.DataStores.${resource}.Implementation.csproj"; do
      if [[ -f "$candidate" ]]; then
        data_store_projects+=("$candidate")
      fi
    done
  fi

  for project in "${data_store_projects[@]}"; do
    echo "==> building ${project}"
    if ! build_output="$(dotnet build "$project" -nologo -v quiet 2>&1)"; then
      echo "$build_output" >&2
      echo "ERROR: could not build ${project}." >&2
      exit 1
    fi
  done
fi

exec dotnet run --project "$tool_project" -- "${tool_args[@]}" --repo-root "$repo_root"
