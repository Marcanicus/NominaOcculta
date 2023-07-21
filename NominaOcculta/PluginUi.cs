using System;
using Dalamud.Game.ClientState.Keys;
using ImGuiNET;
using Lumina.Data.Parsing.Uld;
using Lumina.Excel.GeneratedSheets;

namespace NominaOcculta;

internal class PluginUi : IDisposable {
    private Plugin Plugin { get; }

    internal bool Visible;
    private bool _debug;
    private int _queueColumns = 2;

    internal PluginUi(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.Interface.UiBuilder.Draw += this.Draw;
        this.Plugin.Interface.UiBuilder.OpenConfigUi += this.OpenConfig;
    }

    public void Dispose() {
        this.Plugin.Interface.UiBuilder.OpenConfigUi -= this.OpenConfig;
        this.Plugin.Interface.UiBuilder.Draw -= this.Draw;
    }

    private void OpenConfig() {
        this.Visible = true;
    }

    private void Draw() {
        if (!this.Visible) {
            return;
        }

        if (!ImGui.Begin(this.Plugin.Name, ref this.Visible)) {
            ImGui.End();
            return;
        }

        var anyChanged = ImGui.Checkbox("Enabled", ref this.Plugin.Config.Enabled);
        ImGui.Separator();
        anyChanged |= ImGui.Checkbox("Obscure self (full name)", ref this.Plugin.Config.SelfFull);
        ImGui.TreePush();
        anyChanged |= ImGui.Checkbox("First name", ref this.Plugin.Config.SelfFirst);
        anyChanged |= ImGui.Checkbox("Last name", ref this.Plugin.Config.SelfLast);
        ImGui.TreePop();
        anyChanged |= ImGui.Checkbox("Obscure party members", ref this.Plugin.Config.Party);
        anyChanged |= ImGui.Checkbox("Obscure others", ref this.Plugin.Config.Others);
        anyChanged |= ImGui.Checkbox("Exclude friends", ref this.Plugin.Config.ExcludeFriends);
        anyChanged |= ImGui.Checkbox("Change FC", ref this.Plugin.Config.Fc);
        
        ImGui.Separator();
        
        anyChanged |= ImGui.Checkbox("Change Datacenter", ref this.Plugin.Config.World);
        ImGui.TreePush();
        anyChanged |= ImGui.Checkbox("Aether", ref this.Plugin.Config.Aether);
        anyChanged |= ImGui.Checkbox("Crystal", ref this.Plugin.Config.Crystal);
        anyChanged |= ImGui.Checkbox("Dynamis", ref this.Plugin.Config.Dynamis);
        anyChanged |= ImGui.Checkbox("Primal", ref this.Plugin.Config.Primal);
        anyChanged |= ImGui.Checkbox("Chaos", ref this.Plugin.Config.Chaos);
        anyChanged |= ImGui.Checkbox("Light", ref this.Plugin.Config.Light);
        anyChanged |= ImGui.Checkbox("Materia", ref this.Plugin.Config.Materia);
        anyChanged |= ImGui.Checkbox("Elemental", ref this.Plugin.Config.Elemental);
        anyChanged |= ImGui.Checkbox("Gaia", ref this.Plugin.Config.Gaia);
        anyChanged |= ImGui.Checkbox("Mana", ref this.Plugin.Config.Mana);
        anyChanged |= ImGui.Checkbox("Meteor", ref this.Plugin.Config.Meteor);
        ImGui.TreePop();    

        ImGui.Separator();

        anyChanged |= ImGui.Checkbox("Obscure appearance of self", ref this.Plugin.Config.ObscureAppearancesSelf);
        anyChanged |= ImGui.Checkbox("Obscure appearance of party members", ref this.Plugin.Config.ObscureAppearancesParty);
        anyChanged |= ImGui.Checkbox("Obscure appearance of others", ref this.Plugin.Config.ObscureAppearancesOthers);
        anyChanged |= ImGui.Checkbox("Exclude friends##appearance", ref this.Plugin.Config.ObscureAppearancesExcludeFriends);

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Personal appearance preferences")) {
            ImGui.TextUnformatted("Sex");
            var sex = (int) this.Plugin.Config.PreferredSex;
            anyChanged |= ImGui.CheckboxFlags("Female", ref sex, (int) Sex.Female);
            anyChanged |= ImGui.CheckboxFlags("Male", ref sex, (int) Sex.Male);

            if (anyChanged) {
                this.Plugin.Config.PreferredSex = (Sex) sex;
            }

            ImGui.Separator();

            ImGui.TextUnformatted("Race");
            foreach (var race in this.Plugin.DataManager.GetExcelSheet<Race>()!) {
                if (race.RowId == 0) {
                    continue;
                }

                var tribe1 = this.Plugin.DataManager.GetExcelSheet<Tribe>()!.GetRow(race.RowId * 2 - 1)!;
                var tribe2 = this.Plugin.DataManager.GetExcelSheet<Tribe>()!.GetRow(race.RowId * 2)!;

                if (ImGui.CheckboxFlags(race.Feminine.RawString, ref this.Plugin.Config.PreferredRaces, 1 << (int) race.RowId)) {
                    anyChanged = true;

                    if ((this.Plugin.Config.PreferredRaces & (1 << (int) race.RowId)) > 0) {
                        this.Plugin.Config.PreferredTribes |= 1 << (int) tribe1.RowId;
                        this.Plugin.Config.PreferredTribes |= 1 << (int) tribe2.RowId;
                    } else {
                        this.Plugin.Config.PreferredTribes &= ~(1 << (int) tribe1.RowId);
                        this.Plugin.Config.PreferredTribes &= ~(1 << (int) tribe2.RowId);
                    }
                }

                ImGui.TreePush();

                anyChanged |= ImGui.CheckboxFlags(tribe1.Feminine.RawString, ref this.Plugin.Config.PreferredTribes, 1 << (int) tribe1.RowId);
                anyChanged |= ImGui.CheckboxFlags(tribe2.Feminine.RawString, ref this.Plugin.Config.PreferredTribes, 1 << (int) tribe2.RowId);

                ImGui.TreePop();
            }
        }

