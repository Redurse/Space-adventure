// One shadowed room lamp, rasterised as a triangle fan and shaded per pixel.
//
// The fan itself only carries the shape of the light: where its rays reach before a wall stops them
// (ShadowCast). How bright the light is at any point inside that shape used to be baked into the
// vertex colours - the centre vertex full strength, each rim vertex faded by its own distance - and
// then linearly interpolated across the triangle. That makes the falloff piecewise linear along
// however many rays the caster happened to emit, which reads as a faintly faceted pool rather than
// a round one. Here the vertices carry the plain lamp colour and the distance is measured per pixel
// instead, so the pool is smooth no matter how coarse the fan is.
//
// This is also the only effect in the project with a vertex shader of its own. Post.fx replaces
// nothing but the pixel stage because SpriteBatch supplies its own vertex shader; this one is drawn
// with DrawUserPrimitives, where there is no such thing to inherit.

#define VS_SHADERMODEL vs_4_0_level_9_3
#define PS_SHADERMODEL ps_4_0_level_9_3

float4x4 WorldViewProjection;

// Centre and reach of the lamp being drawn, in the same untransformed space as the vertices - that
// is, pixels before the render scale is applied.
float2 LightCenter;
float LightRadius;

// Fraction of the radius that stays at full brightness before the fade begins. A room lamp is meant
// to fill its compartment evenly rather than read as a hotspot in the middle of it.
float FalloffStart;

// How much brighter than white the centre of the lamp may get. The mask this renders into is a
// floating point target precisely so this can exceed 1.
float Intensity;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 World : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, WorldViewProjection);
    output.Color = input.Color;
    // Untransformed, so the pixel stage can measure distance in the same units LightCenter is in.
    output.World = input.Position.xy;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float t = saturate(distance(input.World, LightCenter) / max(LightRadius, 0.0001));
    float fade = 1.0 - saturate((t - FalloffStart) / max(1.0 - FalloffStart, 0.0001));
    // Squared, matching the curve the CPU path used, so switching between them does not change the
    // look of a room - only how smooth it is.
    fade *= fade;
    return float4(input.Color.rgb * fade * Intensity, 1.0);
}

technique Light
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
