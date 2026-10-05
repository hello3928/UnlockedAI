using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Tests.Chat;

public class ContextBuilderTests
{
    [Fact]
    public void System_prompt_comes_first_and_is_left_out_when_blank()
    {
        ChatMessage[] history = [Message(1, ChatRole.User, "hi")];

        var withPrompt = ContextBuilder.Build("Be brief.", history, []);
        var withoutPrompt = ContextBuilder.Build("   ", history, []);

        Assert.Equal([ChatRole.System, ChatRole.User], withPrompt.Select(turn => turn.Role));
        Assert.Equal("Be brief.", withPrompt[0].Content);
        Assert.Equal(ChatRole.User, Assert.Single(withoutPrompt).Role);
    }

    [Fact]
    public void Attached_files_are_inlined_before_the_message_they_belong_to()
    {
        ChatMessage[] history =
        [
            Message(1, ChatRole.User, "What does it say?", new Attachment(10, "notes.txt", "text", 5, false)),
            Message(2, ChatRole.Assistant, "It says hello."),
            Message(3, ChatRole.User, "Thanks"),
        ];
        AttachmentText[] files = [new(MessageId: 1, "notes.txt", Truncated: false, "hello")];

        var turns = ContextBuilder.Build("", history, files);

        Assert.Equal(
            """
            [File: notes.txt]
            hello
            [End of file: notes.txt]

            What does it say?
            """.ReplaceLineEndings(),
            turns[0].Content.ReplaceLineEndings());
        Assert.Equal("Thanks", turns[2].Content);
    }

    [Fact]
    public void Truncated_files_say_so()
    {
        ChatMessage[] history = [Message(1, ChatRole.User, "Summarise", new Attachment(10, "big.pdf", "pdf", 9_000_000, true))];
        AttachmentText[] files = [new(1, "big.pdf", Truncated: true, "first pages")];

        var content = ContextBuilder.Build("", history, files)[0].Content;

        Assert.StartsWith("[File: big.pdf (truncated, only the first part is included)]", content);
    }

    [Fact]
    public void Tool_calls_and_tool_results_are_carried_through()
    {
        var call = new ToolCall("web_search", """{"query":"x"}""");
        ChatMessage[] history =
        [
            Message(1, ChatRole.Assistant, "") with { ToolCalls = [call] },
            Message(2, ChatRole.Tool, "result") with { ToolName = "web_search" },
        ];

        var turns = ContextBuilder.Build("", history, [], withTools: true);

        Assert.Equal(ContextBuilder.ToolGuidance, turns[0].Content);
        Assert.Equal(call, Assert.Single(turns[1].ToolCalls!));
        Assert.Equal("web_search", turns[2].ToolName);
    }

    [Fact]
    public void Tool_guidance_is_added_after_the_users_own_system_prompt()
    {
        var turns = ContextBuilder.Build("Be brief.", [Message(1, ChatRole.User, "hi")], [], withTools: true);

        Assert.Equal($"Be brief.\n\n{ContextBuilder.ToolGuidance}", turns[0].Content);
    }

    [Fact]
    public void Earlier_tool_use_is_left_out_for_a_model_without_tools()
    {
        ChatMessage[] history =
        [
            Message(1, ChatRole.User, "what time is it?"),
            Message(2, ChatRole.Assistant, "") with { ToolCalls = [new ToolCall("system_info", "{}")] },
            Message(3, ChatRole.Tool, "14:30") with { ToolName = "system_info" },
            Message(4, ChatRole.Assistant, "It is 14:30."),
        ];

        var turns = ContextBuilder.Build("", history, [], withTools: false);

        Assert.Equal(["what time is it?", "It is 14:30."], turns.Select(turn => turn.Content));
        Assert.All(turns, turn => Assert.Null(turn.ToolCalls));
    }

    private static ChatMessage Message(long id, ChatRole role, string content, params Attachment[] attachments) =>
        new(id, ConversationId: 1, Seq: (int)id, role, content, ToolName: null, ToolCalls: [], attachments, DateTimeOffset.UnixEpoch);
}
