@echo off
REM Cross-platform launcher for Partnership Agent setup
REM Works on Windows Command Prompt and PowerShell

echo 🚀 Partnership Agent Setup Launcher
echo ==================================

REM Check if .NET is available
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ .NET SDK is required but not found
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo 🔧 Running cross-platform setup tool...
cd /d "%~dp0"
dotnet run --project setup.csproj %*