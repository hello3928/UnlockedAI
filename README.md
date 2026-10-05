# UnlockedAI

A native Windows chat client for local [Ollama](https://ollama.com) models, written in C# with WinUI 3.

- Streaming chat with markdown, tables and code blocks with a copy button
- Chats saved in SQLite, listed in a sidebar, with rename and delete
- A model picker; the model is remembered per chat
- File attachments: text and source code, PDF, Word
- Tools the model can call: web search, read a web page, web requests, date and system info, list a folder, read a file, write a file, run a PowerShell command
- Approval before anything changes the PC

## Requirements

- Windows 10 19041 or later
- .NET 10 SDK
- Ollama running on `http://localhost:11434` with at least one model pulled

## Build and run

```bash
dotnet build UnlockedAI.slnx -p:Platform=x64
```

```bash
dotnet run --project src/UnlockedAI.App
```

```bash
dotnet test --project tests/UnlockedAI.Tests
```

## Keyboard

| Keys | Action |
|---|---|
| Enter | Send |
| Shift+Enter | New line |
| Esc | Stop the reply |
| Ctrl+N | New chat |
| F2, Del | Rename or delete the selected chat |

## Tools

Tools are switched on and off with the **Tools** switch at the top of a chat. It is off by default: small local models that are offered tools use one for nearly every message, which makes ordinary chat slower and sends ordinary questions to a search engine. The switch is disabled for models that Ollama reports as unable to call tools.

Looking things up (searching, reading a public web page, listing a folder, reading a file) runs without asking. Running a command, writing a file, and any request to this PC or the local network wait for you, with the exact command or path shown first. Settings has an option to run everything without asking; while it is on, the chat header says so.

Web search uses DuckDuckGo with no setup. Paste the API key of a free ollama.com account into Settings and it uses Ollama Web Search instead. The key is kept in Windows Credential Manager.

To add a tool, write a class deriving from `ToolBase` and add it to `BuiltinTools.CreateRegistry`. Give every tool at least one parameter: a tool with an empty parameter list made the default model's calls to the other tools unreliable.

## Layout

| Project | Contents |
|---|---|
| `src/UnlockedAI.Core` | Everything without UI: errors, models, SQLite storage, the Ollama client, the chat loop, tools, search, file readers |
| `src/UnlockedAI.App` | The WinUI 3 app: theme tokens, reusable controls, views, view models |
| `tests/UnlockedAI.Tests` | xUnit tests for Core |

In the app project:

- `Theme/Tokens.xaml` is the only place raw colours, spacing, sizes and font sizes are written. Styles and controls read those keys.
- `Controls/` holds the reusable control classes (`AppButton`, `IconButton`, `Notice`, `ErrorMessage`, `Card`, `FormRow`, `Chip`, `CodeBlock`, `MarkdownView`, `ToolCallCard`, and so on). Views are built from these.
- Every failure becomes an `AppError` (in Core) and is shown with the `ErrorMessage` control.

## Data

Chats and settings are stored in `%LOCALAPPDATA%\UnlockedAI\unlockedai.db`. Only the text extracted from an attached file is stored, not the file. Set the `UNLOCKEDAI_DATA_DIR` environment variable to keep the data somewhere else. Unexpected errors are appended to `crash.log` in the same folder.
