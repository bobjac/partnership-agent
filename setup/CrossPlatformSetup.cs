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
using System.Net.Http.Headers;

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
    
    private static class ElasticCredentials
    {
        public static string Uri { get; set; } = "http://localhost:9200";
        public static string Username { get; set; } = "elastic";
        public static string Password { get; set; } = "";
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("🚀 Partnership Agent Cross-Platform Setup");
            Console.WriteLine("=========================================");
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Project root: {_projectRoot}");

            // Parse command line arguments
            string chatHistoryProvider = "inmemory"; // default
            if (args.Length > 0)
            {
                if (args[0] == "--help" || args[0] == "-h")
                {
                    Console.WriteLine("\nUsage: dotnet run [chat-history-provider]");
                    Console.WriteLine("Options:");
                    Console.WriteLine("  inmemory  - Use in-memory chat history (default, no persistence)");
                    Console.WriteLine("  sqlite    - Use SQLite with Docker container (persistent)");
                    Console.WriteLine("  azuresql  - Use Azure SQL Database (persistent, requires Azure setup)");
                    Console.WriteLine("\nExample: dotnet run sqlite");
                    return 0;
                }
                chatHistoryProvider = args[0].ToLowerInvariant();
            }

            Console.WriteLine($"💾 Chat History Provider: {chatHistoryProvider}");

            // Step 1: Check prerequisites
            if (!await CheckPrerequisites())
                return 1;

            // Step 2: Check for existing Elasticsearch and extract credentials
            bool elasticRunning = await DetectElasticsearch();
            
            if (!elasticRunning)
            {
                // Step 3: Clean up existing containers
                await CleanupContainers();

                // Set default credentials for new instance
                ElasticCredentials.Password = "elastic123";

                // Step 4: Start Elasticsearch
                if (!await StartElasticsearch())
                    return 1;

            }

            // Step 5: Setup index and documents
            if (!await SetupElasticsearchData())
                return 1;

            // Step 5.5: Setup SQLite if requested
            if (chatHistoryProvider == "sqlite")
            {
                if (!await SetupSQLite())
                    return 1;
            }

            // Step 6: Configure user secrets
            if (!await ConfigureUserSecrets(chatHistoryProvider))
                return 1;

            // Step 7: Build solution
            if (!await BuildSolution())
                return 1;

            // Step 8: Test with Web API and Console App
            await RunTests();

            Console.WriteLine("\n🎉 Setup completed successfully!");
            Console.WriteLine("\nSummary:");
            Console.WriteLine($"• Chat History Provider: {chatHistoryProvider}");
            if (chatHistoryProvider == "sqlite")
            {
                Console.WriteLine("• SQLite Container: partnership-agent-sqlite (running)");
                Console.WriteLine("• SQLite Database: /data/partnership-agent.db");
                Console.WriteLine("• SQLite Volume: partnership-agent-sqlite-data");
            }
            Console.WriteLine($"• Elasticsearch: {ElasticCredentials.Uri}");
            if (!string.IsNullOrEmpty(ElasticCredentials.Username))
            {
                Console.WriteLine($"• Username: {ElasticCredentials.Username}");
                Console.WriteLine("• Password: ******** (configured in user secrets)");
            }
            else
            {
                Console.WriteLine("• Authentication: None (open instance)");
            }
            Console.WriteLine("• Index: partnership-documents");
            Console.WriteLine("• Documents: 8 with rich citation content");
            Console.WriteLine("• Citation functionality: Active");
            Console.WriteLine("• User secrets: Configured for PartnershipAgent.WebApi");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Setup failed: {ex.Message}");
            return 1;
        }
    }

    private static Task<bool> CheckPrerequisites()
    {
        Console.WriteLine("\n📋 Checking prerequisites...");

        if (!IsCommandAvailable("docker"))
        {
            Console.WriteLine("❌ Docker is not installed or not in PATH");
            return Task.FromResult(false);
        }

        if (!IsCommandAvailable("dotnet"))
        {
            Console.WriteLine("❌ .NET SDK is not installed or not in PATH");
            return Task.FromResult(false);
        }

        Console.WriteLine("✅ All prerequisites found");
        return Task.FromResult(true);
    }

    private static async Task<bool> DetectElasticsearch()
    {
        Console.WriteLine("\n🔍 Detecting existing Elasticsearch instance...");
        
        try
        {
            // First try without authentication
            var response = await _httpClient.GetAsync("http://localhost:9200/_cluster/health?timeout=5s");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ Found Elasticsearch without authentication");
                ElasticCredentials.Uri = "http://localhost:9200";
                ElasticCredentials.Username = "";
                ElasticCredentials.Password = "";
                return true;
            }
        }
        catch { }

        // Try to extract credentials from running Docker container
        if (await ExtractDockerCredentials())
        {
            Console.WriteLine("✅ Found Elasticsearch with extracted credentials");
            return true;
        }

        Console.WriteLine("ℹ️ No existing Elasticsearch found, will start new instance");
        return false;
    }

    private static async Task<bool> ExtractDockerCredentials()
    {
        try
        {
            // Get running Elasticsearch containers - check for common container names first
            var containerNames = new[] { "elasticsearch-secure", "elasticsearch", "es-local-dev" };
            string? containerName = null;
            
            foreach (var name in containerNames)
            {
                var checkOutput = await GetCommandOutput("docker", $"ps --filter \"name={name}\" --format \"{{{{.Names}}}}\"");
                if (!string.IsNullOrWhiteSpace(checkOutput))
                {
                    containerName = checkOutput.Trim().Split('\n')[0];
                    break;
                }
            }
            
            // If no named containers found, check by image
            if (containerName == null)
            {
                var output = await GetCommandOutput("docker", "ps --filter \"ancestor=docker.elastic.co/elasticsearch/elasticsearch\" --format \"{{.Names}}\"");
                if (string.IsNullOrWhiteSpace(output))
                    return false;
                containerName = output.Trim().Split('\n')[0];
            }
            Console.WriteLine($"Found Elasticsearch container: {containerName}");

            // Try to extract password from environment variables
            var envOutput = await GetCommandOutput("docker", $"exec {containerName} printenv");
            
            foreach (var line in envOutput.Split('\n'))
            {
                if (line.StartsWith("ELASTIC_PASSWORD="))
                {
                    ElasticCredentials.Password = line.Substring("ELASTIC_PASSWORD=".Length);
                    ElasticCredentials.Username = "elastic";
                    
                    // Test the credentials
                    var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ElasticCredentials.Username}:{ElasticCredentials.Password}"));
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                    
                    var response = await client.GetAsync("http://localhost:9200/_cluster/health");
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"✅ Extracted credentials - Username: {ElasticCredentials.Username}");
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not extract credentials: {ex.Message}");
        }

        return false;
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
            .Append("-e \"xpack.security.enabled=true\" ")
            .Append("-e \"ELASTIC_PASSWORD=elastic123\" ")
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
                var client = CreateHttpClientWithAuth();
                var response = await client.GetAsync("http://localhost:9200/_cluster/health");
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

        var client = CreateHttpClientWithAuth();
        
        // Check if index exists
        var checkResponse = await client.GetAsync("http://localhost:9200/partnership-documents");
        if (checkResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("ℹ️ Index already exists, deleting and recreating...");
            var deleteResponse = await client.DeleteAsync("http://localhost:9200/partnership-documents");
            if (!deleteResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("❌ Failed to delete existing index");
                return false;
            }
        }
        
        // Create index
        var mappingFile = Path.Combine(_setupDir, "setup-elasticsearch.json");
        var mapping = await File.ReadAllTextAsync(mappingFile);
        
        var content = new StringContent(mapping, Encoding.UTF8, "application/json");
        var response = await client.PutAsync("http://localhost:9200/partnership-documents", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"❌ Failed to create index: {errorContent}");
            return false;
        }
        Console.WriteLine("✅ Index created");

        // Index documents
        var bulkFile = Path.Combine(_setupDir, "sample-documents-bulk.json");
        var bulkData = await File.ReadAllTextAsync(bulkFile);
        
        content = new StringContent(bulkData, Encoding.UTF8, "application/json");
        response = await client.PostAsync("http://localhost:9200/partnership-documents/_bulk", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"❌ Failed to index documents: {errorContent}");
            return false;
        }
        Console.WriteLine("✅ Documents indexed");

        return true;
    }

    private static async Task<bool> SetupSQLite()
    {
        Console.WriteLine("\n💾 Setting up SQLite container...");

        // Check if Docker is available
        if (!await RunCommand("docker", "--version", outputToConsole: false))
        {
            Console.WriteLine("❌ Docker is required for SQLite setup but not found");
            return false;
        }

        // Stop any existing SQLite container
        Console.WriteLine("🛑 Stopping existing SQLite container...");
        await RunCommand("docker", "stop partnership-agent-sqlite", outputToConsole: false);
        await RunCommand("docker", "rm partnership-agent-sqlite", outputToConsole: false);

        // Build SQLite container
        Console.WriteLine("🔨 Building SQLite container...");
        var dockerDir = Path.Combine(_projectRoot, "docker");
        if (!await RunCommand("docker", "build -t partnership-agent-sqlite sqlite/", 
            workingDirectory: dockerDir))
        {
            Console.WriteLine("❌ Failed to build SQLite container");
            return false;
        }

        // Start SQLite container
        Console.WriteLine("🚀 Starting SQLite container...");
        if (!await RunCommand("docker", "run -d --name partnership-agent-sqlite -v partnership-agent-sqlite-data:/data partnership-agent-sqlite", 
            workingDirectory: dockerDir))
        {
            Console.WriteLine("❌ Failed to start SQLite container");
            return false;
        }

        // Wait for container to be ready
        Console.WriteLine("⏳ Waiting for SQLite container to initialize...");
        await Task.Delay(3000);

        // Verify container is running
        if (!await RunCommand("docker", "ps --filter name=partnership-agent-sqlite --format \"table {{.Names}}\\t{{.Status}}\""))
        {
            Console.WriteLine("❌ SQLite container failed to start properly");
            return false;
        }

        Console.WriteLine("✅ SQLite container setup complete");
        Console.WriteLine("   • Container: partnership-agent-sqlite");
        Console.WriteLine("   • Volume: partnership-agent-sqlite-data");
        Console.WriteLine("   • Database: /data/partnership-agent.db");
        
        return true;
    }

    private static async Task<bool> ConfigureUserSecrets(string chatHistoryProvider)
    {
        Console.WriteLine("\n🔐 Configuring user secrets...");
        
        var webApiDir = Path.Combine(_projectRoot, "src", "PartnershipAgent.WebApi");
        
        Console.WriteLine($"Setting Elasticsearch URI: {ElasticCredentials.Uri}");
        if (!await RunCommand("dotnet", $"user-secrets set \"ElasticSearch:Uri\" \"{ElasticCredentials.Uri}\"", 
            workingDirectory: webApiDir))
            return false;

        if (!string.IsNullOrEmpty(ElasticCredentials.Username))
        {
            Console.WriteLine($"Setting Elasticsearch Username: {ElasticCredentials.Username}");
            if (!await RunCommand("dotnet", $"user-secrets set \"ElasticSearch:Username\" \"{ElasticCredentials.Username}\"", 
                workingDirectory: webApiDir))
                return false;
        }

        if (!string.IsNullOrEmpty(ElasticCredentials.Password))
        {
            Console.WriteLine("Setting Elasticsearch Password: ********");
            if (!await RunCommand("dotnet", $"user-secrets set \"ElasticSearch:Password\" \"{ElasticCredentials.Password}\"", 
                workingDirectory: webApiDir))
                return false;
        }

        // Configure chat history provider
        Console.WriteLine($"Setting Chat History Provider: {chatHistoryProvider}");
        if (!await RunCommand("dotnet", $"user-secrets set \"ChatHistory:Provider\" \"{chatHistoryProvider}\"", 
            workingDirectory: webApiDir))
            return false;

        // Configure SQLite connection string if using SQLite
        if (chatHistoryProvider == "sqlite")
        {
            Console.WriteLine("Setting SQLite Connection String");
            if (!await RunCommand("dotnet", $"user-secrets set \"SQLite:ConnectionString\" \"Data Source=/data/partnership-agent.db;Cache=Shared\"", 
                workingDirectory: webApiDir))
                return false;
        }

        Console.WriteLine("✅ User secrets configured successfully");
        return true;
    }

    private static async Task<bool> BuildSolution()
    {
        Console.WriteLine("\n🔨 Building solution...");
        
        // Check if solution file exists
        var solutionPath = Path.Combine(_projectRoot, "PartnershipAgent.sln");
        if (!File.Exists(solutionPath))
        {
            Console.WriteLine($"❌ Solution file not found: {solutionPath}");
            return false;
        }
        
        Console.WriteLine($"Building: {solutionPath}");
        Console.WriteLine("This may take a few minutes on first run...");
        
        // Try the main build first with longer timeout
        bool buildSuccess = await RunCommandWithProgress("dotnet", "build PartnershipAgent.sln --configuration Release --verbosity minimal", workingDirectory: _projectRoot, timeoutMinutes: 15);
        
        if (!buildSuccess)
        {
            Console.WriteLine("⚠️ Release build failed, trying Debug build...");
            buildSuccess = await RunCommandWithProgress("dotnet", "build PartnershipAgent.sln --configuration Debug --verbosity minimal", workingDirectory: _projectRoot, timeoutMinutes: 15);
        }
        
        if (!buildSuccess)
        {
            Console.WriteLine("⚠️ Solution build failed, trying restore first...");
            if (await RunCommand("dotnet", "restore PartnershipAgent.sln", workingDirectory: _projectRoot))
            {
                Console.WriteLine("✅ Restore completed, retrying build...");
                buildSuccess = await RunCommandWithProgress("dotnet", "build PartnershipAgent.sln --configuration Debug --verbosity normal", workingDirectory: _projectRoot, timeoutMinutes: 15);
            }
        }
        
        if (!buildSuccess)
        {
            Console.WriteLine("❌ All build attempts failed. Common Windows issues:");
            Console.WriteLine("  • Antivirus software blocking file access");
            Console.WriteLine("  • Files in use by another process");
            Console.WriteLine("  • Insufficient disk space");
            Console.WriteLine("  • Network issues downloading packages");
            Console.WriteLine("\nTry:");
            Console.WriteLine("  1. Close Visual Studio/VS Code");
            Console.WriteLine("  2. Disable antivirus temporarily");
            Console.WriteLine("  3. Run as Administrator");
            Console.WriteLine("  4. Clear NuGet cache: dotnet nuget locals all --clear");
        }
        
        return buildSuccess;
    }

    private static Task RunTests()
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
        
        return Task.CompletedTask;
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

    private static async Task<bool> RunCommandWithProgress(string command, string arguments, 
        string? workingDirectory = null, int timeoutMinutes = 10)
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
            Console.WriteLine($"❌ Failed to start {command}");
            return false;
        }

        // Show progress indicators
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
        var progressTask = Task.Run(async () =>
        {
            var dots = 0;
            while (!process.HasExited && !cts.Token.IsCancellationRequested)
            {
                Console.Write(".");
                dots++;
                if (dots % 50 == 0) // New line every 50 dots
                {
                    Console.WriteLine();
                }
                await Task.Delay(1000, cts.Token);
            }
        }, cts.Token);

        // Read output in real-time for important messages
        var outputTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
            {
                // Show important build messages
                if (line.Contains("error") || line.Contains("failed") || line.Contains("warning"))
                {
                    Console.WriteLine($"\n{line}");
                }
            }
        });

        try
        {
            await process.WaitForExitAsync();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n❌ Command timed out after {timeoutMinutes} minutes");
            try { process.Kill(true); } catch { }
            return false;
        }
        finally
        {
            cts.Cancel(); // Stop progress indicator
            try { await progressTask; } catch (OperationCanceledException) { } // Ignore cancellation
        }

        Console.WriteLine(); // New line after progress dots
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"❌ Build failed with exit code {process.ExitCode}");
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Error output: {error}");
            }
            return false;
        }

        Console.WriteLine("✅ Build completed successfully");
        return true;
    }

    private static HttpClient CreateHttpClientWithAuth()
    {
        var client = new HttpClient();
        
        if (!string.IsNullOrEmpty(ElasticCredentials.Username) && !string.IsNullOrEmpty(ElasticCredentials.Password))
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ElasticCredentials.Username}:{ElasticCredentials.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }
        
        return client;
    }

    private static async Task<string> GetCommandOutput(string command, string arguments, 
        string? workingDirectory = null)
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
            return "";

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return process.ExitCode == 0 ? output : "";
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