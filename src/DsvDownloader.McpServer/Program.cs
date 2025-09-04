using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DsvDownloader.Core;
using System.IO;

// Minimal JSON-RPC 2.0 server over stdio, exposing a few tools.
// This is a lightweight implementation compatible with basic MCP-style clients
// for simple tool discovery and invocation.

var tools = new[]
{
    new Tool("validate_dsv_url", new { url = "string" }),
    new Tool("derive_filename", new { url = "string" }),
    new Tool("download_mp4", new { url = "string", destPath = "string", referer = "string?", userAgent = "string?" })
};

var downloadService = new DownloadService();
var stdin = Console.OpenStandardInput();
var stdout = Console.OpenStandardOutput();
var reader = new StreamReader(stdin, Encoding.UTF8);
var writer = new StreamWriter(stdout, new UTF8Encoding(false)) { AutoFlush = true };

await writer.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"ready\"}");

string? line;
while ((line = await reader.ReadLineAsync()) != null)
{
    if (string.IsNullOrWhiteSpace(line)) continue;
    try
    {
        var req = JsonSerializer.Deserialize<JsonRpcRequest>(line);
        if (req is null)
        {
            continue;
        }

        switch (req.Method)
        {
            case "tools/list":
                await RespondAsync(req.Id, new { tools = tools.Select(t => new { name = t.Name }) });
                break;

            case "tools/call":
                if (req.Params is null) { await ErrorAsync(req.Id, -32602, "Missing params"); break; }
                var p = req.Params.Value;
                var name = p.GetPropertyOrDefault<string>("name");
                if (string.IsNullOrWhiteSpace(name)) { await ErrorAsync(req.Id, -32602, "Missing tool name"); break; }
                if (name == "validate_dsv_url")
                {
                    var url = p.GetPropertyOrDefault<string>("url");
                    var ok = UrlValidator.TryValidate(url, out var reason);
                    await RespondAsync(req.Id, new { isValid = ok, reason });
                }
                else if (name == "derive_filename")
                {
                    var url = p.GetPropertyOrDefault<string>("url");
                    var fname = FileNameHelper.DeriveBaseNameFromUrl(url ?? string.Empty);
                    await RespondAsync(req.Id, new { safeFilename = fname });
                }
                else if (name == "download_mp4")
                {
                    var url = p.GetPropertyOrDefault<string>("url");
                    var dest = p.GetPropertyOrDefault<string>("destPath") ?? Directory.GetCurrentDirectory();
                    var res = await downloadService.DownloadAsync(url ?? string.Empty, dest);
                    await RespondAsync(req.Id, new { success = res.Success, filePath = res.FilePath, bytesWritten = res.Bytes, error = res.ErrorMessage });
                }
                else
                {
                    await ErrorAsync(req.Id, -32601, $"Unknown tool: {name}");
                }
                break;

            default:
                await ErrorAsync(req.Id, -32601, "Method not found");
                break;
        }
    }
    catch (Exception ex)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(new JsonRpcError
        {
            Jsonrpc = "2.0",
            Error = new JsonRpcErrorBody { Code = -32000, Message = ex.Message }
        }));
    }
}

async Task RespondAsync(object? id, object result)
{
    var resp = new JsonRpcResponse { Jsonrpc = "2.0", Id = id, Result = result };
    await writer.WriteLineAsync(JsonSerializer.Serialize(resp));
}

async Task ErrorAsync(object? id, int code, string message)
{
    var resp = new JsonRpcError { Jsonrpc = "2.0", Id = id, Error = new JsonRpcErrorBody { Code = code, Message = message } };
    await writer.WriteLineAsync(JsonSerializer.Serialize(resp));
}

record Tool(string Name, object InputSchema);

sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string? Jsonrpc { get; set; }
    [JsonPropertyName("id")] public object? Id { get; set; }
    [JsonPropertyName("method")] public string? Method { get; set; }
    [JsonPropertyName("params")] public JsonElement? Params { get; set; }
}

sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")] public string Jsonrpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public object? Id { get; set; }
    [JsonPropertyName("result")] public object? Result { get; set; }
}

sealed class JsonRpcError
{
    [JsonPropertyName("jsonrpc")] public string Jsonrpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public object? Id { get; set; }
    [JsonPropertyName("error")] public JsonRpcErrorBody Error { get; set; } = new();
}

sealed class JsonRpcErrorBody
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

static class JsonElementExtensions
{
    public static T? GetPropertyOrDefault<T>(this JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop)) return default;
        try
        {
            return prop.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }
}