        if (anyChanged) {
            this.Plugin.SaveConfig();
            this.Plugin.AppearanceRepository.RefilterPersonal();
        }

        ImGui.Separator();

        if (ImGui.Button("Reset names")) {
            if (this.Plugin.KeyState[VirtualKey.CONTROL] && this.Plugin.KeyState[VirtualKey.SHIFT]) {
                this._debug ^= true;
            } else {
                this.Plugin.NameRepository.Reset();
                this.Plugin.AppearanceRepository.Reset();
            }
        }

        if (this._debug) {
            if (ImGui.CollapsingHeader("Debug")) {
                ImGui.PushID("debug");
                try {
                    this.DrawDebug();
                } finally {
                    ImGui.PopID();
                }
            }
        }

        ImGui.End();
    }

    private void DrawDebug() {
        ImGui.TextUnformatted($"Initialised: {this.Plugin.NameRepository.Initialised}");

        if (ImGui.Button("Load sheet")) {
            this.Plugin.Functions.LoadSheet(Util.SheetName);
        }

        if (this.Plugin.TargetManager.Target is { } target) {
            var npc = this.Plugin.AppearanceRepository.GetNpc(target.ObjectId);
            ImGui.TextUnformatted(npc.ToString());
            ImGui.TextUnformatted(this.Plugin.DataManager.GetExcelSheet<ENpcResident>()!.GetRow(npc.RowId)!.Singular);
        }

        ImGui.Separator();

        if (ImGui.TreeNode("Name queue")) {
            if (ImGui.InputInt("Columns", ref this._queueColumns)) {
                this._queueColumns = Math.Max(1, this._queueColumns);
            }

            foreach (var (info, queue) in this.Plugin.NameRepository.ReadOnlyNames) {
                if (!ImGui.CollapsingHeader($"{info}")) {
                    continue;
                }

                if (!ImGui.BeginTable($"{info} table", this._queueColumns)) {
                    continue;
                }

                foreach (var name in queue) {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name);
                }

                ImGui.EndTable();
            }

            ImGui.TreePop();
        }

        if (ImGui.TreeNode("Replacements")) {
            if (ImGui.BeginTable("replacements", 2)) {
                foreach (var (name, replacement) in this.Plugin.NameRepository.ReadonlyReplacements) {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(replacement);
                }

                ImGui.EndTable();
            }

            ImGui.TreePop();
        }

        if (ImGui.TreeNode("Last seen info")) {
            if (ImGui.BeginTable("last seen info", 2)) {
                foreach (var (name, info) in this.Plugin.NameRepository.ReadOnlyLastSeenInfo) {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{info}");
                }

                ImGui.EndTable();
            }
        }

        ImGui.TreePop();
    }
}
