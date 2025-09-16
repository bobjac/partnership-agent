#!/bin/bash

set -e

echo "🔍 Partnership Agent Citation Testing"
echo "===================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${BLUE}Testing citation functionality with various prompts...${NC}"

# Array of test prompts designed to generate good citations
declare -a test_prompts=(
    "What are the revenue sharing percentages for Tier 1, Tier 2, and Tier 3 partners?"
    "How long is the notice period required for voluntary partnership termination?"
    "What is the minimum credit score requirement for partner verification?"
    "What are the specific customer satisfaction score requirements for partners?"
    "How often are compliance reviews and certifications required?"
    "What is the minimum payment threshold for revenue sharing payments?"
    "What encryption standards are required for data protection?"
    "How many hours of ongoing education are required annually for partners?"
    "What is the maximum percentage for bad debt provisions in revenue calculations?"
    "What are the response time targets for partner communications?"
)

# Test each prompt
cd "$PROJECT_ROOT/src/PartnershipAgent.ConsoleApp"

for i in "${!test_prompts[@]}"; do
    prompt_num=$((i + 1))
    echo -e "\n${YELLOW}=== Citation Test ${prompt_num}/10 ===${NC}"
    echo -e "${BLUE}Prompt: ${test_prompts[$i]}${NC}"
    echo -e "${GREEN}Response:${NC}"
    
    # Send prompt to console app
    (
        sleep 1
        echo "${test_prompts[$i]}"
        sleep 15
        echo "quit"
    ) | timeout 30s dotnet run 2>/dev/null || echo -e "${RED}Test timed out${NC}"
    
    echo -e "${BLUE}--- End Test ${prompt_num} ---${NC}"
done

echo -e "\n${GREEN}🎉 Citation testing completed!${NC}"
echo -e "\n${BLUE}Note: Look for 'Citations' section in the responses above${NC}"
echo -e "${BLUE}Each citation should include:${NC}"
echo -e "  • Document ID and title"
echo -e "  • Relevant text excerpt"
echo -e "  • Start/end positions"
echo -e "  • Relevance score"
echo -e "  • Context before/after"