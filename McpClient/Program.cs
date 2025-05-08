using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McpClient;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("MCP Issue Reproducible Example");
        Console.WriteLine("=============================");
        
        var serverUrl = "https://localhost:7269/mcp"; // Default port for the server project
        
        // Create a client that ignores SSL errors for local testing
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        var client = new HttpClient(handler);
        
        // First test: Try to list the available tools
        await TestListTools(client, serverUrl);
        
        // Second test: Try to call the Echo tool using different method formats
        await TestMethodFormats(client, serverUrl);
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    static async Task TestListTools(HttpClient client, string serverUrl)
    {
        Console.WriteLine("\nTesting list_tools method...");
        
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "list_tools"
        };
        
        await SendRequest(client, serverUrl, request);
    }
    
    static async Task TestMethodFormats(HttpClient client, string serverUrl)
    {
        // Try different method formats
        string[] methodFormats = {
            "Echo",                   // Direct method name
            "echo",                   // Lowercase method name
            "manual-echo",            // Manually registered echo tool
            "EchoTool.Echo",          // Class.Method format
            "tools/Echo",             // tools/ prefix
            "call_tool"               // Standard JSON-RPC format with name parameter
        };
        
        foreach (var methodName in methodFormats)
        {
            Console.WriteLine($"\nTesting method format: {methodName}");
            
            object requestObj;
            
            // Special case for call_tool format
            if (methodName == "call_tool")
            {
                requestObj = new
                {
                    jsonrpc = "2.0",
                    id = Guid.NewGuid().ToString(),
                    method = "call_tool",
                    @params = new 
                    {
                        name = "Echo",
                        arguments = new 
                        {
                            message = "Hello from call_tool format!"
                        }
                    }
                };
            }
            else
            {
                requestObj = new
                {
                    jsonrpc = "2.0",
                    id = Guid.NewGuid().ToString(),
                    method = methodName,
                    @params = new
                    {
                        message = $"Hello from {methodName} format!"
                    }
                };
            }
            
            await SendRequest(client, serverUrl, requestObj);
        }
    }
    
    static async Task SendRequest(HttpClient client, string serverUrl, object requestObj)
    {
        string jsonContent = JsonSerializer.Serialize(
            requestObj,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }
        );
        
        Console.WriteLine($"Sending request: {jsonContent}");
        
        var request = new HttpRequestMessage(HttpMethod.Post, serverUrl);
        request.Content = new StringContent(jsonContent, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("X-MCP-Stream-Id", Guid.NewGuid().ToString());
        
        try
        {
            var response = await client.SendAsync(request);
            Console.WriteLine($"Status: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response: {content}");
                
                // Print content in a more readable format
                if (content.StartsWith("event: message"))
                {
                    // Extract the JSON data part from event-stream format
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"data: (.+)");
                    if (match.Success)
                    {
                        var jsonData = match.Groups[1].Value.Trim();
                        try
                        {
                            // Try to parse and format the JSON
                            var jsonDoc = JsonDocument.Parse(jsonData);
                            var formatted = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                            Console.WriteLine($"Formatted JSON data:\n{formatted}");
                        }
                        catch
                        {
                            // If parsing fails, just show as is
                            Console.WriteLine($"Extracted data: {jsonData}");
                        }
                    }
                }
                
                // Check if the response indicates success
                if (!content.Contains("error"))
                {
                    Console.WriteLine();
                    Console.WriteLine("==================================================");
                    Console.WriteLine("SUCCESS! This method format works");
                    Console.WriteLine("==================================================");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("Request failed");
                if (response.Content != null)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {responseBody}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}