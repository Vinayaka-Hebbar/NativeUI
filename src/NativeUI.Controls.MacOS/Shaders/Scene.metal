// Renders a Viewport3D scene: a gradient sky behind instanced, lit, and fogged meshes, then optionally finishes the image
// with glow, light shafts, tone mapping, color adjustments, and a vignette.
// The structs match SceneFrame, SceneLight, MeshInstance, SceneVertex, and PostProcessFrame in NativeUI.Graphics3D.
// Sky colors are sRGB channels in [0, 1]. Every other color is in linear light. The standard fragment functions write
// clamped sRGB, and the HDR fragment functions write unclamped linear light for the post-processing passes. Outputs are
// premultiplied by alpha.

#include <metal_stdlib>
using namespace metal;

struct Frame
{
    float4x4 viewProjection;
    float4x4 inverseViewProjection;
    float4 skyTopColor;
    float4 skyHorizonColor;
    float4 groundHorizonColor;
    float4 groundColor;

    // Sky curve, ground curve, energy multiplier, and 1 when the scene has a sky.
    float4 skyParameters;
    float4 cameraPosition;
    float4 fogColor;

    // Linear near and far distances, exponential density, and the fog mode: 0 none, 1 linear, 2 exponential.
    float4 fogParameters;
    float4 hemisphereSkyColor;
    float4 hemisphereGroundColor;

    // Caustics color scaled by intensity, and 1 in W when the scene has caustics.
    float4 causticsColor;

    // Caustics cell size in world units, and caustics time in seconds.
    float4 causticsParameters;

    // Unit horizontal wind direction, and wind strength in W.
    float4 windDirection;

    // Wind turbulence, frequency, time in seconds, and 1 when the scene has wind.
    float4 windParameters;
    float ambientLight;
    uint lightCount;
    float fogAerialPerspective;
    float reserved;
};

struct Light
{
    float4 direction;
    float4 color;
};

struct Instance
{
    float4x4 world;
    float4x4 normalTransform;
    float4 color;
    float4 emissiveColor;

    // Detail scale, detail amount, and wind response.
    float4 materialParameters;
};

struct Vertex
{
    packed_float3 position;
    packed_float3 normal;
    packed_float4 color;
};

struct PostFrame
{
    // Exposure, tone mapper (0 linear, 1 ACES), brightness, and contrast.
    float4 tonemap;

    // Saturation, glow intensity, glow threshold, and glow strength.
    float4 adjustments;

    // Light shaft color scaled by intensity, and visibility in W.
    float4 lightShaftColor;

    // Light position in texture coordinates, and decay.
    float4 lightShaftParameters;

    // Vignette color, and intensity in W.
    float4 vignetteColor;

    // Vignette smoothness, and image aspect ratio.
    float4 vignetteParameters;
};

struct SkyOutput
{
    float4 position [[position]];
    float2 ndc;
};

struct MeshOutput
{
    float4 position [[position]];
    float3 worldPosition;
    float3 normal;
    float4 color;
    float3 emissiveColor;
    float3 detailPosition;
    float detailAmount;
};

struct PostOutput
{
    float4 position [[position]];
    float2 uv;
};

constexpr sampler s_linearClamp(filter::linear, address::clamp_to_edge);
constant float3 s_luminance = float3(0.2126, 0.7152, 0.0722);

// Returns the corner of one triangle covering the viewport: (-1, -1), (3, -1), or (-1, 3).
static float2 fullScreenCorner(uint vertexId)
{
    return float2(float((vertexId << 1) & 2), float(vertexId & 2)) * 2.0 - 1.0;
}

static float3 srgbToLinear(float3 color)
{
    return select(pow((color + 0.055) / 1.055, 2.4), color / 12.92, color <= 0.04045);
}

static float3 linearToSrgb(float3 color)
{
    color = saturate(color);
    return select(1.055 * pow(color, 1.0 / 2.4) - 0.055, color * 12.92, color <= 0.0031308);
}

// Matches GradientSky.Blend: eases from one color to another by elevation, then applies the energy multiplier.
static float3 blendSky(float3 from, float3 to, float elevation, float curve, float energy)
{
    float amount = clamp(1.0 - pow(1.0 - elevation, 1.0 / max(curve, 0.001)), 0.0, 1.0);
    return min(float3(1.0), mix(from, to, amount) * energy);
}

