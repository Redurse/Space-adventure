// The menu's planet, drawn live so it can turn.
//
// A pre-rendered frame sequence long enough to rotate smoothly would be tens of megabytes; one
// equirectangular strip plus this shader is a few hundred kilobytes and rotates continuously. The
// strip is seamless in longitude, so Rotation can grow without bound and the surface just keeps
// coming round.
//
// Everything else - which half is day, where the terminator falls, where the city lights show - is
// derived from SunDirection, so moving the star in the scene moves all of it together. That is the
// whole reason to do this in a shader rather than bake it: the light is a parameter, not a decision
// made at bake time.

#define PS_SHADERMODEL ps_4_0_level_9_3

// Where the sun is. X and Y are fixed by the backdrop - the star is painted up and to the left of the
// planet and the light has to come from there. Z is the one free number, because a painted star has
// no stated depth, and it is what decides the shape of the terminator:
//
//   z > 0   star towards the viewer, MORE than half the disc lit, terminator bows away from the star
//   z = 0   star level with the planet, exactly half lit, terminator a straight line
//   z < 0   star beyond the planet, LESS than half lit, terminator bows towards it - a crescent
//
// Negative on purpose: it leaves a lit crescent and gives the night side most of the globe to put
// cities on. Y is zero, which is what pins the terminator to both poles - see Game1.MenuScene.cs.
// Where the boundary crosses the equator is |z| of a radius towards the star; at the poles it is
// exactly on the meridian, whatever z is.
float3 SunDirection = float3(-1.0, 0.0, -0.55);

// Turns with time. One full revolution per 2*pi.
float Rotation = 0.0;

// Colour of the star, used for the warm line at the terminator.
float3 SunColour = float3(1.0, 0.86, 0.66);

// How bright the cities on the night side burn.
float CityBrightness = 1.0;

// The disc's radius as a fraction of the quad's half size. Less than 1 on purpose: the atmosphere
// reaches past the surface, and a quad sized exactly to the disc clips that halo into four corner
// wedges instead of a ring. The caller pads the quad by the same factor.
float DiscRadius = 0.82;

// RGB is the surface, ALPHA is city density - baked together so the night side needs no second
// texture and cannot disagree with the land underneath it.
Texture2D SurfaceTexture;
sampler2D SurfaceSampler : register(s0) = sampler_state
{
    Texture = <SurfaceTexture>;
    AddressU = Wrap;      // longitude has to wrap or the seam shows once per revolution
    AddressV = Clamp;
    MinFilter = Point;    // the art is pixel art; smoothing it here would undo that
    MagFilter = Point;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // The quad is the planet's bounding box, so put the sphere in the middle of it.
    // Scaled so the disc is radius 1 and the padding is whatever is left over.
    float2 p = (input.TextureCoordinates * 2.0 - 1.0) / DiscRadius;
    float r2 = dot(p, p);

    if (r2 > 1.0)
    {
        // Outside the disc: atmosphere. A weak ring all the way round so the night limb still draws
        // a line against the stars, and a much stronger arc on the side facing the star.
        // Thin. An atmosphere 16% of the planet's radius deep was a glowing hoop around the world;
        // the real thing is a hairline against the stars, and keeping it that way is most of the
        // difference between a photograph and a logo.
        float d = sqrt(r2);
        float fall = saturate(1.0 - (d - 1.0) / 0.07);
        if (fall <= 0.0)
            return float4(0, 0, 0, 0);
        float2 n = p / d;
        float facing = saturate(dot(n, SunDirection.xy));
        float3 halo = float3(0.16, 0.30, 0.58) * (fall * fall * 0.70)
                    + float3(0.40, 0.62, 1.0) * (fall * fall * pow(facing, 1.2) * 0.90);
        return float4(halo, saturate(fall * (0.40 + facing * 0.9)));
    }

    float3 n = float3(p, sqrt(max(0.0, 1.0 - r2)));
    float lambert = dot(n, normalize(SunDirection));

    // Sphere to strip. atan2 on x and z gives longitude, asin on y gives latitude; Rotation simply
    // slides the longitude along, which the wrapping sampler turns into endless spin.
    float lon = atan2(n.x, n.z) / 6.2831853 + Rotation;
    float lat = asin(clamp(n.y, -1.0, 1.0)) / 3.14159265 + 0.5;
    float4 surface = tex2D(SurfaceSampler, float2(lon, lat));

    // Day, and hardly anything else. There used to be a flat 0.22 added to every pixel on the globe,
    // which let ground already turned away from the star keep glowing grey - a lit zone past the
    // terminator that geometry says must be dark. Darkness has to fall out of the geometry, not be
    // subtracted back off afterwards by a second mask fighting a constant.
    //
    // What is left is a twentieth of that, and it earns its keep on the night side: it is multiplied
    // by albedo, so cloud tops at 0.9 come through as the faint moonlit swirls a real night side has
    // while ground at 0.25 stays black. A dark side that is pure black plus city lights reads as a
    // hole cut in the picture.
    float day = pow(saturate(lambert), 0.85);
    float3 lit = surface.rgb * (0.030 + 1.34 * day);

    // Dusk. Barely there on purpose: photographs from orbit show the terminator as a gradient into
    // black, and a warm streak bright enough to see as a line reads as a scratch drawn across the
    // globe. The warmth belongs to the air, not the ground.
    lit += SunColour * exp(-lambert * lambert / 0.0012) * 0.035;

    // Night: cities only. The curve matters, and so does how gentle it is - a city seen from orbit
    // is mostly outskirts, and a curve steep enough to make capitals pop will erase the suburbs that
    // give a city its shape, leaving the bare dot of light this was meant to stop being. The n.z term
    // dims them towards the limb, where street light leaves at a grazing angle.
    float night = saturate(-lambert * 11.0);
    float lamp = surface.a * (0.55 + 0.45 * surface.a) * saturate(0.30 + n.z);
    float3 lampColour = lerp(float3(1.0, 0.48, 0.14), float3(1.0, 0.88, 0.68), saturate(surface.a * 1.5));
    // 1.5 puts only the biggest cores over the post chain's bloom threshold, which is what is wanted:
    // capitals get a halo of glow the way they do from orbit, and every town below them stays a hard
    // pixel. Push this up and the bloom spreads over the lot, back into the smear this replaced.
    lit += lampColour * lamp * night * CityBrightness * 1.5;

    // Airglow: the upper atmosphere emits faintly in its own right, oxygen green, and towards the limb
    // the line of sight runs through far more of it. It is what stops the night limb being a hard
    // edge against the stars in photographs, and it costs one term.
    lit += float3(0.05, 0.11, 0.10) * night * pow(saturate(1.0 - n.z), 2.5) * 0.55;

    // The disc edge softens, but only slightly: with the star off to one side, the sunward limb is
    // the brightest part of the planet, and the heavy limb darkening this had before was dimming
    // precisely the wrong place.
    lit *= 1.0 - (1.0 - n.z) * 0.15;

    return float4(lit, 1.0);
}

technique Planet
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
