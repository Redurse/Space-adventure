namespace SpaceAdventure.Shared.Model;

// Wall variants (direct user request, humble-soaring-cat.md M76 follow-up) - a Solid wall/tile's
// own "skin", orthogonal to TileWallKind's None/Solid/Door structural role. Standard is every
// existing wall (hand-authored hulls and every custom ship built before this existed) - adding this
// enum doesn't change their behavior, since WallBlock/TileCell both default to it.
public enum WallMaterial
{
    Standard,
    // Tougher bulkhead - same collision/atmosphere behavior as Standard, just more HP before it
    // breaches (WallMaterialDefaults.MaxHp).
    Reinforced,
    // Renders as glass instead of plating and breaches with much less HP - still blocks movement/
    // holds atmosphere exactly like a Standard wall (no change to sightlines/occluders - that's a
    // separate, bigger system this doesn't touch), it's just weaker and reads differently on screen.
    Window,
}

public static class WallMaterialDefaults
{
    public const float StandardMaxHp = 100f;
    public const float ReinforcedMaxHp = 220f;
    public const float WindowMaxHp = 45f;

    public static float MaxHp(WallMaterial material) => material switch
    {
        WallMaterial.Reinforced => ReinforcedMaxHp,
        WallMaterial.Window => WindowMaxHp,
        _ => StandardMaxHp,
    };
}
