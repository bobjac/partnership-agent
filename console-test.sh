#!/bin/bash
cd /Users/bobjacobs/work/src/github.com/bobjac/partnership-agent/src/PartnershipAgent.ConsoleApp

echo "Starting console app test..."
echo -e "What are partnership terms?\nquit" | timeout 15 dotnet run