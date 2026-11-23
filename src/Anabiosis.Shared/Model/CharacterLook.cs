namespace Anabiosis.Shared.Model;

public enum CharacterGender { Male, Female }

// How a crew member looks, as chosen in the lobby's "Внешность" tab: every number is an index into a small fixed set that the client's CrewSkin draws
// (hair shapes, beard shapes, an accessory, and colours from short palettes). Indices, not colours, so the whole look is a few bytes on the wire and a
// value that is out of range is simply clamped. Null on a character means "not chosen": the figure then gets the look its player id happens to pick.
public sealed record CharacterLook(
    CharacterGender Gender = CharacterGender.Male,
    int Hair = 1,
    int Beard = 0,
    int Accessory = 0,
    int HairColor = 0,
    int BeardColor = 0,
    int Skin = 0)
{
    // 0 is always "none" for hair, beard and accessory.
    public const int HairStyles = 6;
    public const int BeardStyles = 5;
    public const int Accessories = 4;
    public const int HairColors = 6;
    public const int SkinTones = 5;

    public static readonly string[] HairNames = { "Нет", "Короткие", "Длинные", "Пучок", "Ёжик", "Лысина с боков" };
    public static readonly string[] BeardNames = { "Нет", "Щетина", "Короткая", "Борода", "Усы" };
    public static readonly string[] AccessoryNames = { "Нет", "Очки", "Повязка", "Кепка" };

    public CharacterLook Clamped() => this with
    {
        Hair = Math.Clamp(Hair, 0, HairStyles - 1),
        // A beard is a man's: the lobby hides the slider for a woman, and the server holds the same line.
        Beard = Gender == CharacterGender.Female ? 0 : Math.Clamp(Beard, 0, BeardStyles - 1),
        Accessory = Math.Clamp(Accessory, 0, Accessories - 1),
        HairColor = Math.Clamp(HairColor, 0, HairColors - 1),
        BeardColor = Math.Clamp(BeardColor, 0, HairColors - 1),
        Skin = Math.Clamp(Skin, 0, SkinTones - 1),
    };

    // The dice button: a whole new look from `random`.
    public static CharacterLook Random(Random random)
    {
        var gender = random.Next(2) == 0 ? CharacterGender.Male : CharacterGender.Female;
        return new CharacterLook(gender, random.Next(1, HairStyles), gender == CharacterGender.Male ? random.Next(BeardStyles) : 0,
            random.Next(Accessories), random.Next(HairColors), random.Next(HairColors), random.Next(SkinTones)).Clamped();
    }
}
