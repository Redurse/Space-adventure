using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The reactor's art entry points. The painter itself (canvas, shading, wear passes) lives in MachineArt,
// shared with the other big machines painted the same way (MachineArt.Workshop.cs - fabricator,
// deconstructor).
public static class ReactorTexture
{
    public const float GlowMargin = MachineArt.GlowMargin;
    public static ReactorArt CreateArt(GraphicsDevice device) => MachineArt.CreateArt(device);
    public static int StageFor(float integrity) => MachineArt.StageFor(integrity);
}
