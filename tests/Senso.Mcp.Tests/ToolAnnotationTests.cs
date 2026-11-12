using System.Reflection;
using ModelContextProtocol.Server;
using Senso.Mcp.Tools;
using Xunit;

namespace Senso.Mcp.Tests;

// Guards the MCP directory requirement that every tool declares all four annotation hints explicitly.
public class ToolAnnotationTests
{
    private static readonly string[] RequiredHints = { "ReadOnly", "Destructive", "Idempotent", "OpenWorld" };

    private static IEnumerable<MethodInfo> RegisteredToolMethods() =>
        typeof(SensoTools).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

    [Fact]
    public void Every_tool_is_discovered()
    {
        var names = RegisteredToolMethods().Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name).ToList();
        Assert.Contains("ocr_image", names);
        Assert.Contains("transcribe_audio", names);
    }

    [Fact]
    public void Every_tool_declares_all_four_hints_explicitly_in_source()
    {
        foreach (var method in RegisteredToolMethods())
        {
            var attr = method.GetCustomAttributesData()
                .Single(d => d.AttributeType == typeof(McpServerToolAttribute));
            var declared = attr.NamedArguments.Select(a => a.MemberName).ToHashSet();

            foreach (var hint in RequiredHints)
                Assert.True(declared.Contains(hint),
                    $"Tool '{method.Name}' does not set {hint} explicitly on [McpServerTool].");
        }
    }

    [Fact]
    public void Every_tool_emits_all_four_hints_in_its_protocol_metadata()
    {
        var sensoTools = new SensoTools(null!, null!, null!);
        foreach (var method in RegisteredToolMethods())
        {
            var tool = McpServerTool.Create(method, method.IsStatic ? null : sensoTools);
            var annotations = tool.ProtocolTool.Annotations;

            Assert.NotNull(annotations);
            Assert.True(annotations!.ReadOnlyHint.HasValue, $"{tool.ProtocolTool.Name}: readOnlyHint missing");
            Assert.True(annotations.DestructiveHint.HasValue, $"{tool.ProtocolTool.Name}: destructiveHint missing");
            Assert.True(annotations.IdempotentHint.HasValue, $"{tool.ProtocolTool.Name}: idempotentHint missing");
            Assert.True(annotations.OpenWorldHint.HasValue, $"{tool.ProtocolTool.Name}: openWorldHint missing");
        }
    }
}
