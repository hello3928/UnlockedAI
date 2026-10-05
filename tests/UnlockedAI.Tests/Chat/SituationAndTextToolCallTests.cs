using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Models;
using UnlockedAI.Tests.Support;

namespace UnlockedAI.Tests.Chat;

public class SituationAndTextToolCallTests
{
    private static readonly string[] Offered = ["run_command", "web_search"];

    [Fact]
    public void Situation_gives_the_date_time_and_offset()
    {
        var text = Situation.Describe(FixedClock.Default, new AppSettings(), withTools: false);

        Assert.Equal("It is Monday, 9 March 2026, 14:30 (UTC+10:00).", text);
    }

    [Fact]
    public void Situation_adds_the_working_folder_only_when_tools_are_in_use()
    {
        var folder = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var text = Situation.Describe(FixedClock.Default, new AppSettings { WorkingDirectory = folder }, withTools: true);

        Assert.StartsWith("It is Monday, 9 March 2026, 14:30 (UTC+10:00).", text);
        Assert.Contains($"Commands start in {folder}", text);
    }

    [Fact]
    public void Situation_is_placed_just_before_the_newest_user_message()
    {
        ChatMessage[] history =
        [
            Message(1, ChatRole.User, "first"),
            Message(2, ChatRole.Assistant, "reply"),
            Message(3, ChatRole.User, "second"),
        ];

        var turns = ContextBuilder.Build("Be brief.", history, [], situation: "It is noon.");

        Assert.Equal(
            ["Be brief.", "first", "reply", "It is noon.", "second"],
            turns.Select(turn => turn.Content));
        Assert.Equal(ChatRole.System, turns[3].Role);
    }

    [Theory]
    [InlineData("""{"name": "run_command", "parameters": {"command": "dir"}}""")]
    [InlineData("""Answer: {"name": "run_command", "arguments": {"command": "dir"}}""")]
    [InlineData("""  {"name":"run_command","parameters":{"command":"dir"}}  """)]
    [InlineData("""
        Here's a JSON for a function call with its proper arguments that best answers the given prompt:
        {"name": "run_command", "parameters": {"command": "dir"}}
        """)]
    public void Tool_call_written_as_text_is_recognised(string reply)
    {
        Assert.True(TextToolCall.TryParse(reply, Offered, out var call));
        Assert.Equal("run_command", call.Name);
        Assert.Equal("""{"command": "dir"}""".Replace(" ", ""), call.ArgumentsJson.Replace(" ", ""));
    }

    [Theory]
    [InlineData("The answer is 42.")]
    [InlineData("""{"name": "delete_everything", "parameters": {}}""")]
    [InlineData("""{"name": "run_command"}""")]
    [InlineData("""{"name": "run_command", "parameters": "dir"}""")]
    [InlineData("""{"name": "run_command", "parameters": {"command": "dir"}""")]
    [InlineData("""
        Tools are called with a small piece of JSON. The name field says which tool you want, and the parameters
        field holds its arguments. Each tool documents the arguments it accepts, and anything missing is reported
        back to you as an error. For instance, listing a folder on Windows would look like this:
        {"name": "run_command", "parameters": {"command": "dir"}}
        """)]
    [InlineData("""{"name": "run_command", "parameters": {"command": "dir"}} and then tell me""")]
    public void Ordinary_replies_and_malformed_calls_are_left_alone(string reply)
    {
        Assert.False(TextToolCall.TryParse(reply, Offered, out _));
    }

    private static ChatMessage Message(long id, ChatRole role, string content) =>
        new(id, ConversationId: 1, Seq: (int)id, role, content, ToolName: null, ToolCalls: [], Attachments: [], DateTimeOffset.UnixEpoch);
}
