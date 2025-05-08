using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpServer;

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool]
    [Description("Echoes the message back to the client.")]
    public static string Echo(string message)
    {
        Console.WriteLine($"Echo method called with: {message}");
        return $"hello {message}";
    }
}