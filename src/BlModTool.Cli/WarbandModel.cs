namespace BlModTool.Cli;

public sealed class WarbandModule
{
    public string ItemsPath { get; set; } = "";
    public string TroopsPath { get; set; } = "";
    public string FactionsPath { get; set; } = "";
    public string? SkillsPath { get; set; }
    public string? PartyTemplatesPath { get; set; }
    public string? PartiesPath { get; set; }
    public string? QuestsPath { get; set; }
    public string? MenusPath { get; set; }
    public string? DialogStatesPath { get; set; }
    public string? ScriptsPath { get; set; }
    public List<WarbandItem> Items { get; } = new();
    public List<WarbandTroop> Troops { get; } = new();
    public List<WarbandFaction> Factions { get; } = new();
    public List<WarbandSkill> Skills { get; } = new();
    public List<WarbandPartyTemplate> PartyTemplates { get; } = new();
    public List<WarbandParty> Parties { get; } = new();
    public List<WarbandQuest> Quests { get; } = new();
    public List<WarbandMenu> Menus { get; } = new();
    public List<WarbandDialogState> DialogStates { get; } = new();
    public List<WarbandScript> Scripts { get; } = new();
}

public sealed class WarbandQuest
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int XpReward { get; set; }
    public string Description { get; set; } = "";
}

public sealed class WarbandMenuOption
{
    public string Id { get; set; } = "";
    public long Flags { get; set; }
    public string Text { get; set; } = "";
    public List<int> Operations { get; } = new();
}

public sealed class WarbandMenu
{
    public string Id { get; set; } = "";
    public long Flags { get; set; }
    public string Text { get; set; } = "";
    public string Mesh { get; set; } = "";
    public List<WarbandMenuOption> Options { get; } = new();
}

public sealed class WarbandDialogState
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
}

public sealed class WarbandScript
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public int ParameterCount { get; set; }
    public List<int> Operations { get; } = new();
}

public sealed class WarbandParty
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public uint Flags { get; set; }
    public int MenuNo { get; set; }
    public int AiBehavior { get; set; }
    public int FactionIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public List<WarbandPartyMember> Members { get; } = new();
    public double Bearing { get; set; }

    public bool IsTown => Id.StartsWith("p_town_", StringComparison.OrdinalIgnoreCase);
    public bool IsCastle => Id.StartsWith("p_castle_", StringComparison.OrdinalIgnoreCase);
    public bool IsVillage => Id.StartsWith("p_village_", StringComparison.OrdinalIgnoreCase);
    public bool IsSettlement => IsTown || IsCastle || IsVillage;
}

public sealed class WarbandPartyMember
{
    public int TroopIndex { get; set; }
    public int Count { get; set; }
    public int Field3 { get; set; }
    public int Field4 { get; set; }
}

public sealed class WarbandPartyTemplateStack
{
    public int TroopIndex { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public int Probability { get; set; }
}

public sealed class WarbandPartyTemplate
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long Flags { get; set; }
    public int Field3 { get; set; }
    public int Behavior { get; set; }
    public int FactionIndex { get; set; }
    public int Field5 { get; set; }
    public List<WarbandPartyTemplateStack> Stacks { get; } = new();
}

public sealed class WarbandSkill
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int BaseAttribute { get; set; }
    public int MaxLevel { get; set; }
    public string Description { get; set; } = "";
    public bool IsPartySkill { get; set; }
    public bool IsInactive { get; set; }

    public string BaseAttributeName
    {
        get
        {
            return (BaseAttribute & 0x0F) switch
            {
                0 => "Strength",
                1 => "Agility",
                2 => "Intelligence",
                3 => "Charisma",
                _ => "Unknown"
            };
        }
    }
}

