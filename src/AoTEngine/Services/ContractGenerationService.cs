using AoTEngine.Models;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace AoTEngine.Services;

/// <summary>
/// Service for generating contracts (interfaces, enums, DTOs, abstract classes) before implementations.
/// This implements the "Contract-First" approach to prevent type mismatches and missing members.
/// </summary>
public class ContractGenerationService
{
    private readonly ChatClient _chatClient;
    private readonly ContractManifestService _manifestService;
    private readonly SymbolTable _symbolTable;
    private const int MaxRetries = 3;

    public ContractGenerationService(string apiKey, string model = "gpt-4")
    {
        _chatClient = new ChatClient(model, apiKey);
        _manifestService = new ContractManifestService();
        _symbolTable = new SymbolTable();
    }

    /// <summary>
    /// Gets the symbol table for tracking symbols.
    /// </summary>
    public SymbolTable SymbolTable => _symbolTable;

    /// <summary>
    /// Generates a contract catalog from task decomposition.
    /// This should be called after task decomposition but before code generation.
    /// </summary>
    /// <param name="tasks">Decomposed tasks.</param>
    /// <param name="projectName">Project name for namespace prefixing.</param>
    /// <param name="userRequest">Original user request for context.</param>
    /// <returns>A frozen contract catalog.</returns>
    public async Task<ContractCatalog> GenerateContractCatalogAsync(
        List<TaskNode> tasks,
        string projectName,
        string userRequest)
    {
        Console.WriteLine("\n📋 Generating contract catalog (Contract-First approach)...");
        
        var catalog = new ContractCatalog
        {
            ProjectName = projectName,
            RootNamespace = projectName
        };

        // Step 1: Analyze tasks to identify all types that need contracts
        var typeAnalysis = await AnalyzeTasksForContractsAsync(tasks, projectName, userRequest);

        // Step 2: Generate enum contracts first (they have no dependencies)
        Console.WriteLine("   Generating enum contracts...");
        var enumContracts = await GenerateEnumContractsAsync(typeAnalysis.Enums, projectName);
        catalog.Enums.AddRange(enumContracts);
        RegisterContracts(enumContracts);

        // Step 3: Generate interface contracts
        Console.WriteLine("   Generating interface contracts...");
        var interfaceContracts = await GenerateInterfaceContractsAsync(typeAnalysis.Interfaces, projectName, catalog);
        catalog.Interfaces.AddRange(interfaceContracts);
        RegisterContracts(interfaceContracts);

        // Step 4: Generate model/DTO contracts
        Console.WriteLine("   Generating model/DTO contracts...");
        var modelContracts = await GenerateModelContractsAsync(typeAnalysis.Models, projectName, catalog);
        catalog.Models.AddRange(modelContracts);
        RegisterContracts(modelContracts);

        // Step 5: Generate abstract class contracts
        Console.WriteLine("   Generating abstract class contracts...");
        var abstractContracts = await GenerateAbstractClassContractsAsync(typeAnalysis.AbstractClasses, projectName, catalog);
        catalog.AbstractClasses.AddRange(abstractContracts);
        RegisterContracts(abstractContracts);

        // Freeze the catalog
        catalog.Freeze();
        Console.WriteLine($"✅ Contract catalog frozen with {catalog.GetAllContracts().Count()} contracts");

        return catalog;
    }

