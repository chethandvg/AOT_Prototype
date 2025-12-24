using AoTEngine.Core;
using AoTEngine.Services;
using Microsoft.Extensions.Configuration;

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = configuration["OpenAI:Model"] ?? "gpt-4";

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Error: OpenAI API key not found.");
    Console.WriteLine("Please set the API key in appsettings.json or as an environment variable OPENAI_API_KEY");
    return;
}

Console.WriteLine("=== AoT Engine - Atom of Thought Prototype ===");
Console.WriteLine();

// Ask for output directory upfront
Console.WriteLine("📁 Enter output directory for generated code and project files:");
Console.WriteLine("   (Press Enter to use default: './GeneratedProjects')");
var outputDirectory = Console.ReadLine()?.Trim();

if (string.IsNullOrWhiteSpace(outputDirectory))
{
    outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedProjects");
    Console.WriteLine($"   Using default: {outputDirectory}");
}
else
{
    // Expand relative paths
    if (!Path.IsPathRooted(outputDirectory))
    {
        outputDirectory = Path.GetFullPath(outputDirectory);
    }
    Console.WriteLine($"   Using: {outputDirectory}");
}

// Validate/create output directory
try
{
    Directory.CreateDirectory(outputDirectory);
    Console.WriteLine($"   ✓ Output directory ready");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: Cannot create output directory: {ex.Message}");
    return;
}

Console.WriteLine();

// Initialize services with configuration
var openAIService = new OpenAIService(apiKey, model);
var validatorService = new CodeValidatorService(configuration);
var userInteractionService = new UserInteractionService();
var executionEngine = new ParallelExecutionEngine(openAIService, validatorService, userInteractionService);
var mergerService = new CodeMergerService(validatorService);
var orchestrator = new AoTEngineOrchestrator(openAIService, executionEngine, mergerService, userInteractionService, validatorService);

// Get user request
Console.WriteLine("Enter your coding request (or press Enter for a demo):");
var userRequest = Console.ReadLine();

if (string.IsNullOrWhiteSpace(userRequest))
{
    // Demo request
    userRequest = "Create a simple calculator class in C# that can add, subtract, multiply, and divide two numbers. Include input validation and unit tests.";
    Console.WriteLine($"\nUsing demo request: {userRequest}");
}
else
{
    // Basic input sanitization
    userRequest = userRequest.Trim();
    
    // Validate input length
    if (userRequest.Length > 2000)
    {
        Console.WriteLine("Error: Request is too long. Please limit your request to 2000 characters.");
        Environment.Exit(1);
    }
    
    // Remove potentially harmful characters
    userRequest = System.Text.RegularExpressions.Regex.Replace(userRequest, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty);
}

Console.WriteLine();

// Execute the AoT workflow with validation mode from configuration
var useHybridValidation = configuration.GetValue<bool>("Engine:UseHybridValidation", true);
var useBatchValidation = configuration.GetValue<bool>("Engine:UseBatchValidation", true);

if (useHybridValidation)
{
    Console.WriteLine("Using hybrid validation mode (individual + batch)");
}
else if (useBatchValidation)
{
    Console.WriteLine("Using batch validation mode");
}
else
{
    Console.WriteLine("Using individual validation mode");
}

var result = await orchestrator.ExecuteAsync(
    userRequest, 
    useBatchValidation: useBatchValidation, 
    useHybridValidation: useHybridValidation,
    outputDirectory: outputDirectory);

// Display results
Console.WriteLine();
Console.WriteLine(result.ExecutionReport);
Console.WriteLine();

if (result.Success)
{
    Console.WriteLine("=== Final Merged Code ===");
    Console.WriteLine(result.FinalCode);
    Console.WriteLine();

    // Save to file in the output directory
    var codeFilePath = Path.Combine(outputDirectory, "generated_code.cs");
    await File.WriteAllTextAsync(codeFilePath, result.FinalCode);
    Console.WriteLine($"Code saved to: {codeFilePath}");
    
    // Ask if user wants to create a standalone project
    Console.WriteLine();
    Console.WriteLine("Would you like to create a standalone runnable project? (y/n):");
    var createProject = Console.ReadLine()?.Trim().ToLower();
    
    if (createProject == "y" || createProject == "yes")
    {
        Console.WriteLine("Enter project name (or press Enter for 'GeneratedApp'):");
        var projectName = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = "GeneratedApp";
        }
        
        var buildService = new ProjectBuildService();
        
        // Use the new method that creates separate files and adds package references dynamically
        // This analyzes the generated code and extracts required packages before creating the project
        var buildResult = await buildService.CreateProjectFromTasksAsync(outputDirectory, projectName, result.Tasks);
        
        if (buildResult.Success)
        {
            Console.WriteLine($"\n🎉 Project created successfully at: {buildResult.ProjectPath}");
            if (buildResult.GeneratedFiles.Any())
            {
                Console.WriteLine($"   📄 Generated files:");
                foreach (var file in buildResult.GeneratedFiles)
                {
                    Console.WriteLine($"      - {Path.GetFileName(file)}");
                }
            }
            if (!string.IsNullOrEmpty(buildResult.OutputAssemblyPath))
            {
                Console.WriteLine($"   🔧 Assembly: {buildResult.OutputAssemblyPath}");
            }
            
            // Ask if user wants to run the project
            Console.WriteLine();
            Console.WriteLine("Would you like to run the project? (y/n):");
            var runProject = Console.ReadLine()?.Trim().ToLower();
            
            if (runProject == "y" || runProject == "yes")
            {
                Console.WriteLine("\n▶️  Running project...");
                Console.WriteLine("─────────────────────────────────────────");
                
                var runProcess = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{buildResult.ProjectPath}\"",
                    UseShellExecute = false
                };
                
                var process = System.Diagnostics.Process.Start(runProcess);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    Console.WriteLine("─────────────────────────────────────────");
                    Console.WriteLine($"Process exited with code: {process.ExitCode}");
                }
            }
        }
        else
        {
            Console.WriteLine($"\n❌ Project creation/build failed: {buildResult.ErrorMessage}");
            if (buildResult.Errors.Any())
            {
                Console.WriteLine("\nBuild Errors:");
                foreach (var error in buildResult.Errors.Take(10))
                {
                    Console.WriteLine($"   {error}");
                }
            }
        }
    }
}
else
{
    Console.WriteLine($"Execution failed: {result.ErrorMessage}");
    Environment.Exit(1);
}
