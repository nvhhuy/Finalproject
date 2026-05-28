#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dotnet test "$ROOT/src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj" "$@"
