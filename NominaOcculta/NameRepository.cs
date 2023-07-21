using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game;
using Lumina.Excel.GeneratedSheets;

namespace NominaOcculta;

internal class NameRepository : IDisposable {
    private Plugin Plugin { get; }

    private Random Rng { get; } = new();

    private Dictionary<(byte, byte, byte), Queue<string>> Names { get; } = new();
    internal IReadOnlyDictionary<(byte, byte, byte), Queue<string>> ReadOnlyNames => this.Names;

    private Dictionary<string, string> Replacements { get; } = new();
    internal IReadOnlyDictionary<string, string> ReadonlyReplacements => this.Replacements;

    private Dictionary<string, (byte, byte, byte)?> LastSeenInfo { get; } = new();
    internal IReadOnlyDictionary<string, (byte, byte, byte)?> ReadOnlyLastSeenInfo => this.LastSeenInfo;

    private readonly int _numRaces;
    private readonly Stopwatch _loadSheetWatch = new();

    internal bool Initialised;

    internal NameRepository(Plugin plugin) {
        this.Plugin = plugin;

        this._numRaces = this.Plugin.DataManager.GetExcelSheet<Race>()!.Count(row => row.RowId != 0);

        this.Plugin.Functions.LoadSheet(Util.SheetName);
        this.Plugin.ClientState.Login += this.OnLogin;

        for (var race = (byte) 1; race <= this._numRaces; race++) {
            for (var clan = (byte) 0; clan <= 1; clan++) {
                for (var sex = (byte) 0; sex <= 1; sex++) {
                    this.Names[(race, clan, sex)] = new Queue<string>();
                }
            }
        }

        this.Plugin.Framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose() {
        this.Plugin.Framework.Update -= this.OnFrameworkUpdate;
        this.Plugin.ClientState.Login -= this.OnLogin;
    }

    private void OnFrameworkUpdate(Framework framework) {
        // The game unloads the CharaMakeName sheet after logging in.
        // We need this sheet to generate names, so we load it again.
        if (this._loadSheetWatch.IsRunning && this._loadSheetWatch.Elapsed > TimeSpan.FromSeconds(3)) {
            this.Plugin.Functions.LoadSheet(Util.SheetName);
            this._loadSheetWatch.Reset();
        }

        // The in-game name generator will generate duplicate names if it is given
        // identical parameters on the same frame. Instead, we will fill up a queue
        // with 100 names (the maximum amount of players in the object table) for
        // each combination of parameters, generating one name per combination per
        // frame.

        for (var race = (byte) 1; race <= this._numRaces; race++) {
            for (var clan = (byte) 0; clan <= 1; clan++) {
                for (var sex = (byte) 0; sex <= 1; sex++) {
                    var queue = this.Names[(race, clan, sex)];
                    if (queue.Count >= 100) {
                        continue;
                    }

                    var name = this.Plugin.Functions.GenerateName(race, clan, sex);
                    if (name != null && (!queue.TryPeek(out var peek) || peek != name)) {
                        queue.Enqueue(name);
                    }
                }
            }
        }

        if (!this.Initialised) {
            this.Initialised = this.Names.Values.All(queue => queue.Count >= 100);
        }
    }

    private void OnLogin(object? sender, EventArgs e) {
        this._loadSheetWatch.Restart();
    }

    /// <summary>
    /// <para>
    /// Get a consistent replacement name for a real name.
    /// </para>
    /// <para>
    /// This will generate a new name if the given info changes.
    /// </para>
    /// </summary>
    /// <param name="name"></param>
    /// <param name="info">(race, clan, sex) if known. Any unknowns should be 0xFF to be replaced with random, valid values.</param>
    /// <returns>A replacement name. Returns null if name is null/empty or no name could be generated.</returns>
    internal string? GetReplacement(string name, (byte race, byte clan, byte sex) info) {
        if (string.IsNullOrEmpty(name)) {
            return null;
        }

        if (this.LastSeenInfo.TryGetValue(name, out var lastInfo) && lastInfo != info) {
            this.Replacements.Remove(name);
        }

        this.LastSeenInfo[name] = info;

        if (this.Replacements.TryGetValue(name, out var replacement)) {
            return replacement;
        }

        // need to generate a name after this point

        // use random parameters for info if none was specified
        if (info.race == 0xFF) {
            info.race = (byte) this.Rng.Next(1, this._numRaces + 1);
        }

        if (info.clan == 0xFF) {
            info.clan = (byte) this.Rng.Next(0, 2);
        }

        if (info.sex == 0xFF) {
            info.sex = (byte) this.Rng.Next(0, 2);
        }

        // get a name for the given info if possible
        if (this.Names.TryGetValue(info, out var names)) {
            // make sure the new name is not the same as the old name
            names.TryDequeue(out var newName);
            while (newName == name) {
                names.TryDequeue(out newName);
            }

            if (newName != null) {
                this.Replacements[name] = newName;
                return newName;
            }
        }

        // otherwise, get a random name
        // can't really do anything about conflicts here, but this should be a very rare/impossible case
        var random = this.Plugin.Functions.GenerateName(info.race, info.clan, info.sex);
        if (random != null) {
            this.Replacements[name] = random;
        }

        return random;
    }

    internal void Reset() {
        this.Replacements.Clear();
    }
}
