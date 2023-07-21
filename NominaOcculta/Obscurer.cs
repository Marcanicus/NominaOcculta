using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.Character.Data;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using XivCommon.Functions.FriendList;
using XivCommon.Functions.NamePlates;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace NominaOcculta;

internal class Obscurer : IDisposable {
    private Plugin Plugin { get; }

    private Stopwatch UpdateTimer { get; } = new();
    private IReadOnlySet<string> Friends { get; set; }

    internal unsafe Obscurer(Plugin plugin) {
        this.Plugin = plugin;

        this.UpdateTimer.Start();

        this.Friends = this.Plugin.Common.Functions.FriendList.List
            .Select(friend => friend.Name.TextValue)
            .ToHashSet();

        this.Plugin.Framework.Update += this.OnFrameworkUpdate;
        this.Plugin.Functions.AtkTextNodeSetText += this.OnAtkTextNodeSetText;
        this.Plugin.Functions.CharacterInitialise += this.OnCharacterInitialise;
        this.Plugin.Functions.FlagSlotUpdate += this.OnFlagSlotUpdate;
        this.Plugin.Common.Functions.NamePlates.OnUpdate += this.OnNamePlateUpdate;
        this.Plugin.ChatGui.ChatMessage += this.OnChatMessage;
    }

    public unsafe void Dispose() {
        this.Plugin.ChatGui.ChatMessage -= this.OnChatMessage;
        this.Plugin.Common.Functions.NamePlates.OnUpdate -= this.OnNamePlateUpdate;
        this.Plugin.Functions.AtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        this.Plugin.Functions.CharacterInitialise -= this.OnCharacterInitialise;
        this.Plugin.Functions.FlagSlotUpdate -= this.OnFlagSlotUpdate;
        this.Plugin.Framework.Update -= this.OnFrameworkUpdate;
    }

    private static readonly ConditionFlag[] DutyFlags = {
        ConditionFlag.BoundByDuty,
        ConditionFlag.BoundByDuty56,
        ConditionFlag.BoundByDuty95,
        ConditionFlag.BoundToDuty97,
    };

    private bool IsInDuty() {
        return DutyFlags.Any(flag => this.Plugin.Condition[flag]);
    }

    private void OnFrameworkUpdate(Framework framework) {
        if (this.UpdateTimer.Elapsed < TimeSpan.FromSeconds(5) || this.IsInDuty()) {
            return;
        }

        this.Friends = this.Plugin.Common.Functions.FriendList.List
            .Select(friend => friend.Name.TextValue)
            .ToHashSet();
        this.UpdateTimer.Restart();
    }

    private static readonly Regex Coords = new(@"^X: \d+. Y: \d+.(?: Z: \d+.)?$", RegexOptions.Compiled);

    private void OnAtkTextNodeSetText(IntPtr node, IntPtr textPtr, ref SeString? overwrite) {
        // A catch-all for UI text. This is slow, so specialised methods should be preferred.

        var text = Util.ReadRawSeString(textPtr);

        if (text.Payloads.All(payload => payload.Type != PayloadType.RawText)) {
            return;
        }

        var tval = text.TextValue;
        if (string.IsNullOrWhiteSpace(tval) || tval.All(c => !char.IsLetter(c)) || Coords.IsMatch(tval)) {
            return;
        }

        var changed = this.ChangeNames(text);
        if (changed) {
            overwrite = text;
        }
    }

    private unsafe bool ShouldObscureAppearance(GameObject* gameObj) {
        if (gameObj == null) {
            return false;
        }

        if (gameObj->ObjectKind != (byte) FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Pc) {
            return false;
        }

        var gameObject = this.Plugin.ObjectTable.CreateObjectReference((IntPtr) gameObj)!;
        return gameObject is Character chara && this.ShouldObscureAppearance(chara);
    }

    private unsafe bool ShouldObscureAppearance(Character chara) {
        if (!this.Plugin.Config.Enabled) {
            return false;
        }

        var name = chara.RawName()!;

        if (this.Plugin.Config.ObscureAppearancesExcludeFriends && this.Friends.Contains(name)) {
            return false;
        }

        var player = *(GameObject**) this.Plugin.ObjectTable.Address;
        if (player != null && player->ObjectID == chara.ObjectId) {
            return this.Plugin.Config.ObscureAppearancesSelf;
        }

        var party = this.Plugin.PartyList.Select(member => member.ObjectId);
        if (party.Contains(chara.ObjectId)) {
            return this.Plugin.Config.ObscureAppearancesParty;
        }

        return this.Plugin.Config.ObscureAppearancesOthers;
    }

