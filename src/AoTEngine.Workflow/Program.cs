using AoTEngine.Core;
using AoTEngine.Services;
using AoTEngine.Workflow;
using Microsoft.Extensions.Configuration;

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var planningModel = configuration["OpenAI:PlanningModel"] ?? "gpt-5.1";

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Error: OpenAI API key not found.");
    Console.WriteLine("Please set the API key in appsettings.json or as an environment variable OPENAI_API_KEY");
    return 1;
}

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║     AoT Engine - Coordinated Atomic Code Generation       ║");
Console.WriteLine("║                      Workflow                              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("This workflow implements the 'Coordinating Atomic Code Generation' pattern:");
Console.WriteLine("  1. 📋 Planning Phase - Clarify requirements and outline solution");
Console.WriteLine("  2. 📝 Specification - Generate detailed spec from clarifications");
Console.WriteLine("  3. 🗺️  Blueprint - Break spec into atomic task plan");
Console.WriteLine("  4. 🔗 Shared Context - Maintain global blackboard for all tasks");
Console.WriteLine("  5. ✅ Iterative Verification - Verify each task before moving on");
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
    Console.WriteLine("   ✓ Output directory ready");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: Cannot create output directory: {ex.Message}");
    return 1;
}

Console.WriteLine();

// Load workflow configuration
var workflowConfig = new WorkflowConfig
{
    EnablePlanningPhase = configuration.GetValue("Workflow:EnablePlanningPhase", true),
    EnableClarificationPhase = configuration.GetValue("Workflow:EnableClarificationPhase", true),
    EnableBlueprintGeneration = configuration.GetValue("Workflow:EnableBlueprintGeneration", true),
    EnableIterativeVerification = configuration.GetValue("Workflow:EnableIterativeVerification", true),
    MaxLinesPerTask = configuration.GetValue("Workflow:MaxLinesPerTask", 300),
    EnableContractFirst = configuration.GetValue("Workflow:EnableContractFirst", true),
    MaxRetries = configuration.GetValue("Workflow:MaxRetries", 3)
};

Console.WriteLine("Workflow Configuration:");
Console.WriteLine($"  Planning Phase:         {(workflowConfig.EnablePlanningPhase ? "✓ Enabled" : "✗ Disabled")}");
Console.WriteLine($"  Clarification Phase:    {(workflowConfig.EnableClarificationPhase ? "✓ Enabled" : "✗ Disabled")}");
Console.WriteLine($"  Blueprint Generation:   {(workflowConfig.EnableBlueprintGeneration ? "✓ Enabled" : "✗ Disabled")}");
Console.WriteLine($"  Iterative Verification: {(workflowConfig.EnableIterativeVerification ? "✓ Enabled" : "✗ Disabled")}");
Console.WriteLine($"  Contract-First:         {(workflowConfig.EnableContractFirst ? "✓ Enabled" : "✗ Disabled")}");
Console.WriteLine($"  Max Lines Per Task:     {workflowConfig.MaxLinesPerTask}");
Console.WriteLine($"  Max Retries:            {workflowConfig.MaxRetries}");
Console.WriteLine();

// Initialize services with configuration
var openAIService = new OpenAIService(apiKey, planningModel);
var validatorService = new CodeValidatorService(configuration);
var userInteractionService = new UserInteractionService();
var buildService = new ProjectBuildService(openAIService);
var executionEngine = new ParallelExecutionEngine(
    openAIService, 
    validatorService, 
    userInteractionService,
    buildService,
    outputDirectory);
var mergerService = new CodeMergerService(validatorService);
var contractService = new ContractGenerationService(apiKey, planningModel);

// Create the coordinated workflow orchestrator
var orchestrator = new CoordinatedWorkflowOrchestrator(
    openAIService,
    executionEngine,
    mergerService,
    validatorService,
    userInteractionService,
    workflowConfig,
    contractService);

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
    // Basic input sanitization - do sanitization first before length checks
    userRequest = userRequest.Trim();
    
    // Remove potentially harmful control characters
    userRequest = System.Text.RegularExpressions.Regex.Replace(
        userRequest,
        @"[\x00-\x08\x0B\x0C\x0E-\x1F]",
        string.Empty);

    // Ensure input is still meaningful after sanitization
    if (string.IsNullOrWhiteSpace(userRequest))
    {
        Console.WriteLine("Error: Request is empty after removing unsupported characters. Please provide a valid request.");
        return 1;
    }

    // Reject excessively long repeated character sequences that may indicate abuse
    if (System.Text.RegularExpressions.Regex.IsMatch(userRequest, @"(.)\1{199,}"))
    {
        Console.WriteLine("Error: Request contains excessively repeated characters. Please simplify your request.");
        return 1;
    }

    // Validate input length after sanitization
    if (userRequest.Length > 2000)
    {
        Console.WriteLine("Error: Request is too long. Please limit your request to 2000 characters.");
        return 1;
    }
}

Console.WriteLine();

// Execute the coordinated workflow
var result = await orchestrator.ExecuteAsync(userRequest, outputDirectory);

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
    
    // Save the shared context as JSON for reference
    var contextFilePath = Path.Combine(outputDirectory, "workflow_context.json");
    var contextJson = System.Text.Json.JsonSerializer.Serialize(result.Context, new System.Text.Json.JsonSerializerOptions 
    { 
        WriteIndented = true 
    });
    await File.WriteAllTextAsync(contextFilePath, contextJson);
    Console.WriteLine($"Workflow context saved to: {contextFilePath}");

    // Save execution report
    var reportFilePath = Path.Combine(outputDirectory, "execution_report.txt");
    await File.WriteAllTextAsync(reportFilePath, result.ExecutionReport);
    Console.WriteLine($"Execution report saved to: {reportFilePath}");

    Console.WriteLine();
    Console.WriteLine($"✅ Workflow completed successfully in {result.TotalDuration.TotalSeconds:F1} seconds!");
    
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
        
        // Use the build service to create the project
        var buildResult = await buildService.CreateProjectFromTasksAsync(outputDirectory, projectName, result.Tasks);
        
        if (buildResult.Success)
        {
            Console.WriteLine($"\n🎉 Project created successfully at: {buildResult.ProjectPath}");
            if (buildResult.GeneratedFiles.Any())
            {
                Console.WriteLine("   📄 Generated files:");
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
    
    return 0;
}
else
{
    Console.WriteLine($"❌ Workflow failed: {result.ErrorMessage}");
    return 1;
}
