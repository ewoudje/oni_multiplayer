using System;
using System.IO;
using MultiplayerMod.Core.Logging;

namespace MultiplayerMod.Multiplayer.Dev;

public class DevCommandListener {
    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<DevCommandListener>();
    public static DevCommandListener? ActivateDevCommands() {
        string[] args = Environment.GetCommandLineArgs();
        string? location = null;

        try {
            foreach (var arg in args) {
                if (arg.StartsWith("--devCommands=")) {
                    location = arg.Split('=')[1];
                }
            }
        } catch (Exception e) {
            log.Warning($"Invalid --devCommands ?\n{e}");
        }

        return location != null ? new DevCommandListener(location) : null;
    }

    private readonly FileSystemWatcher watcher;

    private DevCommandListener(string location) {
        var filter = Path.GetFileName(location)!;
        var directory = Path.GetDirectoryName(location)!;
        log.Info($"Using dev commands with location: {directory}\\{filter}");

        watcher = new FileSystemWatcher {
            Path = directory,
            Filter = filter,
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        watcher.Changed += CommandInput;
    }

    private void CommandInput(object sender, FileSystemEventArgs e) {
        var content = File.ReadAllText(e.FullPath).Trim();
        File.Delete(e.FullPath);

        log.Info($"Running command: {content}");
        RunCommand(content);
    }

    private void RunCommand(string content) {
        switch (content) {
            case "restart":
                App.instance.Restart();
                break;
            case "quit":
                App.Quit();
                break;
            default:
                throw new ArgumentException();
        }
    }
}