    private unsafe void OnCharacterInitialise(GameObject* gameObj, IntPtr humanPtr, IntPtr customiseDataPtr) {
        if (!this.ShouldObscureAppearance(gameObj)) {
            return;
        }

        var npc = this.Plugin.AppearanceRepository.GetNpc(gameObj->ObjectID);

        var customise = (byte*) customiseDataPtr;
        customise[(int) CustomizeIndex.Race] = (byte) npc.Race.Row;
        customise[(int) CustomizeIndex.Gender] = npc.Gender;
        customise[(int) CustomizeIndex.ModelType] = npc.BodyType;
        customise[(int) CustomizeIndex.Height] = npc.Height;
        customise[(int) CustomizeIndex.Tribe] = (byte) npc.Tribe.Row;
        customise[(int) CustomizeIndex.FaceType] = npc.Face;
        customise[(int) CustomizeIndex.HairStyle] = npc.HairStyle;
        customise[(int) CustomizeIndex.HasHighlights] = npc.HairHighlight;
        customise[(int) CustomizeIndex.SkinColor] = npc.SkinColor;
        customise[(int) CustomizeIndex.EyeColor] = npc.EyeColor;
        customise[(int) CustomizeIndex.HairColor] = npc.HairColor;
        customise[(int) CustomizeIndex.HairColor2] = npc.HairHighlightColor;
        customise[(int) CustomizeIndex.FaceFeatures] = npc.FacialFeature;
        customise[(int) CustomizeIndex.FaceFeaturesColor] = npc.FacialFeatureColor;
        customise[(int) CustomizeIndex.Eyebrows] = npc.Eyebrows;
        customise[(int) CustomizeIndex.EyeColor2] = npc.EyeHeterochromia;
        customise[(int) CustomizeIndex.EyeShape] = npc.EyeShape;
        customise[(int) CustomizeIndex.NoseShape] = npc.Nose;
        customise[(int) CustomizeIndex.JawShape] = npc.Jaw;
        customise[(int) CustomizeIndex.LipStyle] = npc.Mouth;
        customise[(int) CustomizeIndex.LipColor] = npc.LipColor;
        customise[(int) CustomizeIndex.RaceFeatureSize] = npc.BustOrTone1;
        customise[(int) CustomizeIndex.RaceFeatureType] = npc.ExtraFeature1;
        customise[(int) CustomizeIndex.BustSize] = npc.ExtraFeature2OrBust;
        customise[(int) CustomizeIndex.Facepaint] = npc.FacePaint;
        customise[(int) CustomizeIndex.FacepaintColor] = npc.FacePaintColor;
    }

    private enum EquipSlot : uint {
        Head = 0,
        Body = 1,
        Hands = 2,
        Legs = 3,
        Feet = 4,
        Ears = 5,
        Neck = 6,
        Wrists = 7,
        RightRing = 8,
        LeftRing = 9,
    }

    private unsafe void OnFlagSlotUpdate(GameObject* gameObj, uint slot, EquipData* equipData) {
        if (equipData == null) {
            return;
        }

        if (!this.ShouldObscureAppearance(gameObj)) {
            return;
        }

        //var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*) gameObj;
        var chara = (CharacterData*) gameObj;
        var (mainHand, offHand) = this.Plugin.AppearanceRepository.GetHands(chara->ClassJob, gameObj->ObjectID);

        var npc = this.Plugin.AppearanceRepository.GetNpc(gameObj->ObjectID);
        var itemSlot = (EquipSlot) slot;
        var info = itemSlot switch {
            EquipSlot.Head => (npc.ModelHead, npc.DyeHead.Row),
            EquipSlot.Body => (npc.ModelBody, npc.DyeBody.Row),
            EquipSlot.Hands => (npc.ModelHands, npc.DyeHands.Row),
            EquipSlot.Legs => (npc.ModelLegs, npc.DyeLegs.Row),
            EquipSlot.Feet => (npc.ModelFeet, npc.DyeFeet.Row),
            EquipSlot.Ears => (npc.ModelEars, npc.DyeEars.Row),
            EquipSlot.Neck => (npc.ModelNeck, npc.DyeNeck.Row),
            EquipSlot.Wrists => (npc.ModelWrists, npc.DyeWrists.Row),
            EquipSlot.RightRing => (npc.ModelRightRing, npc.DyeRightRing.Row),
            EquipSlot.LeftRing => (npc.ModelLeftRing, npc.DyeLeftRing.Row),
            // EquipSlot.MainHand => (mainHand.ModelMain, npc.DyeMainHand.Row),
            // EquipSlot.OffHand => (mainHand.ModelSub != 0 ? mainHand.ModelSub : offHand?.ModelMain ?? 0, npc.DyeOffHand.Row),
            _ => (uint.MaxValue, uint.MaxValue),
        };

        if (info.Item1 == uint.MaxValue) {
            return;
        }

        equipData->Model = (ushort) (info.Item1 & 0xFFFF);
        equipData->Variant = (byte) ((info.Item1 >> 16) & 0xFF);
        equipData->Dye = (byte) info.Item2;
    }

