using System;
using PartnershipAgent.Core.Services;

// Simple test to verify ground truth matching
var groundTruthService = new GroundTruthService();

string testPrompt = "What are the key terms of a typical partnership agreement?";
string expectedOutput = groundTruthService.GetExpectedOutput(testPrompt, "FAQAgent");

Console.WriteLine($"Test Prompt: {testPrompt}");
Console.WriteLine($"Expected Output Found: {!string.IsNullOrEmpty(expectedOutput)}");
if (!string.IsNullOrEmpty(expectedOutput))
{
    Console.WriteLine($"Expected Output: {expectedOutput.Substring(0, Math.Min(100, expectedOutput.Length))}...");
}

var allGroundTruth = groundTruthService.GetAllGroundTruth();
Console.WriteLine($"Total Ground Truth Items: {allGroundTruth.Count()}");

foreach (var item in allGroundTruth.Take(3))
{
    Console.WriteLine($"- {item.UserPrompt.Substring(0, Math.Min(50, item.UserPrompt.Length))}... [{item.Module}]");
}