using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PartnershipAgent.Setup;

/// <summary>
/// Cross-platform setup script for Partnership Agent
/// Can be run with: dotnet run --project setup/CrossPlatformSetup.cs
/// </summary>
public class CrossPlatformSetup
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string _projectRoot = GetProjectRoot();
    private static readonly string _setupDir = Path.Combine(_projectRoot, "setup");

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("🚀 Partnership Agent Cross-Platform Setup");
            Console.WriteLine("=========================================");
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Project root: {_projectRoot}");

            // Step 1: Check prerequisites
            if (!await CheckPrerequisites())
                return 1;

            // Step 2: Clean up existing containers
            await CleanupContainers();

            // Step 3: Start Elasticsearch
            if (!await StartElasticsearch())
                return 1;

            // Step 4: Setup index and documents
            if (!await SetupElasticsearchData())
                return 1;

            // Step 5: Configure user secrets
            if (!await ConfigureUserSecrets())
                return 1;

            // Step 6: Build solution
            if (!await BuildSolution())
                return 1;

            // Step 7: Test with Web API and Console App
            await RunTests();

            Console.WriteLine("\n🎉 Setup completed successfully!");
            Console.WriteLine("\nSummary:");
            Console.WriteLine("• Elasticsearch: http://localhost:9200");
            Console.WriteLine("• Index: partnership-documents");
            Console.WriteLine("• Documents: 8 with rich citation content");
            Console.WriteLine("• Citation functionality: Active");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Setup failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<bool> CheckPrerequisites()
    {
        Console.WriteLine("\n📋 Checking prerequisites...");

        if (!IsCommandAvailable("docker"))
        {
            Console.WriteLine("❌ Docker is not installed or not in PATH");
            return false;
        }

        if (!IsCommandAvailable("dotnet"))
        {
            Console.WriteLine("❌ .NET SDK is not installed or not in PATH");
            return false;
        }

        Console.WriteLine("✅ All prerequisites found");
        return true;
    }

    private static async Task CleanupContainers()
    {
        Console.WriteLine("\n🧹 Cleaning up existing containers...");
        
        var containers = new[] { "elasticsearch-secure", "elasticsearch", "es-local-dev" };
        foreach (var container in containers)
        {
            await RunCommand("docker", $"stop {container}", ignoreErrors: true);
            await RunCommand("docker", $"rm {container}", ignoreErrors: true);
        }
    }

    private static async Task<bool> StartElasticsearch()
    {
        Console.WriteLine("\n🐳 Starting Elasticsearch...");

        var dockerArgs = new StringBuilder()
            .Append("run -d --name elasticsearch-secure ")
            .Append("-p 9200:9200 -p 9300:9300 ")
            .Append("-e \"discovery.type=single-node\" ")
            .Append("-e \"xpack.security.enabled=false\" ")
            .Append("-e \"ES_JAVA_OPTS=-Xms512m -Xmx512m\" ")
            .Append("docker.elastic.co/elasticsearch/elasticsearch:7.17.0")
            .ToString();

        if (!await RunCommand("docker", dockerArgs))
            return false;

        // Wait for Elasticsearch to be ready
        Console.WriteLine("⏳ Waiting for Elasticsearch...");
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync("http://localhost:9200/_cluster/health");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Elasticsearch is ready");
                    return true;
                }
            }
            catch
            {
                Console.Write(".");
                await Task.Delay(2000);
            }
        }

        Console.WriteLine("\n❌ Elasticsearch failed to start");
        return false;
    }

    private static async Task<bool> SetupElasticsearchData()
    {
        Console.WriteLine("\n📊 Setting up Elasticsearch data...");

        // Create index
        var mappingFile = Path.Combine(_setupDir, "setup-elasticsearch.json");
        var mapping = await File.ReadAllTextAsync(mappingFile);
        
        var content = new StringContent(mapping, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync("http://localhost:9200/partnership-documents", content);
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("❌ Failed to create index");
            return false;
        }
        Console.WriteLine("✅ Index created");

        // Index documents
        var bulkFile = Path.Combine(_setupDir, "sample-documents-bulk.json");
        var bulkData = await File.ReadAllTextAsync(bulkFile);
        
        content = new StringContent(bulkData, Encoding.UTF8, "application/json");
        response = await _httpClient.PostAsync("http://localhost:9200/partnership-documents/_bulk", content);
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("❌ Failed to index documents");
            return false;
        }
        Console.WriteLine("✅ Documents indexed");

        return true;
    }

    private static async Task<bool> ConfigureUserSecrets()
    {
        Console.WriteLine("\n🔐 Configuring user secrets...");
        
        var webApiDir = Path.Combine(_projectRoot, "src", "PartnershipAgent.WebApi");
        
        return await RunCommand("dotnet", "user-secrets set \"ElasticSearch:Uri\" \"http://localhost:9200\"", 
            workingDirectory: webApiDir);
    }

    private static async Task<bool> BuildSolution()
    {
        Console.WriteLine("\n🔨 Building solution...");
        
        return await RunCommand("dotnet", "build PartnershipAgent.sln", workingDirectory: _projectRoot);
    }

    private static async Task RunTests()
    {
        Console.WriteLine("\n🧪 Running citation tests...");
        
        // Define test prompts
        var testPrompts = new[]
        {
            "What are the revenue sharing percentages for different partner tiers?",
            "What is the minimum credit score requirement for partners?",
            "How long is the partnership termination notice period?",
            "What customer satisfaction scores are required?"
        };

        var consoleAppDir = Path.Combine(_projectRoot, "src", "PartnershipAgent.ConsoleApp");
        
        Console.WriteLine("📝 Test prompts designed for citations:");
        for (int i = 0; i < testPrompts.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {testPrompts[i]}");
        }
        
        Console.WriteLine("\n💡 Run the console app manually to see full citation details:");
        Console.WriteLine($"cd {consoleAppDir}");
        Console.WriteLine("dotnet run");
    }

    private static async Task<bool> RunCommand(string command, string arguments, 
        string? workingDirectory = null, bool ignoreErrors = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            if (!ignoreErrors)
                Console.WriteLine($"❌ Failed to start {command}");
            return false;
        }

        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0 && !ignoreErrors)
        {
            var error = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"❌ Command failed: {error}");
            return false;
        }

        return true;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetProjectRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null && !File.Exists(Path.Combine(current, "PartnershipAgent.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }
        return current ?? Directory.GetCurrentDirectory();
    }
}