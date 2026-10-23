#!/bin/bash

# Initialise this template repository as a new module.
# This is destructive: files are edited and renamed in place.
#
# Usage: ./init-repo.sh New.Namespace

NAMESPACE_OLD="RedShirt.Example.Api"
NAMESPACE_NEW="${1}"

rename_project() {
  local directory
  directory="$(dirname "${1}")"
  local name
  name="$(basename "${1}")"
  local csproj
  csproj="${1}/${name}.csproj"

  local nameNew
  nameNew="$(sed "s/${NAMESPACE_OLD}/${NAMESPACE_NEW}/g" <<< "${name}")"
  local csprojNew
  csprojNew="${1}/${nameNew}.csproj"

  mv "${csproj}" "${csprojNew}"
  mv "${1}" "${directory}/${nameNew}"
}

if [ -z "${NAMESPACE_NEW}" ]; then
  echo "No new namespace provided."
  exit 1
fi

# C# namespace: dotted identifiers (letter or underscore, then letters, digits, or underscores).
if ! [[ "${NAMESPACE_NEW}" =~ ^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$ ]]; then
  echo "Namespace must look like a C# namespace (e.g. MyCompany.MyApp)."
  exit 1
fi

if [ "${NAMESPACE_NEW}" = "${NAMESPACE_OLD}" ]; then
  echo "New namespace must differ from ${NAMESPACE_OLD}."
  exit 1
fi

while read -r f; do
  rename_project "${f}"
done <<< "$(find . -type d -name "${NAMESPACE_OLD}*")"

mv "${NAMESPACE_OLD}.slnx" "${NAMESPACE_NEW}.slnx"

while read -r f; do
  [ -z "${f}" ] && continue
  sed -i "s/${NAMESPACE_OLD}/${NAMESPACE_NEW}/g" "${f}"
done <<< "$(find . \( \
  -name '*.cs' -o \
  -name '*.csproj' -o \
  -name '*.md' -o \
  -name '*.mdc' -o \
  -name '*.slnx' -o \
  -name '*.sh' -o \
  -name 'Dockerfile' -o \
  -name 'nswag.json' -o \
  -path './test/local/docker-compose.yaml' -o \
  \( -path './.github/*' -a \( -name '*.yml' -o -name '*.yaml' \) \) \
\))"
