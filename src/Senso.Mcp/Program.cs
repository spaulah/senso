using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Senso.Mcp;
using Senso.Mcp.Input;
using Senso.Mcp.Ocr;
using Senso.Mcp.Resources;
using Senso.Mcp.Speech;
using Senso.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// stdout is the MCP protocol channel: all logs go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.Configure<SensoOptions>(builder.Configuration.GetSection(SensoOptions.SectionName));
builder.Services.AddSingleton<MediaInputResolver>();
builder.Services.AddHttpClient<IOcrClient, AzureVisionOcrClient>(c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient<ISpeechClient, AzureOpenAiSpeechClient>(c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddTransient<OcrService>();
builder.Services.AddTransient<TranscriptionService>();
builder.Services.AddSingleton<ToolRunner>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SensoTools>()
    .WithResources<StatusResource>();

await builder.Build().RunAsync();
