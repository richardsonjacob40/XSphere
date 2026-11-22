#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
dotnet run --project "src/XSphere/XSphere.csproj"
