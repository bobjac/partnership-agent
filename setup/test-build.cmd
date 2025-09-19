@echo off
REM Test script to validate build improvements
echo Testing improved setup build process...

echo Checking solution file exists...
if not exist "..\PartnershipAgent.sln" (
    echo ERROR: Solution file not found
    exit /b 1
)

echo Testing build command...
cd ..
dotnet build PartnershipAgent.sln --configuration Release --verbosity minimal
if %errorlevel% neq 0 (
    echo Build failed, this would trigger fallback logic in setup
    exit /b 1
)

echo Build test completed successfully!