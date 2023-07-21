using System;
using Dalamud.Game.Command;

namespace NominaOcculta;

internal class Commands : IDisposable {
    private Plugin Plugin { get; }

    internal Commands(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.CommandManager.AddHandler("/occulta", new CommandInfo(this.OnCommand) {
            HelpMessage = "Toggle the Nomina Occulta interface",
        });
    }

    public void Dispose() {
        this.Plugin.CommandManager.RemoveHandler("/occulta");
    }

    private void OnCommand(string command, string arguments) {
        arguments = arguments.Trim();

        if (arguments.Length == 0) {
            this.Plugin.Ui.Visible ^= true;
            return;
        }

        var first = arguments.Split(' ', 2);
        bool? enable;
        switch (first[0]) {
            case "enable":
                enable = true;
                break;
            case "disable":
                enable = false;
                break;
            case "toggle":
                enable = null;
                break;
            case "reset":
                this.Plugin.NameRepository.Reset();
                return;
            default:
                this.Plugin.ChatGui.PrintError($"Invalid operation \"{first[0]}\", was expecting enable, disable, toggle, or reset.");
                return;
        }

        string? rest = null;
        if (first.Length > 1) {
            rest = first[1];
        }

        void Set(ref bool setting) {
            if (enable == null) {
                setting ^= true;
            } else {
                setting = enable.Value;
            }
        }

        switch (rest) {
            case null:
                Set(ref this.Plugin.Config.Enabled);
                break;
            case "self":
                Set(ref this.Plugin.Config.SelfFull);
                Set(ref this.Plugin.Config.SelfFirst);
                Set(ref this.Plugin.Config.SelfLast);
                break;
            case "self full":
                Set(ref this.Plugin.Config.SelfFull);
                break;
            case "self first":
                Set(ref this.Plugin.Config.SelfFirst);
                break;
            case "self last":
                Set(ref this.Plugin.Config.SelfLast);
                break;
            case "party":
                Set(ref this.Plugin.Config.Party);
                break;
            case "others":
                Set(ref this.Plugin.Config.Others);
                break;
            case "exclude friends":
                Set(ref this.Plugin.Config.ExcludeFriends);
                break;
            default:
                this.Plugin.ChatGui.PrintError($"Invalid option \"{rest}\".");
                return;
        }

        this.Plugin.SaveConfig();
    }
}
