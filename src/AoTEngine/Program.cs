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

// Initialize services
var openAIService = new OpenAIService(apiKey, model);
var validatorService = new CodeValidatorService();
var userInteractionService = new UserInteractionService();
var executionEngine = new ParallelExecutionEngine(openAIService, validatorService, userInteractionService);
var mergerService = new CodeMergerService(validatorService);
var orchestrator = new AoTEngineOrchestrator(openAIService, executionEngine, mergerService, userInteractionService);

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

// Execute the AoT workflow
var result = await orchestrator.ExecuteAsync(userRequest);

// Display results
Console.WriteLine();
Console.WriteLine(result.ExecutionReport);
Console.WriteLine();

if (result.Success)
{
    Console.WriteLine("=== Final Merged Code ===");
    Console.WriteLine(result.FinalCode);
    Console.WriteLine();

    // Optionally save to file
    var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "generated_code.cs");
    await File.WriteAllTextAsync(outputPath, result.FinalCode);
    Console.WriteLine($"Code saved to: {outputPath}");
}
else
{
    Console.WriteLine($"Execution failed: {result.ErrorMessage}");
    Environment.Exit(1);
}
