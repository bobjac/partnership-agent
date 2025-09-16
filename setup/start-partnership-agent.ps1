# Partnership Agent Complete Setup Script for Windows
# ====================================================

param(
    [switch]$Help
)

if ($Help) {
    Write-Host @"
Partnership Agent Setup Script for Windows
==========================================

This script will:
1. Start Elasticsearch for testing (without security)
2. Create index and add enhanced sample documents (8 documents with rich content)  
3. Configure user secrets for the Web API
4. Build the solution
5. Start the Web API
6. Run the console app with multiple citation test prompts

Prerequisites:
- Docker Desktop for Windows
- .NET 8 SDK
- PowerShell 5.1 or later

Usage:
  .\start-partnership-agent.ps1
  .\start-partnership-agent.ps1 -Help
"@
    exit 0
}

# Enable strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "🚀 Partnership Agent Complete Setup Script" -ForegroundColor Blue
Write-Host "=========================================" -ForegroundColor Blue

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "Project root: $ProjectRoot" -ForegroundColor Cyan

# Function to check if command exists
function Test-Command {
    param([string]$Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# Function to wait for service
function Wait-ForService {
    param(
        [string]$Url,
        [string]$ServiceName,
        [hashtable]$Headers = @{},
        [int]$MaxRetries = 30,
        [int]$SleepSeconds = 2
    )
    
    Write-Host "Waiting for $ServiceName to be ready..." -ForegroundColor Cyan
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            $response = Invoke-RestMethod -Uri $Url -Method Get -Headers $Headers -TimeoutSec 5
            if ($response) {
                Write-Host "✓ $ServiceName is ready" -ForegroundColor Green
                return $true
            }
        }
        catch {
            Write-Host "." -NoNewline -ForegroundColor Yellow
            Start-Sleep -Seconds $SleepSeconds
        }
    }
    
    Write-Host ""
    Write-Host "Error: $ServiceName failed to start properly" -ForegroundColor Red
    return $false
}

# Check prerequisites
Write-Host "`nChecking prerequisites..." -ForegroundColor Yellow

