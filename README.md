# UnlockedAI

A native Windows chat client for local [Ollama](https://ollama.com) models, written in C# with WinUI 3.

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

## Layout

| Project | Contents |
|---|---|
| `src/UnlockedAI.Core` | Everything without UI: errors, models, SQLite storage, Ollama client, chat loop, tools, file readers |
| `src/UnlockedAI.App` | WinUI 3 app: theme tokens, reusable controls, views, view models |
| `tests/UnlockedAI.Tests` | xUnit tests for Core |

Chats and settings are stored in `%LOCALAPPDATA%\UnlockedAI\unlockedai.db`.
