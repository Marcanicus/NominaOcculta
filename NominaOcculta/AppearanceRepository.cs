using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Dalamud;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.GeneratedSheets;

namespace NominaOcculta;

internal class AppearanceRepository {
    private Plugin Plugin { get; }
    private List<ENpcBase> Npcs { get; }
    private List<ENpcBase> PersonalNpcs { get; } = new();
    private Dictionary<uint, int> Cache { get; } = new();
    private int Salt { get; set; } = new Random().Next();

    private static readonly string[] Exclude = {
        "Alisaie",
        "Alphinaud",
        "Aymeric",
        "Biggs",
        "Cid",
        "Estinien",
        "G'raha Tia",
        "Godbert",
        "Haurchefant",
        "Hermes",
        "Hildibrand",
        "Hythlodaeus",
        "Igeyorhm",
        "Kan-E-Senna",
        "Krile",
        "Lahabrea",
        "Lyse",
        "Merlwyb",
        "Minfilia",
        "Nanamo Ul Namo",
        "Papalymo",
        "Raubahn",
        "Ryne",
        "Tataru",
        "Thancred",
        "Themis",
        "Urianger",
        "Venat",
        "Wedge",
        "Y'shtola",
        "Yda",
        "Yugiri",
    };

    internal IReadOnlyDictionary<uint, IReadOnlyList<Item>> JobMainHands { get; }
    internal IReadOnlyDictionary<uint, IReadOnlyList<Item>> JobOffHands { get; }

    internal AppearanceRepository(Plugin plugin) {
        this.Plugin = plugin;

        var names = this.Plugin.DataManager.GetExcelSheet<ENpcResident>()!;
        this.Npcs = this.Plugin.DataManager.GetExcelSheet<ENpcBase>()!
            .Where(row => row.BodyType == 1)
            .Where(row => row.ModelChara.Row == 0)
            .Where(row => row.ModelBody != 0)
            .Where(row => row.ModelLegs != 0)
            .Where(row => !Exclude.Contains(names.GetRow(row.RowId)?.Singular.RawString))
            .ToList();
        this.RefilterPersonal();
        PluginLog.Log($"npcs: {this.Npcs.Count}");

        var jobMainHands = new Dictionary<uint, List<Item>>();
        var jobOffHands = new Dictionary<uint, List<Item>>();
        var allMainHands = this.Plugin.DataManager.GetExcelSheet<Item>()!
            .Where(row => row.EquipSlotCategory.Value!.MainHand != 0)
            // let's not give people ultimate weapons and shit
            .Where(row => !row.IsUnique)
            .ToList();
        var allOffHands = this.Plugin.DataManager.GetExcelSheet<Item>()!
            .Where(row => row.EquipSlotCategory.Value!.OffHand != 0)
            .Where(row => !row.IsUnique)
            .ToList();
        foreach (var job in this.Plugin.DataManager.GetExcelSheet<ClassJob>(ClientLanguage.English)!) {
            if (job.RowId == 0) {
                continue;
            }

            if (!jobMainHands.ContainsKey(job.RowId)) {
                jobMainHands[job.RowId] = new List<Item>();
            }

            if (!jobOffHands.ContainsKey(job.RowId)) {
                jobOffHands[job.RowId] = new List<Item>();
            }

            var engAbbr = job.Abbreviation.RawString;

            foreach (var mainHand in allMainHands) {
                var mainHandCategory = mainHand.ClassJobCategory.Value!;
                var canUseMainHand = (bool?) mainHandCategory.GetType()
                    .GetProperty(engAbbr, BindingFlags.Instance | BindingFlags.Public)!
                    .GetValue(mainHandCategory);

                if (canUseMainHand == true) {
                    jobMainHands[job.RowId].Add(mainHand);
                }
            }

            foreach (var offHand in allOffHands) {
                var offHandCategory = offHand.ClassJobCategory.Value!;
                var canUseOffHand = (bool?) offHandCategory.GetType()
                    .GetProperty(engAbbr, BindingFlags.Instance | BindingFlags.Public)!
                    .GetValue(offHandCategory);

                if (canUseOffHand == true) {
                    jobOffHands[job.RowId].Add(offHand);
                }
            }
        }

        this.JobMainHands = jobMainHands
            .Select(entry => (entry.Key, (IReadOnlyList<Item>) entry.Value.ToImmutableList()))
            .ToImmutableDictionary(entry => entry.Key, entry => entry.Item2);
        this.JobOffHands = jobOffHands
            .Select(entry => (entry.Key, (IReadOnlyList<Item>) entry.Value.ToImmutableList()))
            .ToImmutableDictionary(entry => entry.Key, entry => entry.Item2);
    }

    internal void Reset() {
        this.Salt = new Random().Next();
        this.Cache.Clear();
    }

    internal void RefilterPersonal() {
        this.PersonalNpcs.Clear();
        this.PersonalNpcs.AddRange(this.Npcs
            .Where(row => {
                var sex = (Sex) (1 << (row.Gender + 1));
                return this.Plugin.Config.PreferredSex.HasFlag(sex);
            })
            .Where(row => {
                var race = 1 << (int) row.Race.Row;
                return (this.Plugin.Config.PreferredRaces & race) > 0;
            })
            .Where(row => {
                var tribe = 1 << (int) row.Tribe.Row;
                return (this.Plugin.Config.PreferredTribes & tribe) > 0;
            }));
    }

    private int GetNpcIndex(uint objectId) {
        if (this.Cache.TryGetValue(objectId, out var index)) {
            return index;
        }

        var idx = new Random((int) (objectId + this.Salt)).Next(0, this.Npcs.Count);
        this.Cache[objectId] = idx;
        return idx;
    }

    private int GetNpcIndexPersonal(uint objectId) {
        return new Random((int) (objectId + this.Salt)).Next(0, this.PersonalNpcs.Count);
    }

    internal unsafe ENpcBase GetNpc(uint objectId) {
        var player = *(GameObject**) this.Plugin.ObjectTable.Address;
        if (player != null && objectId == player->ObjectID && this.PersonalNpcs.Count > 0) {
            return this.PersonalNpcs[this.GetNpcIndexPersonal(objectId)];
        }

        return this.Npcs[this.GetNpcIndex(objectId)];
    }

    internal (Item, Item?) GetHands(uint jobId, uint objectId) {
        var random = new Random((int) (objectId + this.Salt));

        var usesOffHand = this.JobOffHands[jobId].Count > 0;

        var mainHand = this.JobMainHands[jobId][random.Next(0, this.JobMainHands[jobId].Count)];
        var mainEquipCategory = mainHand.EquipSlotCategory.Value!;
        var offHand =
            mainEquipCategory.MainHand != 0 && mainEquipCategory.OffHand != 0
                ? null
                : usesOffHand
                    ? this.JobOffHands[jobId][random.Next(0, this.JobOffHands[jobId].Count)]
                    : null;

        return (mainHand, offHand);
    }
}