// Matches GradientSky.GetColor: the sRGB sky color along a unit direction.
static float3 skyColor(constant Frame& frame, float3 direction)
{
    float angleFromTop = acos(clamp(direction.y, -1.0, 1.0));
    return angleFromTop <= M_PI_2_F
        ? blendSky(frame.skyHorizonColor.rgb, frame.skyTopColor.rgb, 1.0 - angleFromTop / M_PI_2_F, frame.skyParameters.x, frame.skyParameters.z)
        : blendSky(frame.groundHorizonColor.rgb, frame.groundColor.rgb, angleFromTop / M_PI_2_F - 1.0, frame.skyParameters.y, frame.skyParameters.z);
}

static float3 skyColorAt(constant Frame& frame, float2 ndc)
{
    float4 near = frame.inverseViewProjection * float4(ndc, 0.0, 1.0);
    float4 far = frame.inverseViewProjection * float4(ndc, 1.0, 1.0);
    return skyColor(frame, normalize(far.xyz / far.w - near.xyz / near.w));
}

vertex SkyOutput skyVertex(uint vertexId [[vertex_id]])
{
    float2 corner = fullScreenCorner(vertexId);
    SkyOutput output;
    output.position = float4(corner, 0.0, 1.0);
    output.ndc = corner;
    return output;
}

fragment float4 skyFragment(SkyOutput input [[stage_in]], constant Frame& frame [[buffer(0)]])
{
    if (frame.skyParameters.w == 0.0)
    {
        discard_fragment();
    }

    return float4(skyColorAt(frame, input.ndc), 1.0);
}

fragment float4 skyFragmentHdr(SkyOutput input [[stage_in]], constant Frame& frame [[buffer(0)]])
{
    if (frame.skyParameters.w == 0.0)
    {
        discard_fragment();
    }

    return float4(srgbToLinear(skyColorAt(frame, input.ndc)), 1.0);
}

// Matches SceneNoise.Hash: an integer hash of a lattice point, in [0, 1).
static float latticeHash(float3 cell)
{
    int3 lattice = int3(cell);
    uint hash = (uint(lattice.x) * 73856093u) ^ (uint(lattice.y) * 19349663u) ^ (uint(lattice.z) * 83492791u);
    hash ^= hash >> 13;
    hash *= 0x5bd1e995u;
    hash ^= hash >> 15;
    return float(hash & 0xFFFFFFu) / 16777216.0;
}

// Matches SceneNoise.GetValue: smooth value noise.
static float valueNoise(float3 position)
{
    float3 cell = floor(position);
    float3 local = position - cell;
    float3 blend = local * local * (3.0 - 2.0 * local);
    float x00 = mix(latticeHash(cell), latticeHash(cell + float3(1, 0, 0)), blend.x);
    float x10 = mix(latticeHash(cell + float3(0, 1, 0)), latticeHash(cell + float3(1, 1, 0)), blend.x);
    float x01 = mix(latticeHash(cell + float3(0, 0, 1)), latticeHash(cell + float3(1, 0, 1)), blend.x);
    float x11 = mix(latticeHash(cell + float3(0, 1, 1)), latticeHash(cell + float3(1, 1, 1)), blend.x);
    return mix(mix(x00, x10, blend.y), mix(x01, x11, blend.y), blend.z);
}

// Matches SceneNoise.GetFractal: three octaves, normalized to [0, 1].
static float fractalNoise(float3 position)
{
    float sum = 0.0;
    float weight = 0.5;
    float total = 0.0;
    for (int octave = 0; octave < 3; octave++)
    {
        sum += valueNoise(position) * weight;
        total += weight;
        position = position * 2.03 + float3(17.1, 9.2, 3.7);
        weight *= 0.5;
    }

    return sum / total;
}

// Bends a vertex along the wind by the square of its height in geometry coordinates, scaled by the instance's height.
// The sway's phase varies with the instance's position, so neighbors move out of step.
static float3 windOffset(constant Frame& frame, Instance instance, float3 localPosition)
{
    float response = instance.materialParameters.z;
    if (frame.windParameters.w == 0.0 || response == 0.0)
    {
        return float3(0.0);
    }

    float3 origin = instance.world[3].xyz;
    float height = length(instance.world[1].xyz);
    float phase = dot(origin.xz, float2(0.37, 0.21));
    float time = frame.windParameters.z * frame.windParameters.y * 2.0 * M_PI_F;
    float sway = sin(time + phase) + frame.windParameters.x * sin(time * 2.3 + phase * 1.7);
    float bend = frame.windDirection.w * response * localPosition.y * localPosition.y * sway;
    return frame.windDirection.xyz * bend * height;
}

