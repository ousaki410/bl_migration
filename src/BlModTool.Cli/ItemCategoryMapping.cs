using System;
using System.Collections.Generic;
using System.Linq;

namespace BlModTool.Cli;

/// <summary>
/// Maps Warband item categories to Bannerlord structured XML files.
/// </summary>
public static class ItemCategoryMapping
{
    // Weapon categories
    public const string Weapons = "weapons";
    public const string TournamentWeapons = "tournament_weapons";
    public const string Banners = "banners";

    // Armor categories by body part
    public const string HeadArmor = "head_armors";
    public const string BodyArmor = "body_armors";
    public const string LegArmor = "leg_armors";
    public const string ArmArmor = "arm_armors";
    public const string ShoulderArmor = "shoulder_armors";

    // Misc categories
    public const string HorsesAndOthers = "horses_and_others";

    // All known Bannerlord item XML file names (for empty placeholder generation)
    public static readonly string[] AllItemFiles = new[]
    {
        Weapons, TournamentWeapons, Banners,
        HeadArmor, BodyArmor, LegArmor, ArmArmor, ShoulderArmor,
        HorsesAndOthers
    };

    // Helper methods to map Warband TypeCode (1-20) to Bannerlord file names
    public static string GetBannerlordXml(int typeCode, bool isFood, bool isConsumable)
    {
        // Consumables (Food/Potion/Map/etc.) -> HorsesAndOthers
        if (isFood || isConsumable)
            return HorsesAndOthers;

        // Shield detection for items with TypeCode 0 (unset) but shield-like name/ID
        // handled by caller passing a pre-resolved shield flag via typeCode override

        return typeCode switch
        {
            WarbandItemType.OneHandedWeapon => Weapons,
            WarbandItemType.TwoHandedWeapon => Weapons,
            WarbandItemType.Polearm => Weapons,
            WarbandItemType.Arrows => Weapons,
            WarbandItemType.Bolts => Weapons,
            WarbandItemType.Bow => Weapons,
            WarbandItemType.Crossbow => Weapons,
            WarbandItemType.Shield => "shields",
            WarbandItemType.HeadArmor => HeadArmor,
            WarbandItemType.BodyArmor => BodyArmor,
            WarbandItemType.FootArmor => LegArmor,
            WarbandItemType.HandArmor => ShoulderArmor,   // hand armor -> shoulder_armors.xml
            WarbandItemType.Horse => HorsesAndOthers,
            WarbandItemType.Animal => HorsesAndOthers,
            WarbandItemType.Thrown => Weapons,   // Warband Throwing -> Bannerlord Throwing
            WarbandItemType.Pistol => HorsesAndOthers, // firearms unsupported, placed here as fallback
            WarbandItemType.Musket => HorsesAndOthers,
            WarbandItemType.Bullets => HorsesAndOthers,
            WarbandItemType.Book => HorsesAndOthers,
            WarbandItemType.Goods => HorsesAndOthers,
            _ => HorsesAndOthers
        };
    }
}