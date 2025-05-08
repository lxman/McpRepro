using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Trace);

var logger = LoggerFactory.Create(config => 
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Trace);
}).CreateLogger("McpServer");

// Configure MCP server
logger.LogInformation("Configuring MCP server...");

// Try both registration approaches
try {
    // Approach 1: Automatic tool discovery
    logger.LogInformation("Setting up automatic tool discovery");
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly(typeof(Program).Assembly);
        
    // Approach 2: Manual tool registration
    logger.LogInformation("Setting up manual tool registration");
    builder.Services.Configure<McpServerOptions>(options => 
    {
        logger.LogInformation("Configuring McpServerOptions");
        
        options.ServerInfo = new Implementation
        {
            Name = "McpReproServer",
            Version = "1.0.0"
        };
        
        options.Capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability
            {
                ListToolsHandler = (request, cancellationToken) => 
                {
                    logger.LogInformation("ListToolsHandler called");
                    return new ValueTask<ListToolsResult>(Task.FromResult(new ListToolsResult
                    {
                        Tools = new List<Tool>
                        {
                            new Tool
                            {
                                Name = "manual-echo",
                                Description = "Manually registered echo tool",
                                InputSchema = JsonSerializer.Deserialize<JsonElement>("""
                                    {
                                        "type": "object",
                                        "properties": {
                                            "message": {
                                                "type": "string",
                                                "description": "The message to echo back"
                                            }
                                        },
                                        "required": ["message"]
                                    }
                                    """)
                            }
                        }
                    }));
                },
                
                CallToolHandler = (request, cancellationToken) =>
                {
                    logger.LogInformation("CallToolHandler called for tool: {ToolName}", request.Params?.Name);
                    
                    if (request.Params?.Name == "manual-echo")
                    {
                        if (request.Params.Arguments?.TryGetValue("message", out var message) != true)
                        {
                            throw new McpException("Missing required argument 'message'");
                        }
                        
                        string response = $"Manual echo: {message}";
                        logger.LogInformation("Responding with: {Response}", response);
                        
                        return new ValueTask<CallToolResponse>(Task.FromResult(new CallToolResponse
                        {
                            Content = new List<Content>
                            {
                                new Content { Type = "text", Text = response }
                            }
                        }));
                    }
                    
                    logger.LogWarning("Tool not found: {ToolName}", request.Params?.Name);
                    throw new McpException($"Tool '{request.Params?.Name}' not found");
                }
            }
        };
    });
}
catch (Exception ex) {
    logger.LogError(ex, "Error configuring MCP server");
}

// Log all registered tools
try {
    logger.LogInformation("Logging all registered tools...");
    
    var toolTypes = typeof(Program).Assembly.GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), true).Any())
        .ToList();
    
    logger.LogInformation("Found {Count} types with McpServerToolType attribute:", toolTypes.Count);
    foreach (var type in toolTypes)
    {
        logger.LogInformation("- {TypeName}", type.FullName);
        
        var methods = type.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), true).Any())
            .ToList();
        
        logger.LogInformation("  Found {Count} methods with McpServerTool attribute:", methods.Count);
        foreach (var method in methods)
        {
            logger.LogInformation("  - {MethodName}", method.Name);
            
            var parameters = method.GetParameters();
            logger.LogInformation("    Parameters: {Parameters}", 
                string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}")));
            
            logger.LogInformation("    Return type: {ReturnType}", method.ReturnType.Name);
        }
    }
}
catch (Exception ex) {
    logger.LogError(ex, "Error logging tools");
}

// Build the application
var app = builder.Build();

// Debug middleware to log all requests
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/mcp")
    {
        logger.LogInformation("Received request to MCP endpoint: {Method}", context.Request.Method);
        logger.LogInformation("Headers:");
        foreach (var header in context.Request.Headers)
        {
            logger.LogInformation("- {Key}: {Value}", header.Key, string.Join(", ", header.Value));
        }
        
        context.Request.EnableBuffering();
        using (var reader = new StreamReader(
            context.Request.Body,
            encoding: System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            logger.LogInformation("Request body: {Body}", body);
            context.Request.Body.Position = 0;
        }
    }
    
    await next();
    
    if (context.Request.Path == "/mcp")
    {
        logger.LogInformation("Response status: {StatusCode}", context.Response.StatusCode);
        logger.LogInformation("Response headers:");
        foreach (var header in context.Response.Headers)
        {
            logger.LogInformation("- {Key}: {Value}", header.Key, string.Join(", ", header.Value));
        }
    }
});

// Map the MCP endpoint
try {
    logger.LogInformation("Mapping MCP endpoint to /mcp...");
    app.MapMcp("/mcp");
    logger.LogInformation("MCP endpoint mapped successfully");
}
catch (Exception ex) {
    logger.LogError(ex, "Error mapping MCP endpoint");
}

// Add minimal API endpoint to verify server is running
app.MapGet("/", () => "MCP Repro Server is running. See console for logs.");

app.Run();