    private void OnNamePlateUpdate(NamePlateUpdateEventArgs args) {
        // only replace nameplates that have objects in the table
        if (!this.Plugin.Config.Enabled || !this.Plugin.NameRepository.Initialised || args.ObjectId == 0xE0000000) {
            return;
        }

        // find the object this nameplate references
        var obj = this.Plugin.ObjectTable.FirstOrDefault(o => o.ObjectId == args.ObjectId);
        if (obj == null) {
            return;
        }

        // handle owners
        if (obj.OwnerId != 0xE0000000) {
            if (this.Plugin.ObjectTable.FirstOrDefault(o => o.ObjectId == obj.OwnerId) is not { } owner) {
                return;
            }

            obj = owner;
        }

        // only work for characters
        if (obj.ObjectKind != ObjectKind.Player || obj is not Character chara) {
            return;
        }

        var info = this.GetInfo(chara);

        void Change(string name) {
            this.ChangeName(args.Name, name, info);
            this.ChangeName(args.Title, name, info);
            this.ChangeName(args.FreeCompany, name, info);
        }
        
        var name = chara.Name.TextValue;
        var playerId = this.Plugin.ClientState.LocalPlayer?.ObjectId;
        var party = this.Plugin.PartyList.Select(member => member.ObjectId).ToArray();

        if ((this.Plugin.Config.SelfFull || this.Plugin.Config.SelfFirst || this.Plugin.Config.SelfLast) && chara.ObjectId == playerId) {
            if (this.Plugin.Config.SelfFull) {
                Change(name);
            }

            if ((this.Plugin.Config.SelfFirst || this.Plugin.Config.SelfLast) && this.Plugin.NameRepository.GetReplacement(name, info) is { } replacement) {
                var parts = name.Split(' ', 2);
                var replacementParts = replacement.Split(' ', 2);

                if (this.Plugin.Config.SelfFirst) {
                    args.Name.ReplacePlayerName(parts[0], replacementParts[0]);
                    args.Title.ReplacePlayerName(parts[0], replacementParts[0]);
                }

                if (this.Plugin.Config.SelfLast) {
                    args.Name.ReplacePlayerName(parts[1], replacementParts[1]);
                    args.Title.ReplacePlayerName(parts[1], replacementParts[1]);
                }
            }
        } else if (this.Plugin.Config.Party && party.Contains(chara.ObjectId) && (!this.Plugin.Config.ExcludeFriends || !this.Friends.Contains(name))) {
            Change(name);
        } else if (this.Plugin.Config.Others && chara.ObjectId != playerId && !party.Contains(chara.ObjectId) && (!this.Plugin.Config.ExcludeFriends || !this.Friends.Contains(name))) {
            Change(chara.Name.TextValue);
        }
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
        this.ChangeNames(sender);
        this.ChangeNames(message);
    }

    private void ChangeName(SeString text, string name, (byte, byte, byte) info) {
        if (this.Plugin.NameRepository.GetReplacement(name, info) is not { } replacement) {
            return;
        }

        text.ReplacePlayerName(name, replacement);
    }

