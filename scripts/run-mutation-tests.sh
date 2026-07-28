#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../Watodoo.Api"

dotnet tool restore
dotnet dotnet-stryker
