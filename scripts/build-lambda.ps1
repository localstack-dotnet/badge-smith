# scripts/build-lambda.ps1
[CmdletBinding(PositionalBinding = $false)]
param(
    [Alias('t')][ValidateSet('zip', 'image', 'both')] [string]$Target = 'zip',
    [Alias('r')] [string]$Rid = 'linux-x64',                      # linux-x64 | linux-arm64
    [Alias('i')] [string]$ImageTag = 'badgesmith-lambda:local',
    [Alias('f')] [string]$Dockerfile = 'src/BadgeSmith.Api/Dockerfile',
    [Alias('c')] [string]$Context = '.',
    [Alias('o')] [string]$OutDir = 'artifacts',
    [switch]$Push,
    [switch]$Clean,
    [Alias('h', 'help')] [switch]$Usage
)
$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Build BadgeSmith Lambda (zip and/or container image) via Docker Buildx.

USAGE:
  scripts\build-lambda.ps1 [-t zip|image|both] [-r linux-x64|linux-arm64]
                           [-i <imageTag>] [-f <Dockerfile>] [-c <context>]
                           [-o <outDir>] [--push] [--clean] [-Verbose] [-h]

OPTIONS:
  -t, --target      zip|image|both           (default: zip)
  -r, --rid         linux-x64|linux-arm64    (default: linux-x64)
  -i, --image-tag   Docker image tag         (default: badgesmith-lambda:local)
  -f, --dockerfile  Path to Dockerfile       (default: src/BadgeSmith.Api/Dockerfile)
  -c, --context     Build context            (default: .)
  -o, --out         Output dir for artifacts (default: artifacts)
      --push        Push image after build
      --clean       Clean output directory before writing
  -Verbose          Show docker commands
  -h, --help        Show this help

EXAMPLES:
  # Zip only (default RID linux-x64)
  .\scripts\build-lambda.ps1 -t zip --clean

  # Zip for ARM64
  .\scripts\build-lambda.ps1 -t zip -r linux-arm64 --clean

  # Container image (don’t push)
  .\scripts\build-lambda.ps1 -t image -i yourrepo/badgesmith:latest

  # Both zip + image, push image
  .\scripts\build-lambda.ps1 -t both -i <acct>.dkr.ecr.eu-central-1.amazonaws.com/badgesmith:latest --push
'@ | Write-Output
}

if ($Usage) { Show-Usage; exit 0 }

function Get-Platform([string]$rid) {
    switch ($rid) {
        'linux-arm64' { 'linux/arm64' }
        default { 'linux/amd64' }
    }
}

function Invoke-Docker([string[]]$DockerArgs) {
    Write-Verbose ("docker " + ($DockerArgs -join ' '))
    & docker @DockerArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker failed ($LASTEXITCODE)" }
}

# prep artifacts dir
if ($Clean -and (Test-Path $OutDir)) { Remove-Item "$OutDir\*" -Recurse -Force }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$platform = Get-Platform $Rid

# ZIP (export-only stage so no symlinks/junk) + platform for cross-arch
if ($Target -in @('zip', 'both')) {
    $zipArgs = @(
        'buildx', 'build',
        '-f', $Dockerfile,
        '--target', 'export-zip',
        '--build-arg', "RID=$Rid",
        '--platform', $platform,
        '--output', "type=local,dest=$OutDir",
        $Context
    )
    Invoke-Docker $zipArgs
}

# IMAGE
if ($Target -in @('image', 'both')) {
    $imgArgs = @(
        'buildx', 'build',
        '-f', $Dockerfile,
        '--target', 'lambda-image',
        '--build-arg', "RID=$Rid",
        '--platform', $platform,
        '-t', $ImageTag
    )
    if ($Push) { $imgArgs += '--push' }
    $imgArgs += $Context
    Invoke-Docker $imgArgs
}

if ($Target -in @('zip', 'both')) {
    $expectedZip = Join-Path $OutDir ("badge-lambda-{0}.zip" -f $Rid)
    if (-not (Test-Path $expectedZip)) {
        throw "ZIP not found: $expectedZip. Re-run with -Verbose to see docker output."
    }
}

Write-Host "`nDone. Artifacts in '$OutDir'." -ForegroundColor Green
