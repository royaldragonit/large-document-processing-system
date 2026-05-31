#Requires -Version 5.1
param(
    [string]$BaseUrl = "http://localhost:5156"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$SamplePdf = Join-Path $RepoRoot "samples/minimal.pdf"

$failures = @()
$passed = 0

function Write-Step([string]$Message) {
    Write-Host "==> $Message"
}

function Assert-True([bool]$Condition, [string]$Message) {
    if ($Condition) {
        $script:passed++
        Write-Host "    OK: $Message" -ForegroundColor Green
    }
    else {
        $script:failures += $Message
        Write-Host "    FAIL: $Message" -ForegroundColor Red
    }
}

function Get-ArtifactUri([string]$ArtifactUrl) {
    if ($ArtifactUrl -match "^https?://") {
        return $ArtifactUrl
    }

    return "$BaseUrl$ArtifactUrl"
}

function Invoke-PdfUpload([string]$Uri, [string]$FilePath) {
    Add-Type -AssemblyName System.Net.Http

    $handler = New-Object System.Net.Http.HttpClientHandler
    $client = New-Object System.Net.Http.HttpClient($handler)

    try {
        $multipart = New-Object System.Net.Http.MultipartFormDataContent
        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        $fileContent = New-Object System.Net.Http.ByteArrayContent(,$bytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/pdf")
        $multipart.Add($fileContent, "file", [System.IO.Path]::GetFileName($FilePath))

        $response = $client.PostAsync($Uri, $multipart).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Content    = $body
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

Write-Step "Checking API at $BaseUrl"
try {
    $null = Invoke-WebRequest -Uri $BaseUrl -UseBasicParsing -TimeoutSec 5
}
catch {
    Write-Host "Start the API first: dotnet run --project src/Web --no-build" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $SamplePdf)) {
    Write-Host "Sample PDF not found: $SamplePdf" -ForegroundColor Red
    exit 1
}

Write-Step "Uploading $SamplePdf"
try {
    $uploadResponse = Invoke-PdfUpload -Uri "$BaseUrl/api/documents/upload" -FilePath $SamplePdf
}
catch {
    Write-Host "Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Assert-True ($uploadResponse.StatusCode -eq 202) "Upload returned HTTP 202 Accepted"

$upload = $uploadResponse.Content | ConvertFrom-Json
$documentId = $upload.documentId
Assert-True ($null -ne $documentId -and $documentId -ne [Guid]::Empty) "Response contains documentId"

Write-Step "Polling document $documentId until Ready (10s timeout)"
$document = $null
$deadline = (Get-Date).AddSeconds(10)

while ((Get-Date) -lt $deadline) {
    $document = Invoke-RestMethod -Uri "$BaseUrl/api/documents/$documentId" -Method Get
    if ($document.status -eq "Ready") {
        break
    }

    Start-Sleep -Milliseconds 500
    $document = $null
}

Assert-True ($null -ne $document) "Document reached Ready within 10 seconds"
if ($null -ne $document) {
    Assert-True ($document.totalPages -eq 3) "totalPages = 3"
}

Write-Step "Listing pages"
$pages = Invoke-RestMethod -Uri "$BaseUrl/api/documents/$documentId/pages" -Method Get
Assert-True ($pages.Count -eq 3) "Page list contains 3 pages"

Write-Step "Getting page 1"
$page1 = Invoke-RestMethod -Uri "$BaseUrl/api/documents/$documentId/pages/1" -Method Get
Assert-True ($page1.status -eq "Ready") "Page 1 status is Ready"
Assert-True (-not [string]::IsNullOrWhiteSpace($page1.artifactUrl)) "Page 1 has artifactUrl"

Write-Step "Fetching artifact"
$artifactUri = Get-ArtifactUri $page1.artifactUrl
try {
    $artifactResponse = Invoke-WebRequest -Uri $artifactUri -UseBasicParsing
    Assert-True ($artifactResponse.StatusCode -eq 200) "Artifact URL returned HTTP 200"
}
catch {
    Assert-True $false "Artifact URL returned HTTP 200 ($($_.Exception.Message))"
}

Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "SMOKE TEST: PASS ($passed checks)" -ForegroundColor Green
    exit 0
}

Write-Host "SMOKE TEST: FAIL ($($failures.Count) failed, $passed passed)" -ForegroundColor Red
foreach ($failure in $failures) {
    Write-Host "  - $failure" -ForegroundColor Red
}

exit 1
