// The full-screen post chain the finished, lit scene is drawn through on its way to the backbuffer -
// after the world and its lighting, before the HUD (the HUD is drawn straight to the backbuffer, so
// it never picks up grain or bloom, and text stays crisp).
//
// Three techniques, driven by ScenePost in that order:
//   BrightPass - keeps only what is brighter than BloomThreshold, into a quarter-size target
//   Blur       - separable gaussian, run once horizontally and once vertically over that target
//   Composite  - scene + blurred highlights, then grade, vignette, aberration and grain
//
// The scene target holds unlit albedo and the light mask is high dynamic range, so the multiply that
// lights the frame happens in here rather than as a blend beforehand. That is the whole point of the
// arrangement: a lamp can be brighter than white, the bright pass sees its true brightness, and only
// then does the tone curve fold everything back into a displayable range. An earlier version of this
// file multiplied the light in with a fixed-function blend, which clamped every lamp to 1 before any
// shader could look at it, and needed a fudge factor to make bloom notice a lamp at all.
//
// With TonemapWhite, BloomStrength, GradeStrength, Vignette, GrainAmount, DitherAmount and Aberration
// at 0, Exposure at 1 and no light mask bound, Composite is a pixel-exact identity - which is what
// SpaceAdventure.ShaderCheck asserts, and what keeps every one of those knobs honest.

// WindowsDX only: there is no DesktopGL build of this game, so the #if OPENGL half of the usual
// MonoGame template would be a branch that has never once compiled - left out rather than left
// looking supported. level_9_3 rather than the 9_1 the Reach profile caps at, because 9_1 allows
// 64 arithmetic instructions in a pixel shader and both the post chain and per-pixel lighting blow
// straight past that; this is why Content.mgcb is on the HiDef profile. There is no VS_SHADERMODEL
// here on purpose - SpriteBatch supplies the vertex shader, this effect only replaces the pixel one.
#define PS_SHADERMODEL ps_4_0_level_9_3

// Linear scale on the scene before anything is added to it. 1 = untouched.
float Exposure = 1.0;

// Luminance a pixel has to beat before it is allowed to glow, and how much of the blurred result is
// added back on top of the scene.
float BloomThreshold = 0.62;
float BloomStrength = 0.85;

// How far the colour grade is pushed: shadows toward blue, highlights toward warm. 0 leaves the
// scene colours exactly as the renderer produced them.
float GradeStrength = 1.0;

// How much the frame darkens toward the corners, as a fraction at the very corner.
float Vignette = 0.18;

// Amplitude of the animated film grain, and the seed that animates it.
float GrainAmount = 0.020;
float Time = 0.0;

// Dither, in least significant bits of the 8-bit output. Kept separate from the grain above on
// purpose: grain is a look and may be turned off, whereas banding is a defect either way. This
// frame is mostly very dark, and a smooth ramp through near-black 8-bit values - which is exactly
// what the vignette and the bloom falloff produce - bands visibly. One LSB of noise breaks the
// bands and is itself invisible.
float DitherAmount = 0.0;

// Sideways split of the red and blue channels toward the edges of the screen. Scaled by the squared
// distance from centre, so the middle of the screen stays clean.
float Aberration = 0.35;

// One texel of the blur target, along the axis being blurred - set to (1/width, 0) for the
// horizontal pass and (0, 1/height) for the vertical one.
float2 BlurDirection = float2(0.0, 0.0);

// One texel of the full-size scene, for the gradient taps in the relief lighting below.
float2 TexelSize = float2(0.0, 0.0);

// Where the tone curve starts compressing. The scene target holds plain albedo and the light mask
// is high dynamic range, so albedo * light genuinely exceeds 1 wherever a lamp is bright - this is
// what folds that back into a displayable range instead of clipping every lamp to a white disc.
// 0 turns the curve off, which is what lets the identity check exist.
float TonemapWhite = 0.0;

// Screen-space relief. The scene doubles as its own height map (its luminance stands in for
// height) and the light mask doubles as its own direction field (its gradient points at whatever
// lamp is lighting the pixel), so surfaces catch light from the right side with no normal maps and
// no second geometry pass. It is a fake, and an honest one: it only fires where the picture
// actually has texture, which is why both terms are scaled by the gradient magnitude.
float ReliefStrength = 0.0;
float SpecularStrength = 0.0;

// How far the picture is pulled about where the distortion mask says something is venting. The mask
// is drawn by the game from the steam it is already spawning, so the shimmer lands exactly where
// atmosphere is escaping without anything new having to know where the breaches are.
float DistortionStrength = 0.0;