    /// <summary>
    /// Analyzes tasks to identify types that need contracts.
    /// </summary>
    private async Task<TypeAnalysisResult> AnalyzeTasksForContractsAsync(
        List<TaskNode> tasks,
        string projectName,
        string userRequest)
    {
        var systemPrompt = @"You are an expert software architect. Analyze the given tasks and identify all types that need to be defined as contracts BEFORE implementation.

Your output must be valid JSON following this structure:
{
  ""enums"": [
    {
      ""name"": ""StatusType"",
      ""namespace"": ""ProjectName.Models"",
      ""members"": [""Active"", ""Inactive"", ""Pending""],
      ""description"": ""Status values for entities""
    }
  ],
  ""interfaces"": [
    {
      ""name"": ""IDataService"",
      ""namespace"": ""ProjectName.Services"",
      ""methods"": [
        {
          ""name"": ""GetDataAsync"",
          ""returnType"": ""Task<Data>"",
          ""parameters"": [{""name"": ""id"", ""type"": ""int""}]
        }
      ],
      ""description"": ""Service interface for data operations""
    }
  ],
  ""models"": [
    {
      ""name"": ""UserInfo"",
      ""namespace"": ""ProjectName.Models"",
      ""properties"": [{""name"": ""Id"", ""type"": ""int""}, {""name"": ""Name"", ""type"": ""string""}],
      ""description"": ""User information DTO""
    }
  ],
  ""abstractClasses"": [
    {
      ""name"": ""BaseExporter"",
      ""namespace"": ""ProjectName.Services"",
      ""abstractMethods"": [
        {
          ""name"": ""ExportAsync"",
          ""returnType"": ""Task"",
          ""parameters"": [{""name"": ""data"", ""type"": ""IReadOnlyList<T>""}]
        }
      ],
      ""isSealed"": false,
      ""description"": ""Base class for exporters""
    }
  ]
}

CRITICAL RULES:
1. DTOs and Models MUST be in the '.Models' namespace, NOT in '.Services'
2. Interfaces MUST be in appropriate namespaces (often '.Services' or '.Contracts')
3. Enums should be in '.Models' unless they're service-specific
4. Each type name must be UNIQUE across all namespaces
5. Include ALL enum members that will be referenced
6. Include ALL interface methods with EXACT return types and parameters
7. Include ALL abstract methods that derived classes must implement
8. Mark sealed classes appropriately to prevent incorrect inheritance
9. Consider async patterns (Task<T>) for I/O operations";

        var taskDescriptions = string.Join("\n", tasks.Select(t => $"- Task {t.Id}: {t.Description} (Namespace: {t.Namespace}, Expected Types: {string.Join(", ", t.ExpectedTypes)})"));
        
        var userPrompt = $@"Project: {projectName}
Original Request: {userRequest}

Tasks to analyze:
{taskDescriptions}

Identify ALL enums, interfaces, models, and abstract classes that need contracts.
Return ONLY valid JSON.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var completion = await _chatClient.CompleteChatAsync(messages);
                var content = completion.Value.Content[0].Text;
                
                // Clean up markdown if present
                content = CleanJsonResponse(content);
                
                var result = JsonConvert.DeserializeObject<TypeAnalysisResult>(content);
                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Type analysis attempt {attempt + 1} failed: {ex.Message}");
                if (attempt == MaxRetries - 1) throw;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        return new TypeAnalysisResult();
    }

    /// <summary>
    /// Generates enum contracts from analysis.
    /// </summary>
    private async Task<List<EnumContract>> GenerateEnumContractsAsync(
        List<EnumAnalysis> enumAnalyses,
        string projectName)
    {
        var contracts = new List<EnumContract>();

        foreach (var analysis in enumAnalyses)
        {
            var contract = new EnumContract
            {
                Name = analysis.Name,
                Namespace = string.IsNullOrEmpty(analysis.Namespace) 
                    ? $"{projectName}.Models" 
                    : analysis.Namespace,
                Documentation = analysis.Description,
                IsFlags = analysis.IsFlags
            };

            foreach (var memberName in analysis.Members)
            {
                contract.Members.Add(new EnumMemberContract
                {
                    Name = memberName
                });
            }

            contracts.Add(contract);
        }

        return contracts;
    }

