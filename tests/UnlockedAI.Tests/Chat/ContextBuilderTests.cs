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

        var turns = ContextBuilder.Build("", history, []);

        Assert.Equal(call, Assert.Single(turns[0].ToolCalls!));
        Assert.Equal("web_search", turns[1].ToolName);
    }

    private static ChatMessage Message(long id, ChatRole role, string content, params Attachment[] attachments) =>
        new(id, ConversationId: 1, Seq: (int)id, role, content, ToolName: null, ToolCalls: [], attachments, DateTimeOffset.UnixEpoch);
}
