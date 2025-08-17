#!/bin/bash
# ------------------------------------------------------------------------
set -eux

yell() { echo "$0: $*" >&2; }
die() { yell "$*"; exit 111; }
try() { "$@" || die "cannot $*"; }


#
# Run all commands from the repository root!
# (That's the directory above the current one :)
# ------------------------------------------------------------------------
#
SCRIPT_PATH="${BASH_SOURCE[0]}"
if ([ -h "${SCRIPT_PATH}" ]); then
  while([ -h "${SCRIPT_PATH}" ]); do cd "$(dirname "$SCRIPT_PATH")";
  SCRIPT_PATH=$(readlink "${SCRIPT_PATH}"); done
fi
cd "$(dirname ${SCRIPT_PATH})" > /dev/null
cd ..


#
# Ensure env
# ------------------------------------------------------------------------
if [ -z ${GITHUB_REF+x} ];      then die "GITHUB_REF is not set"; fi
if [ -z ${GITHUB_TOKEN+x} ];    then die "GITHUB_TOKEN is not set"; fi
if [ -z ${NUGET_APIKEY+x} ];    then die "NUGET_APIKEY is not set"; fi

if [[ ${GITHUB_REF} != refs/tags/v* ]]; then die "Script only works for tags"; fi

export VERSION=${GITHUB_REF##*/v}
echo ${VERSION}


#
# Build
# ------------------------------------------------------------------------

dotnet clean   -c Release
dotnet restore --packages .nuget
dotnet build   -c Release --no-restore -p:Version=${VERSION}
dotnet test    -c Release --no-restore --no-build --verbosity=normal


#
# Publish to nuget.org
# ------------------------------------------------------------------------

mkdir -p nupkg
rm -f nupkg/*.*

dotnet pack    -c Release --no-restore --no-build src/Lefty.Tailwind       -o nupkg -p:Version=${VERSION}

dotnet nuget push "nupkg/*.nupkg" --api-key ${NUGET_APIKEY} --source=https://api.nuget.org/v3/index.json


#
# Release
# ------------------------------------------------------------------------

gh release create v${VERSION} --notes="Release v${VERSION}"

# eof