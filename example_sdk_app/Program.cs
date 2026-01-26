using GitHub.Copilot.SDK;

// Check if server URL is provided as command-line argument
// Usage: dotnet run [server-url] [prompt...]
// Examples:
//   dotnet run localhost:35137 "What is async/await?"
//   dotnet run "" "What is 2+2?"  (local mode with custom prompt)
string? serverUrl = args.Length > 0 && !string.IsNullOrEmpty(args[0]) ? args[0] : null;

// Create client - either connect to existing server or start new one
CopilotClient client;
if (serverUrl != null)
{
    Console.WriteLine($"🔗 Connecting to Copilot server at {serverUrl}...");
    // When using CliUrl, create a completely new options object with ONLY CliUrl set
    // Don't use object initializer that might include default values
    var options = new CopilotClientOptions();
    options.CliUrl = serverUrl;
    // Explicitly set UseStdio to false when using CliUrl
    options.UseStdio = false;
    client = new CopilotClient(options);
}
else
{
    Console.WriteLine("🚀 Starting new Copilot client (local mode)...");
    client = new CopilotClient();
}

await using var _ = client;

// Create session
Console.WriteLine("📝 Creating session...");
await using var session = await client.CreateSessionAsync(new SessionConfig { Model = "gpt-4.1" });

// Get prompt from command-line or use default
string prompt = args.Length > 1 ? string.Join(" ", args[1..]) : "What is 2 + 2?";

Console.WriteLine($"💭 Prompt: {prompt}");
Console.WriteLine();

// Send prompt and get response
var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });

Console.WriteLine("🤖 Response:");
Console.WriteLine(response?.Data.Content);