vertex MeshOutput meshVertex(
    uint vertexId [[vertex_id]],
    uint instanceId [[instance_id]],
    const device Vertex* vertices [[buffer(0)]],
    const device Instance* instances [[buffer(1)]],
    constant Frame& frame [[buffer(2)]])
{
    Vertex source = vertices[vertexId];
    Instance instance = instances[instanceId];
    float3 localPosition = float3(source.position);
    float4 worldPosition = instance.world * float4(localPosition, 1.0);
    worldPosition.xyz += windOffset(frame, instance, localPosition);

    MeshOutput output;
    output.position = frame.viewProjection * worldPosition;
    output.worldPosition = worldPosition.xyz;
    output.normal = (instance.normalTransform * float4(float3(source.normal), 0.0)).xyz;
    output.color = instance.color * float4(source.color);
    output.emissiveColor = instance.emissiveColor.rgb;

    // Detail follows the geometry, offset by the instance position so copies differ.
    output.detailPosition = localPosition / instance.materialParameters.x + instance.world[3].xyz * 0.37;
    output.detailAmount = instance.materialParameters.y;
    return output;
}

// Matches Caustics.GetLayer: bent sine waves that cancel along thin curves, sharpened into bright lines.
static float causticsLayer(float2 position, float seconds)
{
    float2 bent = position + 0.6 * float2(sin(position.y * 1.3 + seconds), cos(position.x * 1.1 - seconds * 0.8));
    float waves = sin(bent.x * 2.1 + seconds * 0.7) + sin(bent.y * 2.3 - seconds * 0.9) + sin((bent.x + bent.y) * 1.7 + seconds * 0.5);
    return pow(saturate(1.0 - abs(waves) / 1.5), 6.0);
}

// Matches Caustics.GetPattern.
static float causticsPattern(float2 position, float seconds)
{
    return causticsLayer(position, seconds) + 0.6 * causticsLayer(position * 1.7 + float2(3.1, 1.3), seconds * 1.3);
}

// Matches Fog.GetAmount and FogExp2.GetAmount.
static float fogAmount(constant Frame& frame, float distance)
{
    if (frame.fogParameters.w == 1.0)
    {
        float near = frame.fogParameters.x;
        float far = frame.fogParameters.y;
        return far <= near ? step(far, distance) : saturate((distance - near) / (far - near));
    }

    float thickness = frame.fogParameters.z * distance;
    return frame.fogParameters.w == 2.0 ? 1.0 - exp(-thickness * thickness) : 0.0;
}

// Matches SceneRenderer.ShadeFace: in linear light, the surface color with its procedural detail times the ambient light,
// the hemisphere light, the caustics on surfaces facing up, and each directional light facing the surface, plus the
// emissive color, blended toward the fog color by distance. Aerial perspective blends the fog color toward the sky along
// the view ray. The detail also tilts the normal by the noise gradient, which the polygon renderer does not.
static float3 shadeMesh(MeshOutput input, constant Frame& frame, const device Light* lights, bool isClamped)
{
    float3 normal = normalize(input.normal);
    float3 surface = input.color.rgb;
    if (input.detailAmount > 0.0)
    {
        float noise = fractalNoise(input.detailPosition);
        const float step = 0.08;
        float3 gradient = float3(
            fractalNoise(input.detailPosition + float3(step, 0, 0)) - noise,
            fractalNoise(input.detailPosition + float3(0, step, 0)) - noise,
            fractalNoise(input.detailPosition + float3(0, 0, step)) - noise) / step;
        normal = normalize(normal - gradient * input.detailAmount * 0.35);
        surface *= 1.0 + input.detailAmount * (noise - 0.5) * 1.2;
    }

    float3 received = float3(frame.ambientLight)
        + mix(frame.hemisphereGroundColor.rgb, frame.hemisphereSkyColor.rgb, normal.y * 0.5 + 0.5);
    for (uint i = 0; i < frame.lightCount; i++)
    {
        float facing = dot(normal, lights[i].direction.xyz);
        if (facing > 0.0)
        {
            received += lights[i].color.rgb * facing;
        }
    }

    if (frame.causticsColor.w != 0.0 && normal.y > 0.0)
    {
        float2 position = input.worldPosition.xz / frame.causticsParameters.x;
        received += frame.causticsColor.rgb * causticsPattern(position, frame.causticsParameters.y) * normal.y;
    }

    float3 color = surface * received + input.emissiveColor;
    if (isClamped)
    {
        color = min(float3(1.0), color);
    }

    float3 toSurface = input.worldPosition - frame.cameraPosition.xyz;
    float distance = length(toSurface);
    float3 fogColor = frame.fogColor.rgb;
    if (frame.fogAerialPerspective > 0.0 && frame.skyParameters.w != 0.0)
    {
        fogColor = mix(fogColor, srgbToLinear(skyColor(frame, toSurface / max(distance, 0.0001))), frame.fogAerialPerspective);
    }

    return mix(color, fogColor, fogAmount(frame, distance));
}

