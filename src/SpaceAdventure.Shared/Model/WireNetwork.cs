namespace SpaceAdventure.Shared.Model;

// Fixed base wiring topology (game_design.md section 1, M14) - "на старте корабль уже полностью
// разведён проводкой". Device-kind node ids reuse the matching ShipSystemDevice.Id (see Ship.cs)
// directly. Shields is the one system with two physical generators (matching the design doc's own
// example - "несколько генераторов щита в разных частях корпуса"), as is Engine now that a hull can
// carry two of them, so those two systems get two drop links off a shared junction; every other
// system has exactly one device, one drop. Losing one drop of a pair costs half that system's
// power rather than all of it (World.Wiring.cs).
//
// The topology is fixed for every hull, so every ship class has to carry the same set of device
// ids - a class with only one engine block would leave drop-engine-2 pointing at nothing, and that
// half-power rule would dock it for damage to a device it doesn't have.
public sealed class WireNetwork
{
    public IReadOnlyList<WireNode> Nodes { get; }
    public IReadOnlyList<WireLink> Links { get; }

    public WireNetwork(IReadOnlyList<WireNode> nodes, IReadOnlyList<WireLink> links)
    {
        Nodes = nodes;
        Links = links;
    }

    public static WireNetwork CreateDefault()
    {
        var nodes = new[]
        {
            new WireNode("node-distribution", WireNodeKind.Distribution, "Распределение", X: 60f, Y: 10f),

            new WireNode("junction-oxygen", WireNodeKind.Junction, "Коробка: Кислород", X: 20f, Y: 60f),
            new WireNode("junction-engine", WireNodeKind.Junction, "Коробка: Двигатель", X: 50f, Y: 60f),
            new WireNode("junction-shields", WireNodeKind.Junction, "Коробка: Щиты", X: 80f, Y: 60f),
            new WireNode("junction-weaponcharger", WireNodeKind.Junction, "Коробка: Орудия", X: 110f, Y: 60f),
            new WireNode("junction-secondary", WireNodeKind.Junction, "Коробка: Прочее", X: 140f, Y: 60f),

            new WireNode("system-oxygen", WireNodeKind.Device, "Кислород", X: 20f, Y: 110f),
            new WireNode("system-engine", WireNodeKind.Device, "Двигатель 1", X: 42f, Y: 110f),
            new WireNode("system-engine-2", WireNodeKind.Device, "Двигатель 2", X: 58f, Y: 110f),
            new WireNode("system-shields", WireNodeKind.Device, "Щиты (ген. 1)", X: 70f, Y: 110f),
            new WireNode("system-shields-2", WireNodeKind.Device, "Щиты (ген. 2)", X: 90f, Y: 110f),
            new WireNode("system-weapon-charger", WireNodeKind.Device, "Орудия", X: 110f, Y: 110f),
            new WireNode("system-secondary", WireNodeKind.Device, "Прочее", X: 140f, Y: 110f),
        };

        var links = new[]
        {
            new WireLink("trunk-oxygen", "node-distribution", "junction-oxygen", PowerSystemId.Oxygen),
            new WireLink("trunk-engine", "node-distribution", "junction-engine", PowerSystemId.Engine),
            new WireLink("trunk-shields", "node-distribution", "junction-shields", PowerSystemId.Shields),
            new WireLink("trunk-weaponcharger", "node-distribution", "junction-weaponcharger", PowerSystemId.WeaponCharger),
            new WireLink("trunk-secondary", "node-distribution", "junction-secondary", PowerSystemId.Secondary),

            new WireLink("drop-oxygen", "junction-oxygen", "system-oxygen", PowerSystemId.Oxygen),
            new WireLink("drop-engine-1", "junction-engine", "system-engine", PowerSystemId.Engine),
            new WireLink("drop-engine-2", "junction-engine", "system-engine-2", PowerSystemId.Engine),
            new WireLink("drop-shields-1", "junction-shields", "system-shields", PowerSystemId.Shields),
            new WireLink("drop-shields-2", "junction-shields", "system-shields-2", PowerSystemId.Shields),
            new WireLink("drop-weaponcharger", "junction-weaponcharger", "system-weapon-charger", PowerSystemId.WeaponCharger),
            new WireLink("drop-secondary", "junction-secondary", "system-secondary", PowerSystemId.Secondary),
        };

        return new WireNetwork(nodes, links);
    }
}
