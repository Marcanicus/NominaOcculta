using System;
using Dalamud.Configuration;

namespace NominaOcculta;

[Serializable]
internal class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public bool Enabled;
    public bool SelfFull;
    public bool SelfFirst;
    public bool SelfLast;
    public bool Party;
    public bool Others;
    public bool ExcludeFriends;

    public bool Fc;
    public bool World;
    public bool Aether;
    public bool Crystal;
    public bool Dynamis;
    public bool Primal;
    public bool Chaos;
    public bool Light;
    public bool Materia;
    public bool Elemental;
    public bool Gaia;
    public bool Mana;
    public bool Meteor;

    public bool ObscureAppearancesSelf;
    public bool ObscureAppearancesParty;
    public bool ObscureAppearancesOthers;
    public bool ObscureAppearancesExcludeFriends;

    public Sex PreferredSex = Sex.Female | Sex.Male;

    public int PreferredRaces = 1 << 1
                                | 1 << 2
                                | 1 << 3
                                | 1 << 4
                                | 1 << 5
                                | 1 << 6
                                | 1 << 7
                                | 1 << 8;

    public int PreferredTribes = 1 << 1
                                 | 1 << 2
                                 | 1 << 3
                                 | 1 << 4
                                 | 1 << 5
                                 | 1 << 6
                                 | 1 << 7
                                 | 1 << 8
                                 | 1 << 9
                                 | 1 << 10
                                 | 1 << 11
                                 | 1 << 12
                                 | 1 << 13
                                 | 1 << 14
                                 | 1 << 15
                                 | 1 << 16;
}
