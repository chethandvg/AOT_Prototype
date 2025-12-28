# Quick Start: Using Individual Modules

This guide shows how to use AoT Engine modules independently for specific use cases.

## Scenario 1: Simple Code Generator (No Validation)

**Modules Needed**: 
- AoTEngine.Models
- AoTEngine.Services.AI

```csharp
using AoTEngine.Models;
using AoTEngine.Services;

// Initialize OpenAI service
var openAI = new OpenAIService(apiKey: "your-api-key");

// Decompose task
var request = new TaskDecompositionRequest 
{ 
    Request = "Create a user authentication system",
    Context = new List<string>()
};

var response = await openAI.DecomposeTaskAsync(request);

// Generate code for each task
foreach (var task in response.Tasks)
{
    Console.WriteLine($"Generating code for: {task.Description}");
    var code = await openAI.GenerateCodeAsync(task, response.Tasks);
    
    // Save or use the code
    File.WriteAllText($"{task.Id}.cs", code);
}
```

## Scenario 2: Code Validator Only

**Modules Needed**: 
- AoTEngine.Models
- AoTEngine.Services.Compilation
- AoTEngine.Services.Validation

```csharp
using AoTEngine.Models;
using AoTEngine.Services;

// Initialize validator
var validator = new CodeValidatorService();

// Validate existing code
string codeToValidate = File.ReadAllText("MyClass.cs");
var result = await validator.ValidateCodeAsync(codeToValidate);

if (result.IsValid)
{
    Console.WriteLine("✓ Code is valid!");
}
else
{
    Console.WriteLine("✗ Validation errors:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

## Scenario 3: Documentation Generator

**Modules Needed**: 
- AoTEngine.Models
- AoTEngine.Services.AI
- AoTEngine.Services.Documentation

```csharp
using AoTEngine.Models;
using AoTEngine.Services;

// Initialize services
var openAI = new OpenAIService(apiKey: "your-api-key");
var docService = new DocumentationService(openAI);

// Create tasks (could be loaded from existing project)
var tasks = new List<TaskNode>
{
    new TaskNode 
    { 
        Id = "task1", 
        Description = "User authentication service",
        GeneratedCode = File.ReadAllText("AuthService.cs")
    },
    // ... more tasks
};

// Generate documentation
var documentation = await docService.SynthesizeProjectDocumentationAsync(
    tasks, 
    "User Management System"
);

// Export to different formats
await docService.ExportMarkdownAsync(documentation, "./docs/Documentation.md");
await docService.ExportJsonAsync(documentation, "./docs/Documentation.json");
await docService.ExportJsonlDatasetAsync(tasks, "./docs/training_data.jsonl");
```

## Scenario 4: Contract-First Code Generation

**Modules Needed**: 
- AoTEngine.Models
- AoTEngine.Services.AI
- AoTEngine.Services.Contracts
- AoTEngine.Services.Validation

```csharp
using AoTEngine.Models;
using AoTEngine.Services;

// Initialize services
var openAI = new OpenAIService(apiKey: "your-api-key");
var contractService = new ContractGenerationService(apiKey: "your-api-key");
var manifestService = new ContractManifestService();

// Generate contract catalog from task descriptions
var tasks = new List<TaskNode>
{
    new TaskNode 
    { 
        Id = "task1", 
        Description = "Create IUserRepository interface with CRUD methods" 
    },
    new TaskNode 
    { 
        Id = "task2", 
        Description = "Create UserStatus enum (Active, Inactive, Banned)" 
    }
};

var catalog = await contractService.GenerateContractCatalogAsync(tasks);

// Save contracts
await manifestService.SaveManifestAsync(catalog, "./contracts/");
await manifestService.GenerateContractFilesAsync(catalog, "./contracts/");

// Now generate implementations using frozen contracts
foreach (var task in tasks)
{
    var code = await openAI.GenerateCodeWithContractsAsync(task, catalog);
    File.WriteAllText($"{task.Id}.cs", code);
}
```

## Scenario 5: Code Integration & Merging

**Modules Needed**: 
- AoTEngine.Models
- AoTEngine.Services.Compilation
- AoTEngine.Services.Validation
- AoTEngine.Services.Integration

```csharp
using AoTEngine.Models;
using AoTEngine.Services;

// Initialize services
var validator = new CodeValidatorService();
var merger = new CodeMergerService(validator);

// Load code snippets from different sources
var codeSnippets = new Dictionary<string, string>
{
    ["AuthService.cs"] = File.ReadAllText("AuthService.cs"),
    ["UserRepository.cs"] = File.ReadAllText("UserRepository.cs"),
    ["UserModel.cs"] = File.ReadAllText("UserModel.cs")
};

// Merge with advanced integration (handles conflicts)
var tasks = codeSnippets.Select((kvp, idx) => new TaskNode
{
    Id = $"task{idx}",
    Description = kvp.Key,
    GeneratedCode = kvp.Value
}).ToList();

var mergeResult = await merger.MergeWithIntegrationAsync(tasks, new SymbolTable());

if (mergeResult.Success)
{
    Console.WriteLine("✓ Code merged successfully!");
    File.WriteAllText("MergedCode.cs", mergeResult.MergedCode);
}
else
{
    Console.WriteLine("✗ Merge conflicts detected:");
    foreach (var conflict in mergeResult.Conflicts)
    {
        Console.WriteLine($"  - {conflict.Message}");
    }
}
```

## Scenario 6: Full Pipeline (All Modules)

**Modules Needed**: All modules

```csharp
using AoTEngine.Core;
using AoTEngine.Models;
using AoTEngine.Services;

// Initialize orchestrator (coordinates all services)
var apiKey = "your-api-key";
var model = "gpt-5.1";
var outputDir = "./output";

var orchestrator = new AoTEngineOrchestrator(
    apiKey: apiKey,
    model: model,
    outputDirectory: outputDir,
    maxRetries: 3,
    enableComplexityAnalysis: true,
    enableContractFirst: true
);

// Execute full pipeline
var result = await orchestrator.ExecuteAsync(
    userRequest: "Create a REST API for managing blog posts"
);

if (result.Success)
{
    Console.WriteLine("✓ Execution completed successfully!");
    Console.WriteLine($"Generated code saved to: {outputDir}");
    Console.WriteLine($"Documentation available at: {outputDir}/Documentation.md");
}
```

## NuGet Package Installation (Future)

Once published as NuGet packages, you can install only what you need:

```bash
# For simple code generation
dotnet add package AoTEngine.Models
dotnet add package AoTEngine.Services.AI

# For code validation
dotnet add package AoTEngine.Models
dotnet add package AoTEngine.Services.Compilation
dotnet add package AoTEngine.Services.Validation

# For documentation
dotnet add package AoTEngine.Models
dotnet add package AoTEngine.Services.AI
dotnet add package AoTEngine.Services.Documentation

# For full pipeline
dotnet add package AoTEngine.Core  # Includes all dependencies
```

## Benefits of Modular Approach

1. **Smaller Dependencies**: Only reference what you need
2. **Faster Startup**: Fewer assemblies to load
3. **Clearer Intent**: Your project references show exactly which features you use
4. **Easier Testing**: Mock only the services you need
5. **Independent Updates**: Update modules independently
6. **Custom Workflows**: Build exactly the workflow you need

## Next Steps

- See **[MODULAR_ARCHITECTURE.md](MODULAR_ARCHITECTURE.md)** for detailed module documentation
- See **[ARCHITECTURE.md](ARCHITECTURE.md)** for system architecture details
- See **[USAGE.md](USAGE.md)** for comprehensive usage examples