fragment float4 meshFragment(
    MeshOutput input [[stage_in]],
    constant Frame& frame [[buffer(0)]],
    const device Light* lights [[buffer(1)]])
{
    float3 color = shadeMesh(input, frame, lights, true);
    return float4(linearToSrgb(color) * input.color.a, input.color.a);
}

fragment float4 meshFragmentHdr(
    MeshOutput input [[stage_in]],
    constant Frame& frame [[buffer(0)]],
    const device Light* lights [[buffer(1)]])
{
    float3 color = shadeMesh(input, frame, lights, false);
    return float4(color * input.color.a, input.color.a);
}

vertex PostOutput postVertex(uint vertexId [[vertex_id]])
{
    float2 corner = fullScreenCorner(vertexId);
    PostOutput output;
    output.position = float4(corner, 0.0, 1.0);
    output.uv = float2(corner.x * 0.5 + 0.5, 0.5 - corner.y * 0.5);
    return output;
}

// Averages 13 taps around a point in overlapping boxes, which halves resolution without flicker.
static float3 downsample(texture2d<float> source, float2 uv, float2 texel)
{
    float3 a = source.sample(s_linearClamp, uv + texel * float2(-2, -2)).rgb;
    float3 b = source.sample(s_linearClamp, uv + texel * float2(0, -2)).rgb;
    float3 c = source.sample(s_linearClamp, uv + texel * float2(2, -2)).rgb;
    float3 d = source.sample(s_linearClamp, uv + texel * float2(-2, 0)).rgb;
    float3 e = source.sample(s_linearClamp, uv).rgb;
    float3 f = source.sample(s_linearClamp, uv + texel * float2(2, 0)).rgb;
    float3 g = source.sample(s_linearClamp, uv + texel * float2(-2, 2)).rgb;
    float3 h = source.sample(s_linearClamp, uv + texel * float2(0, 2)).rgb;
    float3 i = source.sample(s_linearClamp, uv + texel * float2(2, 2)).rgb;
    float3 j = source.sample(s_linearClamp, uv + texel * float2(-1, -1)).rgb;
    float3 k = source.sample(s_linearClamp, uv + texel * float2(1, -1)).rgb;
    float3 l = source.sample(s_linearClamp, uv + texel * float2(-1, 1)).rgb;
    float3 m = source.sample(s_linearClamp, uv + texel * float2(1, 1)).rgb;
    return e * 0.125 + (a + c + g + i) * 0.03125 + (b + d + f + h) * 0.0625 + (j + k + l + m) * 0.125;
}

// Keeps the light above the glow threshold, with a soft knee that fades in pixels just below it.
fragment float4 glowThreshold(
    PostOutput input [[stage_in]],
    texture2d<float> scene [[texture(0)]],
    constant PostFrame& post [[buffer(0)]],
    constant float2& texel [[buffer(1)]])
{
    float3 color = downsample(scene, input.uv, texel);
    float brightness = max(color.r, max(color.g, color.b));
    float threshold = post.adjustments.z;
    float knee = threshold * 0.5 + 0.0001;
    float soft = clamp(brightness - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee);
    float contribution = max(soft, brightness - threshold) / max(brightness, 0.0001);
    return float4(color * contribution, 1.0);
}

