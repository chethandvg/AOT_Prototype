using AoTEngine.AtomicAgent;
using AoTEngine.AtomicAgent.Blackboard;
using AoTEngine.AtomicAgent.ClarificationLoop;
using AoTEngine.AtomicAgent.Context;
using AoTEngine.AtomicAgent.Execution;
using AoTEngine.AtomicAgent.Planner;
using AoTEngine.AtomicAgent.Roslyn;
using AoTEngine.AtomicAgent.Workspace;
using AoTEngine.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Load .env file if exists
try
{
    dotenv.net.DotEnv.Load();
}
catch
{
    // .env file is optional
}

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Prompt user for output directory
Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Atomic Thought Framework - Autonomous C# Coding Agent       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Where would you like to store the generated solution?");
Console.WriteLine($"(Press Enter for default: {Path.GetFullPath("./output")})");
Console.Write("> ");
var outputPath = Console.ReadLine()?.Trim();

if (string.IsNullOrWhiteSpace(outputPath))
{
    outputPath = configuration.GetSection("Workspace")["RootPath"] ?? "./output";
}

// Validate and create the directory
try
{
    var fullOutputPath = Path.GetFullPath(outputPath);
    Console.WriteLine($"✓ Using output directory: {fullOutputPath}\n");
    outputPath = fullOutputPath;
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Invalid path: {ex.Message}");
    Console.WriteLine($"   Using default: {Path.GetFullPath("./output")}\n");
    outputPath = "./output";
}

// Configure services using .NET Generic Host
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Get API key
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ Error: OpenAI API key not found.");
            Console.WriteLine("   Set it in appsettings.json or as environment variable OPENAI_API_KEY");
            Environment.Exit(1);
        }

        var model = configuration["OpenAI:Model"] ?? "gpt-4";

        // Register configuration sections
        var workspaceConfig = configuration.GetSection("Workspace");
        var blackboardConfig = configuration.GetSection("Blackboard");
        var plannerConfig = configuration.GetSection("Planner");
        var clarificationConfig = configuration.GetSection("Clarification");
        var contextConfig = configuration.GetSection("Context");
        var executionConfig = configuration.GetSection("Execution");
        var roslynConfig = configuration.GetSection("Roslyn");

        // Register services
        services.AddMemoryCache();
        
        // Workspace - use user-provided path
        services.AddSingleton(sp => new WorkspaceService(
            outputPath,
            workspaceConfig.GetValue<bool>("EnableSandboxing", true),
            sp.GetRequiredService<ILogger<WorkspaceService>>()));

        // Blackboard
        services.AddSingleton(sp =>
        {
            var workspace = sp.GetRequiredService<WorkspaceService>();
            return new BlackboardService(
                workspace.RootPath,
                blackboardConfig["ManifestFileName"] ?? "solution_manifest.json",
                blackboardConfig.GetValue<bool>("AutoSave", true),
                sp.GetRequiredService<ILogger<BlackboardService>>());
        });

        // OpenAI Service (from existing library)
        services.AddSingleton(sp => new OpenAIService(apiKey, model));

        // Planner
        services.AddSingleton(sp => new PlannerAgent(
            sp.GetRequiredService<OpenAIService>(),
            sp.GetRequiredService<ILogger<PlannerAgent>>(),
            plannerConfig.GetValue<bool>("AbstractionsFirst", true),
            plannerConfig.GetValue<bool>("EnableTopologicalSort", true),
            plannerConfig.GetValue<int>("MaxRetryOnCircularDependency", 3)));

        // Clarification
        services.AddSingleton(sp => new ClarificationService(
            sp.GetRequiredService<ILogger<ClarificationService>>(),
            clarificationConfig.GetValue<bool>("EnableAmbiguityDetection", true),
            clarificationConfig.GetValue<int>("VagueTermsThreshold", 3)));

        // Context Engine
        services.AddSingleton(sp => new ContextEngine(
            sp.GetRequiredService<BlackboardService>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<ILogger<ContextEngine>>(),
            contextConfig.GetValue<bool>("EnableHotCache", true)));

        // Roslyn Feedback Loop
        services.AddSingleton(sp => new RoslynFeedbackLoop(
            sp.GetRequiredService<BlackboardService>(),
            sp.GetRequiredService<ILogger<RoslynFeedbackLoop>>(),
            roslynConfig.GetValue<bool>("SuppressWarnings", true)));

        // Project Compilation Service
        services.AddSingleton(sp => new ProjectCompilationService(
            sp.GetRequiredService<BlackboardService>(),
            sp.GetRequiredService<WorkspaceService>(),
            sp.GetRequiredService<ILogger<ProjectCompilationService>>(),
            roslynConfig.GetValue<bool>("SuppressWarnings", true)));

        // Atomic Worker
        services.AddSingleton(sp => new AtomicWorkerAgent(
            sp.GetRequiredService<OpenAIService>(),
            sp.GetRequiredService<ContextEngine>(),
            sp.GetRequiredService<RoslynFeedbackLoop>(),
            sp.GetRequiredService<BlackboardService>(),
            sp.GetRequiredService<ILogger<AtomicWorkerAgent>>(),
            executionConfig.GetValue<int>("MaxRetries", 3),
            executionConfig.GetValue<bool>("ValidateAtomically", true)));

        // Orchestrator
        services.AddSingleton(sp => new AtomicAgentOrchestrator(
            sp.GetRequiredService<WorkspaceService>(),
            sp.GetRequiredService<BlackboardService>(),
            sp.GetRequiredService<PlannerAgent>(),
            sp.GetRequiredService<ClarificationService>(),
            sp.GetRequiredService<ContextEngine>(),
            sp.GetRequiredService<AtomicWorkerAgent>(),
            sp.GetRequiredService<ProjectCompilationService>(),
            sp.GetRequiredService<ILogger<AtomicAgentOrchestrator>>(),
            executionConfig["CompilationMode"] ?? "Progressive",
            executionConfig.GetValue<bool>("ValidateAfterAllGenerated", true),
            executionConfig.GetValue<int>("MaxProgressiveRounds", 3)));
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

// Display components
var compilationMode = configuration["Execution:CompilationMode"] ?? "Progressive";
Console.WriteLine("Components:");
Console.WriteLine("  1. ✓ Workspace Service (Sandboxed File System)");
Console.WriteLine("  2. ✓ Blackboard Service (Shared Knowledge Base)");
Console.WriteLine("  3. ✓ Planner Agent (Abstractions First + Topological Sort)");
Console.WriteLine("  4. ✓ Clarification Loop (Ambiguity Detection)");
Console.WriteLine("  5. ✓ Context Engine (Tiered Injection + Hot Cache)");
Console.WriteLine("  6. ✓ Atomic Worker Agent (Code Generation)");
Console.WriteLine($"  7. ✓ Compilation Mode: {compilationMode}");
Console.WriteLine("  8. ⚠  Token Garbage Collection (Not yet implemented)");
Console.WriteLine();

// Get user request
Console.WriteLine("Enter your coding request:");
Console.Write("> ");
var userRequest = Console.ReadLine()?.Trim();

if (string.IsNullOrWhiteSpace(userRequest))
{
    userRequest = "Create a simple user management system with a User DTO, IUserRepository interface, and FileUserRepository implementation that stores users in a JSON file.";
    Console.WriteLine($"\nUsing demo request:\n{userRequest}\n");
}

// Execute the workflow
var orchestrator = host.Services.GetRequiredService<AtomicAgentOrchestrator>();
var result = await orchestrator.ExecuteAsync(userRequest);

// Exit with appropriate code
Environment.Exit(result.Success ? 0 : 1);