    // PERFORMANCE NOTE: This potentially loops over the party list twice and the object
    //                   table once entirely. Should be avoided if being used in a
    //                   position where the player to replace is known.
    private bool ChangeNames(SeString text) {
        if (!this.Plugin.Config.Enabled || !this.Plugin.NameRepository.Initialised) {
            return false;
        }
        
        var changed = false;
        var player = this.Plugin.ClientState.LocalPlayer;
        
        if (player != null && this.Plugin.Config.Fc)
        {
            var fc = player.CompanyTag.TextValue;
            text.ReplacePlayerName(fc, "FC");
            changed = true;
        }
        
        if (player != null && this.Plugin.Config.World && player.CurrentWorld.GameData != null)
        {
            if (this.Plugin.Config.Aether)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Aether");
                changed = true;
            }
            if (this.Plugin.Config.Crystal)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Crystal");
                changed = true;
            }
            if (this.Plugin.Config.Dynamis)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Dynamis");
                changed = true;
            }
            if (this.Plugin.Config.Primal)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Primal");
                changed = true;
            }
            if (this.Plugin.Config.Chaos)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Chaos");
                changed = true;
            }
            if (this.Plugin.Config.Light)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Light");
                changed = true;
            }
            if (this.Plugin.Config.Materia)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Materia");
                changed = true;
            }
            if (this.Plugin.Config.Elemental)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Elemental");
                changed = true;
            }
            if (this.Plugin.Config.Gaia)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Gaia");
                changed = true;
            }
            if (this.Plugin.Config.Mana)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Mana");
                changed = true;
            }
            if (this.Plugin.Config.Meteor)
            {
                var world = player.CurrentWorld.GameData.Name.RawString;
                text.ReplacePlayerName(world, "Meteor");
                changed = true;
            }
        }
        
        if (player != null && this.Plugin.Config.SelfFull) {
            var playerName = player.RawName()!;
            if (this.Plugin.NameRepository.GetReplacement(playerName, this.GetInfo(player!)) is { } replacement) {
                text.ReplacePlayerName(playerName, replacement);
                changed = true;
            }
        }

        if (player != null && (this.Plugin.Config.SelfFirst || this.Plugin.Config.SelfLast)) {
            var playerName = player.RawName()!;
            if (this.Plugin.NameRepository.GetReplacement(playerName, this.GetInfo(player!)) is { } replacement) {
                var parts = playerName.Split(' ', 2);
                var replacementParts = replacement.Split(' ', 2);

                if (this.Plugin.Config.SelfFirst) {
                    text.ReplacePlayerName(parts[0], replacementParts[0]);
                    changed = true;
                }

                if (this.Plugin.Config.SelfLast) {
                    text.ReplacePlayerName(parts[1], replacementParts[1]);
                    changed = true;
                }
            }
        }
        if (this.Plugin.Config.Party) {
            foreach (var member in this.Plugin.PartyList) {
                string name;
                unsafe {
                    var raw = (PartyMember*) member.Address;
                    name = Marshal.PtrToStringUTF8((IntPtr) raw->Name)!;
                }

                var info = ((byte) 0xFF, (byte) 0xFF, member.Sex);
                if (member.GameObject is Character chara) {
                    info = this.GetInfo(chara);
                }

                if (member.ObjectId == player?.ObjectId || this.Plugin.NameRepository.GetReplacement(name, info) is not { } replacement) {
                    continue;
                }

                if (this.Plugin.Config.ExcludeFriends && this.Friends.Contains(name)) {
                    continue;
                }

                text.ReplacePlayerName(name, replacement);
                changed = true;
            }
        }

        if (this.Plugin.Config.Others) {
            var party = this.Plugin.PartyList.Select(member => member.ObjectId).ToList();

            foreach (var obj in this.Plugin.ObjectTable) {
                if (obj.ObjectKind != ObjectKind.Player || obj is not Character chara || obj.ObjectId == player?.ObjectId || party.Contains(obj.ObjectId)) {
                    continue;
                }

                var name = chara.RawName()!;
                if (this.Plugin.Config.ExcludeFriends && this.Friends.Contains(name)) {
                    continue;
                }

                var info = this.GetInfo(chara);
                if (info.race == 0) {
                    continue;
                }

                if (this.Plugin.NameRepository.GetReplacement(name, info) is not { } replacement) {
                    continue;
                }

                text.ReplacePlayerName(name, replacement);
                changed = true;
            }
        }

        return changed;
    }

    private (byte race, byte clan, byte gender) GetInfo(Character chara) {
        if (this.ShouldObscureAppearance(chara)) {
            var npc = this.Plugin.AppearanceRepository.GetNpc(chara.ObjectId);
            return (
                (byte) npc.Race.Row,
                (byte) ((npc.Tribe.Row - 1) % 2),
                npc.Gender
            );
        }

        return (
            chara.Customize[(byte) CustomizeIndex.Race],
            (byte) ((chara.Customize[(byte) CustomizeIndex.Tribe] - 1) % 2),
            chara.Customize[(byte) CustomizeIndex.Gender]
        );
    }
}