fragment float4 glowDownsample(
    PostOutput input [[stage_in]],
    texture2d<float> source [[texture(0)]],
    constant float2& texel [[buffer(1)]])
{
    return float4(downsample(source, input.uv, texel), 1.0);
}

// Spreads a smaller glow level over the next larger one with a 3x3 tent filter, weighted by the glow strength. The
// pipeline adds the result to the larger level.
fragment float4 glowUpsample(
    PostOutput input [[stage_in]],
    texture2d<float> source [[texture(0)]],
    constant PostFrame& post [[buffer(0)]],
    constant float2& texel [[buffer(1)]])
{
    float3 sum = source.sample(s_linearClamp, input.uv).rgb * 4.0;
    sum += (source.sample(s_linearClamp, input.uv + texel * float2(-1, 0)).rgb
        + source.sample(s_linearClamp, input.uv + texel * float2(1, 0)).rgb
        + source.sample(s_linearClamp, input.uv + texel * float2(0, -1)).rgb
        + source.sample(s_linearClamp, input.uv + texel * float2(0, 1)).rgb) * 2.0;
    sum += source.sample(s_linearClamp, input.uv + texel * float2(-1, -1)).rgb
        + source.sample(s_linearClamp, input.uv + texel * float2(1, -1)).rgb
        + source.sample(s_linearClamp, input.uv + texel * float2(-1, 1)).rgb
        + source.sample(s_linearClamp, input.uv + texel * float2(1, 1)).rgb;
    return float4(sum / 16.0 * post.adjustments.w, 1.0);
}

// Streams the bright parts of the image away from the light by sampling along the line toward it, each sample weaker
// by the decay.
fragment float4 lightShafts(
    PostOutput input [[stage_in]],
    texture2d<float> scene [[texture(0)]],
    constant PostFrame& post [[buffer(0)]])
{
    const int samples = 48;
    float2 toLight = post.lightShaftParameters.xy - input.uv;
    float2 stepSize = toLight / float(samples) * 0.9;
    float2 uv = input.uv;
    float weight = 1.0;
    float3 sum = float3(0.0);
    for (int i = 0; i < samples; i++)
    {
        uv += stepSize;
        float3 color = scene.sample(s_linearClamp, uv).rgb;
        float bright = saturate((dot(color, s_luminance) - 0.25) * 2.0);
        sum += color * bright * weight;
        weight *= post.lightShaftParameters.z;
    }

    return float4(sum / float(samples) * post.lightShaftColor.rgb * post.lightShaftColor.w, 1.0);
}

// Matches SceneLighting.ToneMapAces.
static float3 toneMapAces(float3 color)
{
    color = max(color, 0.0);
    return saturate(color * (2.51 * color + 0.03) / (color * (2.43 * color + 0.59) + 0.14));
}

// Matches SceneLighting.Finish, then adds glow and light shafts before it and the vignette after it.
fragment float4 composite(
    PostOutput input [[stage_in]],
    texture2d<float> scene [[texture(0)]],
    texture2d<float> glow [[texture(1)]],
    texture2d<float> shafts [[texture(2)]],
    constant PostFrame& post [[buffer(0)]])
{
    float4 sceneColor = scene.sample(s_linearClamp, input.uv);
    float3 color = sceneColor.rgb;
    if (post.adjustments.w > 0.0)
    {
        color += glow.sample(s_linearClamp, input.uv).rgb * post.adjustments.y;
    }

    if (post.lightShaftColor.w > 0.0)
    {
        color += shafts.sample(s_linearClamp, input.uv).rgb;
    }

    color *= post.tonemap.x;
    color = post.tonemap.y == 1.0 ? toneMapAces(color) : saturate(color);
    float3 encoded = linearToSrgb(color) * post.tonemap.z;
    encoded = (encoded - 0.5) * post.tonemap.w + 0.5;
    encoded = mix(float3(dot(encoded, s_luminance)), encoded, post.adjustments.x);

    float2 centered = (input.uv - 0.5) * float2(post.vignetteParameters.y, 1.0);
    float smoothness = post.vignetteParameters.x;
    float vignette = smoothstep(0.75 - smoothness * 0.5, 0.75 + smoothness * 0.25, length(centered) * 1.2);
    encoded = mix(encoded, linearToSrgb(post.vignetteColor.rgb), vignette * post.vignetteColor.w);

    return float4(saturate(encoded) * sceneColor.a, sceneColor.a);
}