    /// <summary>
    /// Generates interface contracts from analysis.
    /// </summary>
    private async Task<List<InterfaceContract>> GenerateInterfaceContractsAsync(
        List<InterfaceAnalysis> interfaceAnalyses,
        string projectName,
        ContractCatalog catalog)
    {
        var contracts = new List<InterfaceContract>();

        foreach (var analysis in interfaceAnalyses)
        {
            var contract = new InterfaceContract
            {
                Name = analysis.Name,
                Namespace = string.IsNullOrEmpty(analysis.Namespace)
                    ? $"{projectName}.Services"
                    : analysis.Namespace,
                Documentation = analysis.Description,
                BaseInterfaces = analysis.BaseInterfaces ?? new List<string>()
            };

            foreach (var methodAnalysis in analysis.Methods ?? new List<MethodAnalysis>())
            {
                var method = new MethodSignatureContract
                {
                    Name = methodAnalysis.Name,
                    ReturnType = methodAnalysis.ReturnType ?? "void",
                    IsAsync = methodAnalysis.ReturnType?.StartsWith("Task") ?? false
                };

                foreach (var param in methodAnalysis.Parameters ?? new List<ParameterAnalysis>())
                {
                    method.Parameters.Add(new ParameterContract
                    {
                        Name = param.Name,
                        Type = param.Type
                    });
                }

                contract.Methods.Add(method);
            }

            foreach (var propAnalysis in analysis.Properties ?? new List<PropertyAnalysis>())
            {
                contract.Properties.Add(new PropertySignatureContract
                {
                    Name = propAnalysis.Name,
                    Type = propAnalysis.Type,
                    HasGetter = true,
                    HasSetter = propAnalysis.HasSetter
                });
            }

            contracts.Add(contract);
        }

        return contracts;
    }

    /// <summary>
    /// Generates model/DTO contracts from analysis.
    /// </summary>
    private async Task<List<ModelContract>> GenerateModelContractsAsync(
        List<ModelAnalysis> modelAnalyses,
        string projectName,
        ContractCatalog catalog)
    {
        var contracts = new List<ModelContract>();

        foreach (var analysis in modelAnalyses)
        {
            var contract = new ModelContract
            {
                Name = analysis.Name,
                Namespace = string.IsNullOrEmpty(analysis.Namespace)
                    ? $"{projectName}.Models"
                    : analysis.Namespace,
                Documentation = analysis.Description,
                IsRecord = analysis.IsRecord,
                BaseClass = analysis.BaseClass,
                ImplementedInterfaces = analysis.ImplementedInterfaces ?? new List<string>()
            };

            foreach (var propAnalysis in analysis.Properties ?? new List<PropertyAnalysis>())
            {
                contract.Properties.Add(new PropertySignatureContract
                {
                    Name = propAnalysis.Name,
                    Type = propAnalysis.Type,
                    HasGetter = true,
                    HasSetter = propAnalysis.HasSetter
                });
            }

            contracts.Add(contract);
        }

        return contracts;
    }

    /// <summary>
    /// Generates abstract class contracts from analysis.
    /// </summary>
    private async Task<List<AbstractClassContract>> GenerateAbstractClassContractsAsync(
        List<AbstractClassAnalysis> abstractClassAnalyses,
        string projectName,
        ContractCatalog catalog)
    {
        var contracts = new List<AbstractClassContract>();

        foreach (var analysis in abstractClassAnalyses)
        {
            var contract = new AbstractClassContract
            {
                Name = analysis.Name,
                Namespace = string.IsNullOrEmpty(analysis.Namespace)
                    ? $"{projectName}.Services"
                    : analysis.Namespace,
                Documentation = analysis.Description,
                IsSealed = analysis.IsSealed,
                BaseClass = analysis.BaseClass,
                ImplementedInterfaces = analysis.ImplementedInterfaces ?? new List<string>()
            };

            foreach (var methodAnalysis in analysis.AbstractMethods ?? new List<MethodAnalysis>())
            {
                var method = new MethodSignatureContract
                {
                    Name = methodAnalysis.Name,
                    ReturnType = methodAnalysis.ReturnType ?? "void",
                    IsAsync = methodAnalysis.ReturnType?.StartsWith("Task") ?? false
                };

                foreach (var param in methodAnalysis.Parameters ?? new List<ParameterAnalysis>())
                {
                    method.Parameters.Add(new ParameterContract
                    {
                        Name = param.Name,
                        Type = param.Type
                    });
                }

                contract.AbstractMethods.Add(method);
            }

            contracts.Add(contract);
        }

        return contracts;
    }

    /// <summary>
    /// Registers contracts in the symbol table.
    /// </summary>
    private void RegisterContracts<T>(List<T> contracts) where T : BaseContract
    {
        foreach (var contract in contracts)
        {
            var symbolKind = contract switch
            {
                EnumContract => ProjectSymbolKind.Enum,
                InterfaceContract => ProjectSymbolKind.Interface,
                _ => ProjectSymbolKind.Type
            };

            _symbolTable.TryRegister(new ProjectSymbolInfo
            {
                FullyQualifiedName = contract.FullyQualifiedName,
                Namespace = contract.Namespace,
                Name = contract.Name,
                Kind = symbolKind,
                DefinedByTaskId = "contract-generation",
                IsPublic = contract.AccessModifier == "public"
            });
        }
    }

