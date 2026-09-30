namespace SwiftBets.Steward.Application.Model;

/// <summary>
/// One content block of a model conversation. <see cref="OpaqueJson"/> carries blocks the agent does not interpret
/// (thinking blocks) so they are echoed back byte-for-byte, as the API requires.
/// </summary>
public sealed record ModelBlock(ModelBlockKind Kind, string? Text = null, string? ToolUseId = null, string? ToolName = null, string? InputJson = null, bool IsError = false, string? OpaqueJson = null)
{
    public static ModelBlock FromText(string text) => new(ModelBlockKind.Text, Text: text);

    public static ModelBlock ToolUse(string id, string name, string inputJson) => new(ModelBlockKind.ToolUse, ToolUseId: id, ToolName: name, InputJson: inputJson);

    public static ModelBlock ToolResult(string toolUseId, string content, bool isError = false) => new(ModelBlockKind.ToolResult, Text: content, ToolUseId: toolUseId, IsError: isError);
}