public sealed class WarbandItem
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mesh { get; set; } = "";
    public uint TypeFlags { get; set; }
    public ulong Capabilities { get; set; }
    public int Value { get; set; }
    public ulong ModifierBits { get; set; }
    public double Weight { get; set; }
    public int Abundance { get; set; }
    public int HeadArmor { get; set; }
    public int BodyArmor { get; set; }
    public int LegArmor { get; set; }
    public int Difficulty { get; set; }
    public int HitPoints { get; set; }
    public int SpeedRating { get; set; }
    public int MissileSpeed { get; set; }
    public int WeaponLength { get; set; }
    public int MaxAmmo { get; set; }
    public int ThrustPacked { get; set; }
    public int SwingPacked { get; set; }

    public int TypeCode => (int)(TypeFlags & 0xFF);
    public int ThrustDamage => ThrustPacked & 0xFF;
    public int ThrustDamageType => (ThrustPacked >> 8) & 0x3;
    public int SwingDamage => SwingPacked & 0xFF;
    public int SwingDamageType => (SwingPacked >> 8) & 0x3;

    public bool HasFlag(uint flag) => (TypeFlags & flag) != 0;
}

public sealed class WarbandInventoryEntry
{
    public int ItemIndex { get; set; }
    public int Modifier { get; set; }
}

public sealed class WarbandTroop
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PluralName { get; set; } = "";
    public int Flags { get; set; }
    public int Scene { get; set; }
    public int Reserved { get; set; }
    public int FactionIndex { get; set; }
    public int Upgrade1Index { get; set; }
    public int Upgrade2Index { get; set; }
    public List<WarbandInventoryEntry> Inventory { get; } = new();
    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Intelligence { get; set; }
    public int Charisma { get; set; }
    public int Level { get; set; }
    public List<int> Proficiencies { get; } = new();
    public List<uint> SkillWords { get; } = new();
    public List<ulong> FaceKeys { get; } = new();

    public bool HasFlag(int flag) => (Flags & flag) != 0;

    public int GetSkill(int index)
    {
        var wordIndex = index / 8;
        if (wordIndex < 0 || wordIndex >= SkillWords.Count)
            return 0;

        var shift = (index % 8) * 4;
        return (int)((SkillWords[wordIndex] >> shift) & 0xF);
    }
}

public sealed class WarbandFaction
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Flags { get; set; }
    public int Color { get; set; }
    public List<double> Relations { get; } = new();
    public List<string> Ranks { get; } = new();
}

public static class WarbandItemType
{
    public const int Horse = 1;
    public const int OneHandedWeapon = 2;
    public const int TwoHandedWeapon = 3;
    public const int Polearm = 4;
    public const int Arrows = 5;
    public const int Bolts = 6;
    public const int Shield = 7;
    public const int Bow = 8;
    public const int Crossbow = 9;
    public const int Thrown = 10;
    public const int Goods = 11;
    public const int HeadArmor = 12;
    public const int BodyArmor = 13;
    public const int FootArmor = 14;
    public const int HandArmor = 15;
    public const int Pistol = 16;
    public const int Musket = 17;
    public const int Bullets = 18;
    public const int Animal = 19;
    public const int Book = 20;
}

public static class WarbandItemFlags
{
    public const uint Spear = 0x00008000;
    public const uint Merchandise = 0x00010000;
    public const uint WoodenAttack = 0x00020000;
    public const uint WoodenParry = 0x00040000;
    public const uint Food = 0x00080000;
    public const uint CantReloadOnHorseback = 0x00100000;
    public const uint TwoHanded = 0x00200000;
    public const uint Primary = 0x00400000;
    public const uint Secondary = 0x00800000;
    public const uint CoversLegs = 0x01000000;
    public const uint DoesntCoverHair = 0x01000000;
    public const uint Consumable = 0x02000000;
    public const uint BonusAgainstShield = 0x04000000;
    public const uint PenaltyWithShield = 0x08000000;
    public const uint CantUseOnHorseback = 0x10000000;
    public const uint Civilian = 0x20000000;
    public const uint FitToHead = 0x40000000;
    public const uint CoversHead = 0x80000000;
}

public static class WarbandTroopFlags
{
    public const int Female = 0x00000001;
    public const int Hero = 0x00000010;
    public const int Mounted = 0x00000400;
    public const int IsMerchant = 0x00001000;
}

public static class WarbandCapabilities
{
    public const ulong ShootBow = 0x0000000000001000;
    public const ulong ShootCrossbow = 0x0000000000004000;
    public const ulong ThrowMask = 0x0000000000070000;
    public const ulong ThrowStone = 0x0000000000010000;
    public const ulong ThrowKnife = 0x0000000000020000;
    public const ulong ThrowAxe = 0x0000000000030000;
    public const ulong ThrowJavelin = 0x0000000000040000;
}
