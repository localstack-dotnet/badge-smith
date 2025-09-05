# scripts/test-ingestion.ps1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$BaseUrl,

    [Parameter(Mandatory=$true)]
    [string]$Owner,

    [Parameter(Mandatory=$true)]
    [string]$Repo,

    [Parameter(Mandatory=$true)]
    [string]$Platform,

    [Parameter(Mandatory=$true)]
    [string]$Branch,

    [Parameter(Mandatory=$true)]
    [string]$Secret,

    [Parameter(Mandatory=$false)]
    [string]$PayloadFile = "",

    [Parameter(Mandatory=$false)]
    [string]$Payload = "",

    [switch]$ShowDetails
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Test BadgeSmith HMAC authentication and test result ingestion.

USAGE:
  scripts\test-ingestion.ps1 -BaseUrl <url> -Owner <owner> -Repo <repo>
                             -Platform <platform> -Branch <branch> -Secret <secret>
                             [-PayloadFile <file>] [-Payload <json>] [-ShowDetails]

EXAMPLES:
  # Using payload file
  .\scripts\test-ingestion.ps1 -BaseUrl "http://localhost:9474" `
    -Owner "localstack-dotnet" -Repo "localstack.client" `
    -Platform "linux" -Branch "main" -Secret "your-hmac-secret" `
    -PayloadFile "test-payload.json"

  # Using inline payload
  .\scripts\test-ingestion.ps1 -BaseUrl "http://localhost:9474" `
    -Owner "localstack-dotnet" -Repo "localstack.client" `
    -Platform "linux" -Branch "main" -Secret "your-hmac-secret" `
    -Payload '{"platform":"Linux","passed":190,"failed":0,...}'

PAYLOAD FORMAT:
  {
    "platform": "Linux",
    "passed": 190,
    "failed": 0,
    "skipped": 0,
    "total": 190,
    "url_html": "https://github.com/owner/repo/runs/123",
    "timestamp": "2025-09-05T10:57:00Z",
    "commit": "4d8474bda0b16fbbb69887d0d08c3885843bbdc7",
    "run_id": "16814735762",
    "workflow_run_url": "https://github.com/owner/repo/actions/runs/456"
  }
'@ | Write-Output
}

# Help is handled by CmdletBinding() automatically

# Validate inputs
if ([string]::IsNullOrWhiteSpace($PayloadFile) -and [string]::IsNullOrWhiteSpace($Payload)) {
    Write-Error "Either -PayloadFile or -Payload must be provided"
    Show-Usage
    exit 1
}

if (![string]::IsNullOrWhiteSpace($PayloadFile) -and ![string]::IsNullOrWhiteSpace($Payload)) {
    Write-Error "Cannot specify both -PayloadFile and -Payload"
    Show-Usage
    exit 1
}

# Load payload
$payloadJson = if ($PayloadFile) {
    if (!(Test-Path $PayloadFile)) {
        Write-Error "Payload file not found: $PayloadFile"
        exit 1
    }
    Get-Content $PayloadFile -Raw
} else {
    $Payload
}

# Normalize parameters
$Owner = $Owner.ToLowerInvariant()
$Repo = $Repo.ToLowerInvariant()
$Platform = $Platform.ToLowerInvariant()
$Branch = $Branch.ToLowerInvariant()

# Generate authentication headers
$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$nonce = [Guid]::NewGuid().ToString("N")

# Compute HMAC-SHA256 signature
$hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($Secret))
$hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($payloadJson))
$signature = "sha256=" + [BitConverter]::ToString($hash).Replace("-", "").ToLowerInvariant()
$hmac.Dispose()

# Build request URL
$url = "$BaseUrl/tests/results/$Platform/$Owner/$Repo/$Branch"

# Prepare headers
$headers = @{
    'Content-Type' = 'application/json'
    'X-Signature' = $signature
    'X-Timestamp' = $timestamp
    'X-Nonce' = $nonce
}

if ($ShowDetails) {
    Write-Host "🚀 Sending test result ingestion request" -ForegroundColor Green
    Write-Host "URL: $url" -ForegroundColor Cyan
    Write-Host "Headers:" -ForegroundColor Cyan
    $headers.GetEnumerator() | ForEach-Object { Write-Host "  $($_.Key): $($_.Value)" -ForegroundColor Gray }
    Write-Host "Payload:" -ForegroundColor Cyan
    Write-Host $payloadJson -ForegroundColor Gray
    Write-Host ""
}

try {
    # Send request
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $payloadJson -ContentType 'application/json'

    Write-Host "✅ Request successful!" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor Gray
}
catch {
    Write-Host "❌ Request failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode
        Write-Host "Status Code: $statusCode" -ForegroundColor Red

        try {
            $errorBody = $_.Exception.Response.GetResponseStream()
            $reader = [System.IO.StreamReader]::new($errorBody)
            $errorContent = $reader.ReadToEnd()
            Write-Host "Response Body:" -ForegroundColor Red
            Write-Host $errorContent -ForegroundColor Gray
        } catch {
            Write-Host "Could not read error response body" -ForegroundColor Red
        }
    }

    exit 1
}
