using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Content-каталог отсеков - real reference art for each of the 13 catalog room types (the player's
// own screenshots, cropped one sprite per room and dropped in Content/Textures/RoomCatalog), keyed
// on the exact same room.Name string AccentByCatalogName already switches on (TryBuildRoom names
// every built room after its own catalog entry). Set once from Game1's content-loading step, same
// "static setter" convention ItemIcons.SetScrewdriverTexture already uses - RoomDecor itself never
// touches a ContentManager, ShipRenderer doesn't own one either.
public static partial class RoomDecor
{
    // (catalog display name, Content/Textures/RoomCatalog file name) - Game1's own content-loading
    // step iterates this to load and register every texture below via SetCatalogTexture, rather
    // than each call site hand-typing all 13 names twice (a stray typo here would silently just mean
    // that one room draws procedurally forever instead of failing loudly).
    public static readonly (string CatalogName, string TextureName)[] CatalogTextureNames =
    {
        ("Реакторный отсек", "reactor"),
        ("Кокпит (малый)", "cockpit-small"),
        ("Двигатель маршевый (малый)", "engine-small"),
        ("Турель лазерная", "turret-laser"),
        ("Капитанский мостик (большой)", "bridge-large"),
        ("Турель пушечная", "turret-ballistic"),
        ("Каюта", "quarters"),
        ("Манёвровый двигатель (однонаправленный)", "rcs-1way"),
        ("Манёвровый двигатель (двусторонний)", "rcs-2way"),
        ("Камера", "camera"),
        ("Двигатель маршевый (большой)", "engine-big"),
        ("Манёвровый двигатель (трёхсторонний)", "rcs-3way"),
        ("Генератор щита", "shield-generator"),
    };

    private static readonly Dictionary<string, Texture2D> _catalogTextures = new();

    public static void SetCatalogTexture(string roomCatalogName, Texture2D texture) =>
        _catalogTextures[roomCatalogName] = texture;

    // Drawn INSTEAD of the deck-plate/grime/light-pool/furniture stack (ShipRenderer.DrawRoomFloor)
    // whenever a catalog room has real reference art - the whole floor, walls, seating and
    // equipment are already baked into the one image, stretched to fill the room's own footprint,
    // rather than layered piecemeal like the procedural rooms are. Per the reference author's own
    // instruction: in a marching-engine room the engine texture itself doubles as the device - there
    // is no separate instrument drawn on top of it, the same way a built turret or camera's own
    // device model already stands in for its room's content elsewhere.
    public static bool TryDrawCatalogTexture(SpriteBatch spriteBatch, Rectangle rect, string? roomName)
    {
        if (roomName is null || !_catalogTextures.TryGetValue(roomName, out var texture))
            return false;
        spriteBatch.Draw(texture, rect, Color.White);
        return true;
    }

    // Same check as TryDrawCatalogTexture above, without actually drawing - lets a device's own
    // renderer (ShipRenderer.DrawReactorBlock) skip its usual icon/housing overlay for a room whose
    // reference art already draws the whole machine baked in, the same "texture doubles as the
    // device" rule this class's own doc comment already states for engine/turret/camera rooms.
    public static bool HasCatalogTexture(string? roomName) => roomName is not null && _catalogTextures.ContainsKey(roomName);
}