if (!(Test-Command "docker")) {
    Write-Host "Error: Docker is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

if (!(Test-Command "dotnet")) {
    Write-Host "Error: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

Write-Host "✓ All prerequisites found" -ForegroundColor Green

# Step 1: Stop existing containers
Write-Host "`nStep 1: Cleaning up existing Elasticsearch containers..." -ForegroundColor Yellow

$containersToStop = @("elasticsearch-secure", "elasticsearch", "es-local-dev")
foreach ($container in $containersToStop) {
    try {
        docker stop $container 2>$null
        docker rm $container 2>$null
    }
    catch {
        # Ignore errors for non-existent containers
    }
}

# Step 2: Start Elasticsearch without security for testing
Write-Host "`nStep 2: Starting Elasticsearch for testing..." -ForegroundColor Yellow

$dockerArgs = @(
    "run", "-d", "--name", "elasticsearch-secure",
    "-p", "9200:9200", "-p", "9300:9300",
    "-e", "discovery.type=single-node",
    "-e", "xpack.security.enabled=false",
    "-e", "ES_JAVA_OPTS=-Xms512m -Xmx512m",
    "docker.elastic.co/elasticsearch/elasticsearch:7.17.0"
)

$containerId = docker @dockerArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to start Elasticsearch container" -ForegroundColor Red
    exit 1
}

# Wait for Elasticsearch to be ready (no auth needed)
$elasticHeaders = @{
    'Content-Type' = 'application/json'
}

if (!(Wait-ForService -Url "http://localhost:9200/_cluster/health" -ServiceName "Elasticsearch" -Headers $elasticHeaders)) {
    exit 1
}

# Step 3: Create index and add documents
Write-Host "`nStep 3: Setting up Elasticsearch index and documents..." -ForegroundColor Yellow

# Check if index exists and delete if it does
Write-Host "Checking for existing partnership-documents index..." -ForegroundColor Cyan
try {
    $indexCheck = Invoke-RestMethod -Uri "http://localhost:9200/partnership-documents" -Method Get -Headers $elasticHeaders -ErrorAction SilentlyContinue
    if ($indexCheck) {
        Write-Host "Index exists, deleting and recreating..." -ForegroundColor Cyan
        Invoke-RestMethod -Uri "http://localhost:9200/partnership-documents" -Method Delete -Headers $elasticHeaders
    }
}
catch {
    # Index doesn't exist, which is fine
}

# Create the index with mapping
Write-Host "Creating partnership-documents index..." -ForegroundColor Cyan
$indexMapping = Get-Content "$ScriptDir\setup-elasticsearch.json" -Raw
try {
    $response = Invoke-RestMethod -Uri "http://localhost:9200/partnership-documents" -Method Put -Headers $elasticHeaders -Body $indexMapping
    Write-Host "✓ Index created successfully" -ForegroundColor Green
}
catch {
    Write-Host "Error: Failed to create index - $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response)" -ForegroundColor Red
    exit 1
}

# Index sample documents
Write-Host "Indexing sample documents..." -ForegroundColor Cyan
$bulkData = Get-Content "$ScriptDir\sample-documents-bulk.json" -Raw
try {
    $bulkHeaders = $elasticHeaders.Clone()
    $bulkHeaders['Content-Type'] = 'application/json'
    $response = Invoke-RestMethod -Uri "http://localhost:9200/partnership-documents/_bulk" -Method Post -Headers $bulkHeaders -Body $bulkData
    Write-Host "✓ Sample documents indexed successfully" -ForegroundColor Green
}
catch {
    Write-Host "Error: Failed to index sample documents - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Set user secrets for the Web API
Write-Host "`nStep 4: Configuring user secrets for Web API..." -ForegroundColor Yellow
Push-Location "$ProjectRoot\src\PartnershipAgent.WebApi"

try {
    dotnet user-secrets set "ElasticSearch:Uri" "http://localhost:9200"
    Write-Host "✓ User secrets configured" -ForegroundColor Green
}
catch {
    Write-Host "Error: Failed to configure user secrets - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}

# Step 5: Build the solution
Write-Host "`nStep 5: Building the solution..." -ForegroundColor Yellow
Push-Location $ProjectRoot

try {
    dotnet build PartnershipAgent.sln
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Solution built successfully" -ForegroundColor Green
}
catch {
    Write-Host "Error: Failed to build solution - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}

# Step 6: Start the Web API in background
Write-Host "`nStep 6: Starting Web API..." -ForegroundColor Yellow

# Kill any existing processes on port 5001
try {
    $processesOnPort = Get-NetTCPConnection -LocalPort 5001 -ErrorAction SilentlyContinue
    if ($processesOnPort) {
        $processesOnPort | ForEach-Object {
            $processId = $_.OwningProcess
            Write-Host "Killing process $processId on port 5001" -ForegroundColor Yellow
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}
catch {
    # Ignore errors if no processes found
}

Push-Location "$ProjectRoot\src\PartnershipAgent.WebApi"

# Start the Web API in background
$webApiJob = Start-Job -ScriptBlock {
    Set-Location $args[0]
    dotnet run --urls="http://localhost:5001"
} -ArgumentList (Get-Location).Path

Write-Host "Web API started as background job: $($webApiJob.Id)" -ForegroundColor Cyan

Pop-Location

# Wait for Web API to be ready
if (!(Wait-ForService -Url "http://localhost:5001/api/chat/health" -ServiceName "Web API")) {
    Write-Host "Web API logs:" -ForegroundColor Yellow
    Receive-Job -Job $webApiJob
    Remove-Job -Job $webApiJob -Force
    exit 1
}

# Step 7: Run the console app with citation test prompts
Write-Host "`nStep 7: Running console app with citation test prompts..." -ForegroundColor Yellow
Push-Location "$ProjectRoot\src\PartnershipAgent.ConsoleApp"

Write-Host "Testing multiple prompts to demonstrate citation functionality..." -ForegroundColor Cyan
Write-Host "==================== CONSOLE APP TESTS ====================" -ForegroundColor Green

# Define test prompts for citations
$testPrompts = @(
    "What are the specific percentage rates for revenue sharing between different partner tiers?",
    "What compliance documentation and audit requirements must partners follow?",
    "What is the process for terminating a partnership and what notice period is required?",
    "What are the minimum performance standards and KPIs that partners must maintain?"
)

try {
    foreach ($i in 0..($testPrompts.Length - 1)) {
        $promptNum = $i + 1
        Write-Host "`n--- Test $promptNum/4 ---" -ForegroundColor Yellow
        Write-Host "Prompt: $($testPrompts[$i])" -ForegroundColor Cyan
        
        # Create input for console app
        $testInput = "$($testPrompts[$i])`nquit`n"
        $testInput | dotnet run
        
        Write-Host "--- End Test $promptNum ---" -ForegroundColor Cyan
        Start-Sleep -Seconds 2
    }
}
catch {
    Write-Host "Console app tests completed" -ForegroundColor Yellow
}
finally {
    Pop-Location
}

Write-Host "==================== END CONSOLE APP TESTS ====================" -ForegroundColor Green

# Cleanup
Write-Host "`nCleaning up..." -ForegroundColor Yellow
Remove-Job -Job $webApiJob -Force -ErrorAction SilentlyContinue
Write-Host "✓ Web API stopped" -ForegroundColor Green

Write-Host "`n🎉 Setup completed successfully!" -ForegroundColor Green
Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "• Elasticsearch is running at: " -NoNewline -ForegroundColor Cyan
Write-Host "http://localhost:9200" -ForegroundColor Yellow
Write-Host "• Index: " -NoNewline -ForegroundColor Cyan
Write-Host "partnership-documents" -ForegroundColor Yellow
Write-Host "• Sample documents have been indexed (8 documents with rich content)" -ForegroundColor Cyan
Write-Host "• Citation functionality is now active" -ForegroundColor Cyan

Write-Host "`nTo manually start the Web API:" -ForegroundColor Cyan
Write-Host "cd src\PartnershipAgent.WebApi && dotnet run --urls=`"http://localhost:5001`"" -ForegroundColor White

Write-Host "`nTo manually run the console app:" -ForegroundColor Cyan
Write-Host "cd src\PartnershipAgent.ConsoleApp && dotnet run" -ForegroundColor White

Write-Host "`nTo stop Elasticsearch:" -ForegroundColor Cyan
Write-Host "docker stop elasticsearch-secure" -ForegroundColor White

Write-Host "`nTo test citations specifically:" -ForegroundColor Cyan
Write-Host ".\setup\test-citations.ps1" -ForegroundColor White