    /// <summary>
    /// Generates a "Known Contracts" block for prompt injection.
    /// </summary>
    public string GenerateKnownContractsBlock(ContractCatalog catalog)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("/* FROZEN CONTRACTS - DO NOT REDEFINE OR MODIFY */");
        sb.AppendLine("/* Implementations MUST use these exact types and signatures */");
        sb.AppendLine();

        // Enums
        if (catalog.Enums.Any())
        {
            sb.AppendLine("// ENUMS (use these exact member names):");
            foreach (var e in catalog.Enums)
            {
                sb.AppendLine($"// - {e.FullyQualifiedName} {{ {string.Join(", ", e.Members.Select(m => m.Name))} }}");
            }
            sb.AppendLine();
        }

        // Interfaces
        if (catalog.Interfaces.Any())
        {
            sb.AppendLine("// INTERFACES (implement all methods with exact signatures):");
            foreach (var i in catalog.Interfaces)
            {
                sb.AppendLine($"// - {i.FullyQualifiedName}");
                foreach (var m in i.Methods)
                {
                    var parameters = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    sb.AppendLine($"//     {m.ReturnType} {m.Name}({parameters})");
                }
            }
            sb.AppendLine();
        }

        // Abstract classes
        if (catalog.AbstractClasses.Any())
        {
            sb.AppendLine("// ABSTRACT CLASSES (implement all abstract methods):");
            foreach (var a in catalog.AbstractClasses)
            {
                var sealedMarker = a.IsSealed ? " [SEALED - DO NOT INHERIT]" : "";
                sb.AppendLine($"// - {a.FullyQualifiedName}{sealedMarker}");
                foreach (var m in a.AbstractMethods)
                {
                    var parameters = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    sb.AppendLine($"//     abstract {m.ReturnType} {m.Name}({parameters})");
                }
            }
            sb.AppendLine();
        }

        // Models
        if (catalog.Models.Any())
        {
            sb.AppendLine("// MODELS/DTOs (use these exact property types):");
            foreach (var m in catalog.Models)
            {
                sb.AppendLine($"// - {m.FullyQualifiedName}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Cleans up JSON response by removing markdown code blocks.
    /// </summary>
    private string CleanJsonResponse(string content)
    {
        content = content.Trim();
        
        // Remove markdown code blocks
        if (content.StartsWith("```json"))
        {
            content = content.Substring(7);
        }
        else if (content.StartsWith("```"))
        {
            content = content.Substring(3);
        }
        
        if (content.EndsWith("```"))
        {
            content = content.Substring(0, content.Length - 3);
        }
        
        return content.Trim();
    }
}

// Analysis result classes for JSON deserialization
public class TypeAnalysisResult
{
    public List<EnumAnalysis> Enums { get; set; } = new();
    public List<InterfaceAnalysis> Interfaces { get; set; } = new();
    public List<ModelAnalysis> Models { get; set; } = new();
    public List<AbstractClassAnalysis> AbstractClasses { get; set; } = new();
}

public class EnumAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool IsFlags { get; set; }
}

public class InterfaceAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<MethodAnalysis> Methods { get; set; } = new();
    public List<PropertyAnalysis> Properties { get; set; } = new();
    public List<string> BaseInterfaces { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

public class ModelAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<PropertyAnalysis> Properties { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool IsRecord { get; set; }
    public string? BaseClass { get; set; }
    public List<string> ImplementedInterfaces { get; set; } = new();
}

public class AbstractClassAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<MethodAnalysis> AbstractMethods { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool IsSealed { get; set; }
    public string? BaseClass { get; set; }
    public List<string> ImplementedInterfaces { get; set; } = new();
}

public class MethodAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "void";
    public List<ParameterAnalysis> Parameters { get; set; } = new();
}

public class ParameterAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class PropertyAnalysis
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool HasSetter { get; set; } = true;
}
