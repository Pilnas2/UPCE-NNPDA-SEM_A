# Kept for compatibility; OpenAI is configured through OpenAI__ApiKey only.
param([switch]$WithOpenAI)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/CvApi/CvApi.csproj'
if (-not $env:Jwt__Key) {
    $keyBytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($keyBytes) } finally { $rng.Dispose() }
    $env:Jwt__Key = [Convert]::ToBase64String($keyBytes)
}
if (-not $env:SeedUser__Username) { $env:SeedUser__Username = 'admin' }
if (-not $env:SeedUser__Password) {
    # Development account for the localhost launcher only.
    $env:SeedUser__Password = 'admin'
}
Write-Host 'Swagger: http://localhost:5080/swagger'
Write-Host ('Uzivatel: ' + $env:SeedUser__Username)
dotnet run --project $project --no-launch-profile --urls http://localhost:5080
if ($LASTEXITCODE -ne 0) { throw 'Spusteni API selhalo.' }
