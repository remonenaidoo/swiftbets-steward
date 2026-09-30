using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Model;

/// <summary>
/// Steward's model calls through the official Anthropic SDK. The system prompt and the tool list are stable and
/// marked for caching; thinking blocks returned by the model are echoed back unchanged on the next turn.
/// </summary>
public sealed class AnthropicLanguageModel(AnthropicClient client, string model) : ILanguageModel
{
    public async Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var tools = request.Tools.Select((t, i) => (ToolUnion)ToTool(t, isLast: i == request.Tools.Count - 1)).ToList();
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = request.MaxTokens,
            System = new List<TextBlockParam> { new() { Text = request.System, CacheControl = new CacheControlEphemeral() } },
            Tools = tools,
            Messages = [.. request.Messages.Select(ToParam)],
        }, cancellationToken: cancellationToken);

        if (response.StopReason == "refusal")
        {
            return new ModelTurn(model, [ModelBlock.FromText("The model declined this request.")], "refusal", Usage(response));
        }

        var blocks = new List<ModelBlock>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                blocks.Add(ModelBlock.FromText(text.Text));
            }
            else if (block.TryPickToolUse(out ToolUseBlock? use))
            {
                blocks.Add(ModelBlock.ToolUse(use.ID, use.Name, JsonSerializer.Serialize(use.Input)));
            }
            else if (block.TryPickThinking(out ThinkingBlock? thinking))
            {
                blocks.Add(new ModelBlock(ModelBlockKind.Opaque, OpaqueJson: JsonSerializer.Serialize(new OpaqueThinking("thinking", thinking.Thinking, thinking.Signature, null))));
            }
            else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
            {
                blocks.Add(new ModelBlock(ModelBlockKind.Opaque, OpaqueJson: JsonSerializer.Serialize(new OpaqueThinking("redacted_thinking", null, null, redacted.Data))));
            }
        }

        return new ModelTurn(model, blocks, response.StopReason?.ToString() ?? "end_turn", Usage(response));
    }

    private static Tool ToTool(ToolSpec spec, bool isLast)
    {
        using var schema = JsonDocument.Parse(spec.InputSchemaJson);
        var root = schema.RootElement;
        return new Tool
        {
            Name = spec.Name,
            Description = spec.Description,
            Strict = true,
            InputSchema = new()
            {
                Properties = root.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
                Required = [.. root.GetProperty("required").EnumerateArray().Select(r => r.GetString()!)],
            },
            CacheControl = isLast ? new CacheControlEphemeral() : null,
        };
    }

    private static MessageParam ToParam(ModelMessage message)
    {
        var content = new List<ContentBlockParam>();
        foreach (var block in message.Blocks)
        {
            switch (block.Kind)
            {
                case ModelBlockKind.Text:
                    content.Add(new TextBlockParam { Text = block.Text! });
                    break;
                case ModelBlockKind.ToolUse:
                    content.Add(new ToolUseBlockParam
                    {
                        ID = block.ToolUseId!,
                        Name = block.ToolName!,
                        Input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(block.InputJson!)!,
                    });
                    break;
                case ModelBlockKind.ToolResult:
                    content.Add(new ToolResultBlockParam { ToolUseID = block.ToolUseId!, Content = block.Text!, IsError = block.IsError });
                    break;
                case ModelBlockKind.Opaque:
                    var opaque = JsonSerializer.Deserialize<OpaqueThinking>(block.OpaqueJson!)!;
                    content.Add(opaque.Type == "thinking"
                        ? new ThinkingBlockParam { Thinking = opaque.Thinking!, Signature = opaque.Signature! }
                        : new RedactedThinkingBlockParam { Data = opaque.Data! });
                    break;
            }
        }

        return new MessageParam { Role = message.IsAssistant ? Role.Assistant : Role.User, Content = content };
    }

    private static ModelUsage Usage(Message response) => new(
        response.Usage.InputTokens,
        response.Usage.OutputTokens,
        response.Usage.CacheReadInputTokens ?? 0,
        response.Usage.CacheCreationInputTokens ?? 0);

    private sealed record OpaqueThinking(string Type, string? Thinking, string? Signature, string? Data);
}
