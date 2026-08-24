#!/usr/bin/env bash
#
# Applies pending database migrations from database/sql/ and exits.
#
# Startup migration is enabled in Development only. Everywhere else this script is the
# migration step: run it as a deploy job BEFORE the new API version starts taking traffic.
#
# Usage:
#   ConnectionStrings__Unify="Host=...;Database=...;Username=...;Password=..." ./scripts/migrate.sh
#
# DbUp journals what it has applied in the schemaversions table, so running this against an
# up-to-date database is a no-op and safe to repeat.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "${ConnectionStrings__Unify:-}" && -z "${Database__ConnectionString:-}" ]]; then
  echo "error: set ConnectionStrings__Unify (or Database__ConnectionString) before running." >&2
  exit 1
fi

echo "Applying migrations from ${repo_root}/database/sql ..."

dotnet run \
  --project "${repo_root}/src/backend/Unify.Api/Unify.Api.csproj" \
  --configuration "${DOTNET_CONFIGURATION:-Release}" \
  -- migrate

echo "Migrations complete."