// SpriteBatch always binds the texture being drawn to slot 0, so the scene has to land on sampler
// register s0. What decides that is the order the shader first *reads* each sampler - not the order
// they are declared in, and not the register() annotations below, which were tried and did not
// override it. Every technique here therefore reads SpriteTextureSampler before any other sampler,
// including the deliberate-looking throwaway tap at the top of CompositePS. Get it wrong and the
// content still compiles, the effect still loads, and the frame comes out black.
Texture2D SpriteTexture;
sampler2D SpriteTextureSampler : register(s0) = sampler_state
{
    Texture = <SpriteTexture>;
};

// The blurred highlights, set by ScenePost before the composite pass.
Texture2D BloomTexture;
sampler2D BloomSampler : register(s1) = sampler_state
{
    Texture = <BloomTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

// The room/sight light mask RoomLighting already builds. Black where nothing set it, which makes
// every term that reads it fall to zero - the honest fallback when lighting did not build.
Texture2D LightTexture;
sampler2D LightSampler : register(s2) = sampler_state
{
    Texture = <LightTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Where the picture ripples: white blobs the game draws over its own venting steam. Black
// everywhere else, which is a shimmer of exactly zero.
Texture2D DistortionTexture;
sampler2D DistortionSampler : register(s3) = sampler_state
{
    Texture = <DistortionTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

// True surface normals for whatever geometry has a normal map, in the usual 0..1 encoding.
// Alpha is the flag: 0 means nothing was drawn here and the slope has to be estimated from the
// picture instead. That is what lets this cover part of the screen rather than all of it.
Texture2D NormalTexture;
sampler2D NormalSampler : register(s4) = sampler_state
{
    Texture = <NormalTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

static const float3 LumaWeights = float3(0.30, 0.59, 0.11);

float Luma(float3 c)
{
    return dot(c, LumaWeights);
}

float4 BrightPassPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;
    // Albedo times the light falling on it. The scene target deliberately holds unlit albedo and
    // the multiply happens here, so this pass sees the true, unclamped brightness of a lit surface
    // rather than one already flattened into 8 bits.
    float3 scene = tex2D(SpriteTextureSampler, uv).rgb * tex2D(LightSampler, uv).rgb * Exposure;
    float luma = dot(scene, LumaWeights);
    // Soft knee rather than a hard cut, so a lamp edging over the threshold fades in instead of
    // popping as the ship power drops.
    float keep = saturate((luma - BloomThreshold) / max(0.0001, 1.0 - BloomThreshold));
    return float4(scene * keep, 1.0);
}

// Five taps using the linear-sampling trick: the two outer taps sit between texels so the hardware
// filter folds two samples into one, giving a nine-tap gaussian for the price of five.
float4 BlurPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;
    float3 sum = tex2D(SpriteTextureSampler, uv).rgb * 0.2270270270;
    sum += (tex2D(SpriteTextureSampler, uv + BlurDirection * 1.3846153846).rgb
          + tex2D(SpriteTextureSampler, uv - BlurDirection * 1.3846153846).rgb) * 0.3162162162;
    sum += (tex2D(SpriteTextureSampler, uv + BlurDirection * 3.2307692308).rgb
          + tex2D(SpriteTextureSampler, uv - BlurDirection * 3.2307692308).rgb) * 0.0702702703;
    return float4(sum, 1.0);
}

float4 CompositePS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;
    float2 centred = uv * 2.0 - 1.0;
    float r2 = dot(centred, centred);

    // Touch the scene sampler before any other one. This looks pointless and is not: the register
    // a sampler ends up in follows the order the shader first reads it, and SpriteBatch always
    // binds the texture being drawn to s0. Read the distortion mask first and the scene slides off
    // s0, whereupon the shader samples the wrong texture and the frame comes out black - silently,
    // with the content still compiling perfectly. An explicit register() annotation did not
    // override it, so the ordering is the contract.
    float3 sceneAtPixel = tex2D(SpriteTextureSampler, uv).rgb;

    // Heat shimmer. Two out-of-phase waves rather than a noise texture: for something this small
    // and this blurred by its own mask, trigonometry is indistinguishable from noise and costs no
    // sampler. Only the scene is pulled about - the light mask is left where it is, so a lamp does
    // not wobble along with the air in front of it.
    float shimmer = tex2D(DistortionSampler, uv).r * DistortionStrength;
    // Two pairs at deliberately unrelated frequencies and speeds. A single sine pair reads as a
    // regular ripple the moment the disturbed area is bigger than its wavelength; summing an
    // incommensurable second pair gives a beat long enough that the eye stops finding the period.
    float2 wobble = float2(sin(uv.y * 140.0 + Time * 5.3), cos(uv.x * 120.0 + Time * 4.1))
                  + float2(sin(uv.y * 61.7 - Time * 2.9), cos(uv.x * 53.3 - Time * 3.7)) * 0.7;
    wobble *= shimmer * 0.0038;
    float2 suv = uv + wobble;

    // Red pulled outward and blue inward, by an amount that is zero at the centre of the screen.
    float2 split = centred * (r2 * Aberration * 0.004);
    float3 scene = sceneAtPixel;
    scene.r = tex2D(SpriteTextureSampler, suv + split).r;
    scene.g = tex2D(SpriteTextureSampler, suv).g;
    scene.b = tex2D(SpriteTextureSampler, suv - split).b;
    // The light multiply that used to happen with a fixed-function blend before this pass ever
    // ran. Doing it here is what allows the mask to carry values above 1 at all.
    scene *= tex2D(LightSampler, uv).rgb * Exposure;

    // Relief. The surface tilt is the negated gradient of the scene luminance; the direction the
    // light arrives from is the gradient of the light mask, which climbs toward whichever lamp is
    // lighting this pixel. Their alignment says whether this bit of metal faces the lamp or turns
    // away from it. Both terms are scaled by how much gradient there actually is, so flat paint and
    // flat panels are left completely alone - there is no relief to catch light on.
    float2 sceneTilt = -float2(
        Luma(tex2D(SpriteTextureSampler, suv + float2(TexelSize.x, 0.0)).rgb) - Luma(tex2D(SpriteTextureSampler, suv - float2(TexelSize.x, 0.0)).rgb),
        Luma(tex2D(SpriteTextureSampler, suv + float2(0.0, TexelSize.y)).rgb) - Luma(tex2D(SpriteTextureSampler, suv - float2(0.0, TexelSize.y)).rgb));
    float2 toLight = float2(
        Luma(tex2D(LightSampler, uv + float2(TexelSize.x, 0.0)).rgb) - Luma(tex2D(LightSampler, uv - float2(TexelSize.x, 0.0)).rgb),
        Luma(tex2D(LightSampler, uv + float2(0.0, TexelSize.y)).rgb) - Luma(tex2D(LightSampler, uv - float2(0.0, TexelSize.y)).rgb));

    // Where a real normal map was drawn, use it; everywhere else fall back to the luminance
    // estimate above. The mapped tilt is the genuine slope of the surface - it comes from the
    // same height field the visible tile was generated from - so it needs far less gain than the
    // guess does, and it does not mistake a painted edge for a physical one.
    float4 mapped = tex2D(NormalSampler, uv);
    float2 mappedTilt = mapped.xy * 2.0 - 1.0;
    float2 tiltVector = lerp(sceneTilt, mappedTilt, mapped.a);

    float tilt = length(tiltVector);
    float relief = saturate(tilt * lerp(12.0, 2.4, mapped.a));
    float align = dot(tiltVector / max(tilt, 0.00001), normalize(toLight + float2(0.000001, 0.000001)));
    float here = Luma(tex2D(LightSampler, uv).rgb);

    scene *= 1.0 + ReliefStrength * align * relief * here;
    scene += SpecularStrength * pow(saturate(align), 16.0) * relief * here;

    scene += tex2D(BloomSampler, uv).rgb * BloomStrength;

    // Extended Reinhard: compresses everything above the white point back into range while
    // leaving the dark two thirds of the picture - which is most of this game - almost untouched.
    // Plain Reinhard would lift the blacks and flatten exactly the part that matters here.
    if (TonemapWhite > 0.0)
    {
        float w2 = TonemapWhite * TonemapWhite;
        scene = scene * (1.0 + scene / w2) / (1.0 + scene);
    }

    // Grade: shadows drift blue, highlights drift warm. This is the cheapest thing in the whole
    // chain and does more for "lit by machinery in the cold" than anything else here.
    float luma = dot(scene, LumaWeights);
    float shadow = saturate(1.0 - luma * 2.0) * GradeStrength;
    float highlight = saturate((luma - 0.55) * 2.0) * GradeStrength;
    scene.b += 0.035 * shadow;
    scene.r -= 0.012 * shadow;
    scene.r += 0.035 * highlight;
    scene.b -= 0.022 * highlight;

    scene *= 1.0 - Vignette * r2;

    // Hash-based grain, moved every frame by Time so it reads as sensor noise rather than dirt on
    // the lens.
    float noise = frac(sin(dot(uv + frac(Time), float2(12.9898, 78.233))) * 43758.5453);
    scene += (noise - 0.5) * GrainAmount;

    float dither = frac(sin(dot(uv, float2(171.0, 231.0)) + frac(Time)) * 43758.5453);
    scene += (dither - 0.5) * DitherAmount * (1.0 / 255.0);

    return float4(saturate(scene), 1.0);
}

technique Composite
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL CompositePS();
    }
};

technique BrightPass
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL BrightPassPS();
    }
};

technique Blur
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL BlurPS();
    }
};
