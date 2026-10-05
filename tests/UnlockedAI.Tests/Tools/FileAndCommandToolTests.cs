using System.Diagnostics;
using System.Text.Json;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Files;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Tools;
using UnlockedAI.Core.Tools.Builtin;

namespace UnlockedAI.Tests.Tools;

public sealed class FileAndCommandToolTests : IAsyncLifetime
{
    private readonly string _folder = Directory.CreateTempSubdirectory("unlockedai-tools-").FullName;
    private readonly Database _database = Database.OpenInMemory();
    private readonly SettingsService _settings;

    public FileAndCommandToolTests() => _settings = new SettingsService(new SettingsRepository(_database));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Relative paths and commands in these tests run inside the temporary folder.
    public async ValueTask InitializeAsync() =>
        await _settings.SaveAsync(new AppSettings { WorkingDirectory = _folder }, Ct);

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        Directory.Delete(_folder, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Write_creates_a_file_then_reports_replacing_it()
    {
        var tool = new WriteFileTool(_settings);

        var created = await RunAsync(tool, new { path = "sub/note.txt", content = "first" });
        var replaced = await RunAsync(tool, new { path = "sub/note.txt", content = "second" });

        Assert.StartsWith("Created", created.Output);
        Assert.StartsWith("Replaced", replaced.Output);
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(_folder, "sub", "note.txt"), Ct));
    }

    [Fact]
    public async Task Write_can_append()
    {
        var tool = new WriteFileTool(_settings);
        await RunAsync(tool, new { path = "log.txt", content = "one\n" });

        await RunAsync(tool, new { path = "log.txt", content = "two\n", append = true });

        Assert.Equal("one\ntwo\n", await File.ReadAllTextAsync(Path.Combine(_folder, "log.txt"), Ct));
    }

    [Fact]
    public async Task Write_can_change_one_exact_piece_of_text()
    {
        var path = Path.Combine(_folder, "config.ini");
        await File.WriteAllTextAsync(path, "mode=slow\nname=demo\n", Ct);

        var result = await RunAsync(new WriteFileTool(_settings), new { path, find = "mode=slow", replace_with = "mode=fast" });

        Assert.Equal(ToolOutcome.Succeeded, result.Outcome);
        Assert.Equal("mode=fast\nname=demo\n", await File.ReadAllTextAsync(path, Ct));
    }

    [Theory]
    [InlineData("missing", "wasn't found")]
    [InlineData("a", "more than once")]
    public async Task Change_fails_unless_the_text_matches_exactly_one_place(string find, string expectedReason)
    {
        var path = Path.Combine(_folder, "data.txt");
        await File.WriteAllTextAsync(path, "a b a", Ct);

        var result = await RunAsync(new WriteFileTool(_settings), new { path, find, replace_with = "x" });

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Contains(expectedReason, result.Output);
        Assert.Equal("a b a", await File.ReadAllTextAsync(path, Ct));
    }

    [Fact]
    public void Write_always_needs_approval_and_says_what_it_will_do()
    {
        var tool = new WriteFileTool(_settings);
        using var arguments = JsonSerializer.SerializeToDocument(new { path = "a.txt", content = "x" });

        Assert.Equal(ToolRisk.ChangesPc, tool.RiskFor(arguments.RootElement));
        Assert.Equal("Write a.txt", tool.Describe(arguments.RootElement));
    }

    [Fact]
    public async Task Read_returns_the_text_of_a_file_given_a_relative_path()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "hello.txt"), "hello there", Ct);

        var result = await RunAsync(new ReadFileTool(_settings, new AttachmentService()), new { path = "hello.txt" });

        Assert.Equal("hello there", result.Output);
    }

    [Fact]
    public async Task Read_explains_a_missing_file_and_a_folder()
    {
        var tool = new ReadFileTool(_settings, new AttachmentService());

        var missing = await RunAsync(tool, new { path = "nope.txt" });
        var folder = await RunAsync(tool, new { path = _folder });

        Assert.Equal(ToolOutcome.Failed, missing.Outcome);
        Assert.Contains("doesn't exist", missing.Output);
        Assert.Contains("list_directory", folder.Output);
    }

    [Fact]
    public async Task List_shows_folders_first_then_files_with_sizes()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "zeta"));
        await File.WriteAllTextAsync(Path.Combine(_folder, "alpha.txt"), new string('x', 2048), Ct);

        var result = await RunAsync(new ListDirectoryTool(_settings), new { });

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal($"Contents of {_folder}:", lines[0]);
        Assert.Equal(["[folder] zeta", "alpha.txt  (2 KB)"], lines[1..]);
    }

    [Fact]
    public async Task List_says_when_a_folder_is_missing()
    {
        var result = await RunAsync(new ListDirectoryTool(_settings), new { path = "nowhere" });

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Contains("doesn't exist", result.Output);
    }

    [Fact]
    public async Task Command_output_and_exit_code_come_back_and_it_runs_in_the_working_folder()
    {
        var result = await RunAsync(new RunCommandTool(_settings), new { command = "Write-Output \"in $((Get-Location).Path)\"; exit 3" });

        Assert.Equal(ToolOutcome.Succeeded, result.Outcome);
        Assert.StartsWith("Exit code: 3", result.Output);
        Assert.Contains($"in {_folder}", result.Output);
    }

    [Fact]
    public async Task Command_errors_are_captured_as_readable_text()
    {
        var result = await RunAsync(new RunCommandTool(_settings), new { command = "Get-Item definitely-not-here" });

        Assert.Contains("definitely-not-here", result.Output);
        Assert.DoesNotContain("CLIXML", result.Output);
    }

    [Fact]
    public async Task Command_over_the_time_limit_is_stopped_with_its_whole_process_tree()
    {
        var tool = new RunCommandTool(_settings, timeoutOverride: TimeSpan.FromSeconds(2));
        var marker = $"unlockedai-test-{Guid.NewGuid():N}";
        var watch = Stopwatch.StartNew();

        // The outer PowerShell starts a second one; both must be gone afterwards.
        var result = await RunAsync(tool, new { command = $"Write-Output 'started'; powershell -NoProfile -Command \"$t='{marker}'; Start-Sleep 60\"" });

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Contains("stopped after running for 2 seconds", result.Output);
        Assert.Contains("started", result.Output);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"took {watch.Elapsed}");
        Assert.Empty(ProcessesWithCommandLine(marker));
    }

    [Fact]
    public async Task Stopping_the_reply_ends_a_running_command()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        stop.CancelAfter(TimeSpan.FromSeconds(1));
        using var arguments = JsonSerializer.SerializeToDocument(new { command = "Start-Sleep 60" });
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new RunCommandTool(_settings).RunAsync(arguments.RootElement, stop.Token));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task Huge_command_output_is_cut_instead_of_filling_memory()
    {
        var result = await RunAsync(new RunCommandTool(_settings), new { command = "1..20000 | ForEach-Object { 'line of output number ' + $_ }" });

        Assert.True(result.Output.Length < ToolBase.MaxOutputChars + 500, $"length {result.Output.Length}");
        Assert.Contains("Output cut here", result.Output);
    }

    private static async Task<ToolResult> RunAsync(ITool tool, object arguments)
    {
        using var document = JsonSerializer.SerializeToDocument(arguments);
        return await tool.RunAsync(document.RootElement, Ct);
    }

    private static List<string> ProcessesWithCommandLine(string marker)
    {
        // Asks Windows for the command lines of running PowerShell processes.
        var query = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        query.ArgumentList.Add("-NoProfile");
        query.ArgumentList.Add("-Command");
        query.ArgumentList.Add($"Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\" | Where-Object {{ $_.CommandLine -like '*{marker}*' -and $_.ProcessId -ne $PID }} | ForEach-Object {{ $_.ProcessId }}");

        using var process = Process.Start(query)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
