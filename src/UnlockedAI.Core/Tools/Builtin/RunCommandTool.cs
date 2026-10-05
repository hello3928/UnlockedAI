using System.Diagnostics;
using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Data;

namespace UnlockedAI.Core.Tools.Builtin;

/// <summary>
/// Runs a PowerShell command. The command gets a time limit, its output is capped while it is
/// still running, and stopping the reply (or hitting the limit) ends the whole process tree.
/// </summary>
public sealed class RunCommandTool(SettingsService settings) : ToolBase
{
    // Makes PowerShell emit UTF-8 and keeps progress bars out of the captured output.
    private const string Preamble =
        "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $ProgressPreference = 'SilentlyContinue'; ";

    private readonly TimeSpan? _timeoutOverride;

    /// <param name="timeoutOverride">Replaces the time limit from Settings. Used by tests.</param>
    internal RunCommandTool(SettingsService settings, TimeSpan timeoutOverride)
        : this(settings)
    {
        _timeoutOverride = timeoutOverride;
    }

    public override string Name => "run_command";

    public override string Title => "Run command";

    public override string Summary => "Run a PowerShell command on this PC and read what it prints.";

    public override string Description =>
        "Run a Windows PowerShell command on the user's PC and return its output and exit code. "
        + "It runs non-interactively, so it must not wait for input.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["command"],
          "properties": {
            "command": { "type": "string", "description": "The PowerShell command to run." }
          }
        }
        """;

    public override ToolRisk RiskFor(JsonElement arguments) => ToolRisk.ChangesPc;

    public override string Describe(JsonElement arguments) => Text(arguments, "command");

    protected override async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var command = RequiredText(arguments, "command");
        var current = settings.Current;
        var timeout = _timeoutOverride ?? TimeSpan.FromSeconds(current.CommandTimeoutSeconds);

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            WorkingDirectory = ToolPaths.WorkingDirectory(current),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-OutputFormat");
        startInfo.ArgumentList.Add("Text");
        // Base64 of UTF-16, which is what PowerShell expects; no quoting to get wrong.
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(Preamble + command)));

        var output = new CappedOutput(MaxOutputChars);
        var errors = new CappedOutput(MaxOutputChars);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => output.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => errors.AppendLine(e.Data);

        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            cancellationToken.ThrowIfCancellationRequested();

            throw new ToolException(
                $"The command was stopped after running for {timeout.TotalSeconds:0} seconds, the time limit set in Settings."
                + $"\n\nOutput before it was stopped:\n{Combine(output, errors)}");
        }

        return $"Exit code: {process.ExitCode}\n\n{Combine(output, errors)}";
    }

    private static string Combine(CappedOutput output, CappedOutput errors)
    {
        var errorText = PowerShellErrors.Decode(errors.ToString());
        if (errorText.Length == 0)
        {
            return output.IsEmpty ? "(no output)" : output.ToString();
        }

        return output.IsEmpty ? $"Errors:\n{errorText}" : $"{output}\n\nErrors:\n{errorText}";
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It exited on its own between the timeout and the kill.
        }
    }

    /// <summary>Collects output lines from two streams, keeping only the first <paramref name="limit"/> characters.</summary>
    private sealed class CappedOutput(int limit)
    {
        private readonly StringBuilder _text = new();
        private readonly Lock _gate = new();
        private long _dropped;

        public void AppendLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_gate)
            {
                var room = limit - _text.Length;
                if (room <= 0)
                {
                    _dropped += line.Length + 1;
                    return;
                }

                if (line.Length > room)
                {
                    _text.Append(line, 0, room);
                    _dropped += line.Length - room;
                }
                else
                {
                    _text.Append(line);
                }

                _text.Append('\n');
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_gate)
                {
                    return _text.Length == 0;
                }
            }
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return _dropped == 0
                    ? _text.ToString().TrimEnd()
                    : $"{_text.ToString().TrimEnd()}\n[Output cut here. About {_dropped:N0} more characters were not kept.]";
            }
        }
    }
}
