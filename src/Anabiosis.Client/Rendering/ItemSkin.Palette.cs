using System;
using Microsoft.Xna.Framework;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The shared materials every item is painted from, and which painter draws which item.
public static partial class ItemSkin
{
    private static readonly Mat Steel = new(new Color(132, 136, 142), 0.4f, 0.07f);
    private static readonly Mat SteelLight = new(new Color(164, 166, 168), 0.45f, 0.06f);
    private static readonly Mat Gunmetal = new(new Color(66, 68, 72), 0.3f, 0.06f);
    private static readonly Mat Blackened = new(new Color(44, 43, 44), 0.2f, 0.05f);
    private static readonly Mat Rubber = new(new Color(36, 37, 41), 0.12f, 0.03f);
    private static readonly Mat Brass = new(new Color(172, 138, 78), 0.45f, 0.07f);
    private static readonly Mat Copper = new(new Color(168, 98, 60), 0.4f, 0.08f);
    private static readonly Mat Wood = new(new Color(122, 78, 46), 0.15f, 0.1f);
    private static readonly Mat HazardYellow = new(new Color(196, 158, 58), 0.2f, 0.08f);
    private static readonly Mat SafetyRed = new(new Color(150, 52, 42), 0.2f, 0.08f);
    private static readonly Mat Olive = new(new Color(88, 92, 66), 0.15f, 0.08f);
    private static readonly Mat WhitePlastic = new(new Color(184, 180, 168), 0.25f, 0.05f);
    private static readonly Mat Rock = new(new Color(104, 98, 92), 0.1f, 0.14f);

    private static readonly Color Ink = new(22, 23, 27);
    private static readonly Color Glint = new(255, 255, 240);

    private static Mat Paint(int r, int g, int b, float gloss = 0.3f, float grain = 0.05f) => new(new Color(r, g, b), gloss, grain);

    private static Action? Painter(ItemType type) => type switch
    {
        // tools - ItemSkin.Tools.cs
        ItemType.Wrench => Wrench,
        ItemType.Screwdriver => Screwdriver,
        ItemType.GoshaScrewdriver => GoshaScrewdriver,
        ItemType.WeldingTool => WeldingTool,
        ItemType.Cutter => Cutter,
        ItemType.Knife => Knife,
        ItemType.Axe => Axe,
        // weapons - ItemSkin.Weapons.cs
        ItemType.Rifle => Rifle,
        ItemType.LaserRifle => LaserRifle,
        ItemType.Revolver => Revolver,
        ItemType.BodyArmor => BodyArmor,
        ItemType.BallisticHelmet => BallisticHelmet,
        ItemType.Magazine => Magazine,
        ItemType.RevolverRounds => RevolverRounds,
        // the firearms and their rounds - ItemSkin.Guns.cs, ItemSkin.Ammo.cs
        ItemType.Handcannon => Handcannon,
        ItemType.MachinePistol => MachinePistol,
        ItemType.Smg => Smg,
        ItemType.Shotgun => Shotgun,
        ItemType.AutoShotgun => AutoShotgun,
        ItemType.BoltRifle => BoltRifle,
        ItemType.Hmg => Hmg,
        ItemType.HarpoonGun => HarpoonGun,
        ItemType.HandcannonRounds => HandcannonRounds,
        ItemType.SmgMagazine => SmgMagazine,
        ItemType.ShotgunShells => ShotgunShells,
        ItemType.RifleRounds => RifleRounds,
        ItemType.HmgMagazine => HmgMagazine,
        ItemType.Harpoons => Harpoons,
        ItemType.RevolverRoundsDepleted => RevolverRoundsDepleted,
        ItemType.SmgMagazineDepleted => SmgMagazineDepleted,
        ItemType.ShotgunShellsRubber => ShotgunShellsRubber,
        ItemType.ShotgunSlugs => ShotgunSlugs,
        ItemType.HarpoonsHeavy => HarpoonsHeavy,
        ItemType.HarpoonsExplosive => HarpoonsExplosive,
        ItemType.AmmoCrate => AmmoCrate,
        // gear and ship parts - ItemSkin.Gear.cs
        ItemType.Spacesuit => Spacesuit,
        ItemType.BeltBag => BeltBag,
        ItemType.IdCard => IdCard,
        ItemType.Radio => Radio,
        ItemType.OxygenTank => OxygenTank,
        ItemType.WeldingTank => WeldingTank,
        ItemType.FuelRod => FuelRod,
        ItemType.WireSpool => WireSpool,
        ItemType.HullPlating => HullPlating,
        // medicine - ItemSkin.Medical.cs
        ItemType.MedKit => MedKit,
        ItemType.Bandage => Bandage,
        ItemType.Tourniquet => Tourniquet,
        ItemType.Antiseptic => Antiseptic,
        ItemType.Splint => Splint,
        ItemType.PlasterCast => PlasterCast,
        ItemType.Painkiller => Painkiller,
        ItemType.Antibiotic => Antibiotic,
        ItemType.BurnGel => BurnGel,
        ItemType.Adrenaline => Adrenaline,
        ItemType.Defibrillator => Defibrillator,
        ItemType.SurgeryKit => SurgeryKit,
        // raw goods - ItemSkin.Materials.cs
        ItemType.Mineral => Mineral,
        ItemType.IronOre => IronOre,
        ItemType.NickelOre => NickelOre,
        ItemType.ZincOre => ZincOre,
        ItemType.CopperOre => CopperOre,
        ItemType.TitaniumOre => TitaniumOre,
        ItemType.UraniumOre => UraniumOre,
        ItemType.AluminumOre => AluminumOre,
        ItemType.PlastalloyOre => PlastalloyOre,
        ItemType.Silicon => Silicon,
        ItemType.Carbon => Carbon,
        ItemType.SteelPlate => SteelPlate,
        ItemType.TitaniumPlate => TitaniumPlate,
        ItemType.CopperCable => CopperCable,
        ItemType.Plastic => Plastic,
        ItemType.Plastalloy => Plastalloy,
        ItemType.Hyperium => Hyperium,
        // raw ore from the asteroids - OreArt.cs
        _ when OreArt.Has(type) => () => OreArt.Paint(_buf, _size, type),
        // wiring parts - ItemSkin.Components.cs
        _ when ComponentDefinitions.ComponentKindFor(type) is { } kind => () => LogicPart(kind),
        _ => null,
    };
}
