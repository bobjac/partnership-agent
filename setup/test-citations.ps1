# Partnership Agent Citation Testing Script for Windows
# ====================================================

Write-Host "🔍 Partnership Agent Citation Testing" -ForegroundColor Blue
Write-Host "====================================" -ForegroundColor Blue

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "Testing citation functionality with various prompts..." -ForegroundColor Cyan

# Array of test prompts designed to generate good citations
$testPrompts = @(
    "What are the revenue sharing percentages for Tier 1, Tier 2, and Tier 3 partners?",
    "How long is the notice period required for voluntary partnership termination?",
    "What is the minimum credit score requirement for partner verification?",
    "What are the specific customer satisfaction score requirements for partners?",
    "How often are compliance reviews and certifications required?",
    "What is the minimum payment threshold for revenue sharing payments?",
    "What encryption standards are required for data protection?",
    "How many hours of ongoing education are required annually for partners?",
    "What is the maximum percentage for bad debt provisions in revenue calculations?",
    "What are the response time targets for partner communications?"
)

# Test each prompt
Push-Location "$ProjectRoot\src\PartnershipAgent.ConsoleApp"

try {
    for ($i = 0; $i -lt $testPrompts.Length; $i++) {
        $promptNum = $i + 1
        Write-Host "`n=== Citation Test $promptNum/10 ===" -ForegroundColor Yellow
        Write-Host "Prompt: $($testPrompts[$i])" -ForegroundColor Cyan
        Write-Host "Response:" -ForegroundColor Green
        
        # Send prompt to console app
        try {
            $testInput = "$($testPrompts[$i])`nquit`n"
            $result = $testInput | dotnet run --no-build 2>$null
            if ($result) {
                Write-Host $result
            }
        }
        catch {
            Write-Host "Test timed out" -ForegroundColor Red
        }
        
        Write-Host "--- End Test $promptNum ---" -ForegroundColor Cyan
        Start-Sleep -Seconds 1
    }
}
finally {
    Pop-Location
}

Write-Host "`n🎉 Citation testing completed!" -ForegroundColor Green
Write-Host "`nNote: Look for 'Citations' section in the responses above" -ForegroundColor Cyan
Write-Host "Each citation should include:" -ForegroundColor Cyan
Write-Host "  • Document ID and title" -ForegroundColor White
Write-Host "  • Relevant text excerpt" -ForegroundColor White
Write-Host "  • Start/end positions" -ForegroundColor White
Write-Host "  • Relevance score" -ForegroundColor White
Write-Host "  • Context before/after" -ForegroundColor White