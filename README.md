# MCP SDK Issue Reproduction

This minimal reproducible example demonstrates a critical issue with the Model Context Protocol (MCP) SDK version 0.1.0-preview.12.

## The Issue

The MCP server correctly registers tools (both via attributes and manual registration), but clients cannot call these tools. All method invocations result in a `-32601` error: "Method 'X' is not available."

## Steps to Reproduce

1. Open the solution in Visual Studio or another IDE
2. Set the McpServer project as the startup project
3. Run the server (observe the console logs to confirm tool registration)
4. Run the McpClient project in a separate instance
5. Observe that all method invocation attempts fail with "Method not available" errors

## Environment

- .NET 8
- ModelContextProtocol version 0.1.0-preview.12
- ModelContextProtocol.AspNetCore version 0.1.0-preview.12
- Windows/MacOS/Linux (specify your environment)

## Server Behavior

The server successfully:
1. Registers a static `EchoTool` class with the `[McpServerToolType]` attribute
2. Registers an `Echo` method with the `[McpServerTool]` attribute
3. Manually registers a `manual-echo` tool with explicit handlers
4. Logs all registration activity to the console

## Client Behavior

The client attempts to call the tools using various method formats:
1. Direct method name: `"Echo"`
2. Lowercase method name: `"echo"`
3. Manually registered tool: `"manual-echo"`
4. Class.Method format: `"EchoTool.Echo"`
5. tools/ prefix: `"tools/Echo"`
6. Standard JSON-RPC format: `"call_tool"` with a `name` parameter

All attempts result in "Method 'X' is not available" errors.

## Test Results

Every method format tested returns the exact same error:

```
Testing list_tools method...
{
  "error": {
    "code": -32601,
    "message": "Method 'list_tools' is not available."
  },
  "id": "467671ad-ecdc-40b2-8475-61e8c588bd2f",
  "jsonrpc": "2.0"
}

Testing method format: Echo
{
  "error": {
    "code": -32601,
    "message": "Method 'Echo' is not available."
  },
  "id": "f62e214a-4238-4fde-837b-58d38c792ead",
  "jsonrpc": "2.0"
}

Testing method format: echo
{
  "error": {
    "code": -32601,
    "message": "Method 'echo' is not available."
  },
  "id": "9bebc902-b051-4657-a725-b75dfed3fcc4",
  "jsonrpc": "2.0"
}

Testing method format: manual-echo
{
  "error": {
    "code": -32601,
    "message": "Method 'manual-echo' is not available."
  },
  "id": "e1b4af45-bd55-47dc-ab86-2ea347964c3e",
  "jsonrpc": "2.0"
}

Testing method format: EchoTool.Echo
{
  "error": {
    "code": -32601,
    "message": "Method 'EchoTool.Echo' is not available."
  },
  "id": "aae8f15e-66d2-4c98-ba11-aa631d79aa11",
  "jsonrpc": "2.0"
}

Testing method format: tools/Echo
{
  "error": {
    "code": -32601,
    "message": "Method 'tools/Echo' is not available."
  },
  "id": "03229baf-fa9d-4ded-8cf7-fb66d737ea51",
  "jsonrpc": "2.0"
}

Testing method format: call_tool
{
  "error": {
    "code": -32601,
    "message": "Method 'call_tool' is not available."
  },
  "id": "fb040c34-5aba-4ed0-b71b-7133026acb62",
  "jsonrpc": "2.0"
}
```

## Expected Behavior

At least one of the method invocation formats should succeed in calling the registered tools.

## Potential Root Causes

1. The SDK may have an issue linking the registered tools to the JSON-RPC handler.
2. There could be a configuration issue in how tools are exposed via the HTTP transport.
3. The preview version might have incomplete implementation of the JSON-RPC protocol.
4. There might be a missing component in the request/response pipeline.

## Additional Observations

1. The HTTP status code is 200 OK, indicating that the server receives and processes the requests.
2. The response format (event-stream with JSON data) appears correct.
3. The error is consistent across all method naming patterns.
4. Even the standard JSON-RPC method `list_tools` fails with the same error.

## Server Logs

The server logs confirm that tools are being registered correctly, but there appears to be a disconnect between registration and exposure via JSON-RPC.