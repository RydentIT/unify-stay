<#
.SYNOPSIS
    Applies pending database migrations from database/sql/ and exits.

.DESCRIPTION
    Startup migration is enabled in Development only. Everywhere else this script is the
    migration step: run it as a deploy job BEFORE the new API version starts taking traffic.

    DbUp journals what it has applied in the schemaversions table, so running this against an
    up-to-date database is a no-op and safe to repeat.

.EXAMPLE
    $env:ConnectionStrings__Unify = "Host=localhost;Database=unify;Username=unify;Password=..."
    ./scripts/migrate.ps1
#>
[CmdletBinding()]
param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $env:ConnectionStrings__Unify -and -not $env:Database__ConnectionString) {
    throw "Set ConnectionStrings__Unify (or Database__ConnectionString) before running."
}

Write-Host "Applying migrations from $repoRoot/database/sql ..."

dotnet run `
    --project (Join-Path $repoRoot "src/backend/Unify.Api/Unify.Api.csproj") `
    --configuration $Configuration `
    -- migrate

if ($LASTEXITCODE -ne 0) {
    throw "Migration failed with exit code $LASTEXITCODE."
}

Write-Host "Migrations complete."
