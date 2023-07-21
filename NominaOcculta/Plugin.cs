using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using XivCommon;
using XivCommon.Functions.FriendList;
using Condition = Dalamud.Game.ClientState.Conditions.Condition;

namespace NominaOcculta;

public class Plugin : IDalamudPlugin {
    public string Name => "Nomina Occulta";

    [PluginService]
    internal DalamudPluginInterface Interface { get; private init; }

    [PluginService]
    internal ChatGui ChatGui { get; private init; }

    [PluginService]
    internal ClientState ClientState { get; private init; }

    [PluginService]
    internal CommandManager CommandManager { get; private init; }

    [PluginService]
    internal Condition Condition { get; private set; }

    [PluginService]
    internal DataManager DataManager { get; private init; }

    [PluginService]
    internal Framework Framework { get; private init; }

    [PluginService]
    internal KeyState KeyState { get; private init; }

    [PluginService]
    internal PartyList PartyList { get; private init; }

    [PluginService]
    internal TargetManager TargetManager { get; private init; }

    [PluginService]
    internal ObjectTable ObjectTable { get; private init; }

    internal XivCommonBase Common { get; }
    internal GameFunctions Functions { get; }

    internal Configuration Config { get; }
    private Commands Commands { get; }
    internal NameRepository NameRepository { get; }
    internal AppearanceRepository AppearanceRepository { get; }
    private Obscurer Obscurer { get; }
    internal PluginUi Ui { get; }

    #pragma warning disable 8618
    public Plugin() {
        this.Common = new XivCommonBase(Hooks.NamePlates);
        this.Functions = new GameFunctions(this);

        this.Config = this.Interface!.GetPluginConfig() as Configuration ?? new Configuration();
        this.Ui = new PluginUi(this);
        this.NameRepository = new NameRepository(this);
        this.AppearanceRepository = new AppearanceRepository(this);
        this.Obscurer = new Obscurer(this);
        this.Commands = new Commands(this);
    }
    #pragma warning restore 8618

    public void Dispose() {
        this.Commands.Dispose();
        this.Obscurer.Dispose();
        this.NameRepository.Dispose();
        this.Ui.Dispose();

        this.Functions.Dispose();
        this.Common.Dispose();
    }

    internal void SaveConfig() {
        this.Interface.SavePluginConfig(this.Config);
    }
}
