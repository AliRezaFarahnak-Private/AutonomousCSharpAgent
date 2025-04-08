using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents;
using ModelContextProtocol.SemanticKernel.Extensions;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

// Main async program
await StartAgentSelectionAsync();

async Task StartAgentSelectionAsync()
{
    // Dynamically find all *-agent.txt files in the current directory
    string currentDirectory = Directory.GetCurrentDirectory();
    string[] agentFiles = Directory.GetFiles(currentDirectory, "*-agent.txt")
        .Union(Directory.GetFiles(currentDirectory, "!*-agent.txt")).ToArray();

    // Display menu options
    for (int i = 0; i < agentFiles.Length; i++)
    {
        string fileName = Path.GetFileNameWithoutExtension(agentFiles[i]);
        // Clean up the display name (remove ! prefix if exists)
        string displayName = fileName.StartsWith("!") ? fileName.Substring(1) : fileName;
        Console.WriteLine($"{i + 1}. {displayName.Replace("-agent", "").ToUpperInvariant()} Agent");
    }
    
    Console.WriteLine("==============================================");
    Console.Write($"Select an agent to run (1-{agentFiles.Length}): ");

    string? choice = Console.ReadLine();
    int selectedIndex;
    
    if (!int.TryParse(choice, out selectedIndex) || selectedIndex < 1 || selectedIndex > agentFiles.Length)
    {
        Console.WriteLine($"\nInvalid selection. Defaulting to first agent.");
        selectedIndex = 1;
    }

    string selectedFile = agentFiles[selectedIndex - 1];
    string agentName = Path.GetFileNameWithoutExtension(selectedFile);
    agentName = agentName.StartsWith("!") ? agentName.Substring(1) : agentName;
    agentName = agentName.Replace("-agent", "").ToUpperInvariant();
    
    Console.WriteLine($"\nStarting {agentName} Agent...");
    await TriggerAgentAsync(selectedFile);
}

async Task TriggerAgentAsync(string instructionsFile)
{
    var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

    string modelName = config["AzureOpenAI:ModelName"];
    string endpoint = config["AzureOpenAI:Endpoint"];
    string apiKey = config["AzureOpenAI:ApiKey"];
    
    // Build kernel with Azure OpenAI
    var builder = Kernel.CreateBuilder();
    builder.Services
        .AddLogging()
        .AddAzureOpenAIChatCompletion(
            deploymentName: modelName,
            endpoint: endpoint,
            apiKey: apiKey);

    Kernel kernel = builder.Build();

    await kernel.Plugins.AddMcpFunctionsFromStdioServerAsync("Everything", new ()
    {
        ["command"] = "npx",
        ["arguments"] = "-y @playwright/mcp@latest --user-data-dir \"%LOCALAPPDATA%\\Google\\Chrome\\User Data\""
    });

    await kernel.Plugins.AddMcpFunctionsFromStdioServerAsync("Filesystem", new ()
    {
        ["command"] = "npx",
        ["arguments"] = "-y @modelcontextprotocol/server-filesystem C:\\Users\\alfarahn\\Desktop"
    });

    // Create the agent with auto-invoke capability
    ChatCompletionAgent agent = new()
    {
        Name = "AutonomousAgent",
        Instructions = "You are an Autonomous AI Agent.",
        Kernel = kernel,
        // Configure auto-invocation of functions
        Arguments = new KernelArguments(new PromptExecutionSettings() 
        { 
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        }),
    };

    // Create a thread to maintain conversation context
    AgentThread thread = new ChatHistoryAgentThread();

    // Process instructions file line by line
    await ProcessInstructionsFileAsync(agent, thread, instructionsFile);

    Console.WriteLine("\n");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ All instructions processed successfully!");
}

async Task ProcessInstructionsFileAsync(ChatCompletionAgent agent, AgentThread thread, string filePath)
{
    // Read all lines from the instructions file
    string[] instructionLines = await File.ReadAllLinesAsync(filePath);
    int lineNumber = 0;
    
    foreach (string line in instructionLines)
    {
        lineNumber++;
        
        // Skip empty lines or comment lines
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
        {
            continue;
        }
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[Line {lineNumber}] Executing: {line}");
        Console.ResetColor();
        
        // Create a message for this instruction line
        ChatMessageContent message = new(AuthorRole.User, line);
        
        // Write user message to console
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"User: {message.Content}");
        Console.ResetColor();
        
        // Execute the instruction with the agent (auto-invokes tools as needed)
        await foreach (ChatMessageContent response in agent.InvokeAsync(message, thread))
        {
            if (response.Content != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Agent: {response.Content}");
                Console.ResetColor();
            }
        }
    }

    string html = @"C:\Users\alfarahn\Desktop\ai.html";

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = html,
        UseShellExecute = true
    };
    
    Process.Start(psi);



}

