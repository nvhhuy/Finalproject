#!/usr/bin/env bash
# Jenkins agent: delegate to deploy/scripts/harbor-login.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
exec "${ROOT}/deploy/scripts/harbor-login.sh"
