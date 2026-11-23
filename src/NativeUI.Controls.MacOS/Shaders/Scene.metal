// Renders a Viewport3D scene: a gradient sky behind instanced, flat-shaded meshes.
// The structs match SceneFrame, SceneLight, MeshInstance, and SceneVertex in NativeUI.Graphics3D.
// Colors are sRGB channels in [0, 1], and outputs are premultiplied by alpha.

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
    float ambientLight;
    uint lightCount;
    float2 reserved;
};

struct Light
{
    float4 direction;
    float4 color;
};

struct Instance
{
    float4x4 world;
    float4x4 rotation;
    float4 color;
};

struct Vertex
{
    packed_float3 position;
    packed_float3 normal;
};

struct SkyOutput
{
    float4 position [[position]];
    float2 ndc;
};

struct MeshOutput
{
    float4 position [[position]];
    float3 normal;
    float4 color;
};

// Covers the viewport with one triangle whose corners are (-1, -1), (3, -1), and (-1, 3).
vertex SkyOutput skyVertex(uint vertexId [[vertex_id]])
{
    float2 corner = float2(float((vertexId << 1) & 2), float(vertexId & 2)) * 2.0 - 1.0;
    SkyOutput output;
    output.position = float4(corner, 0.0, 1.0);
    output.ndc = corner;
    return output;
}

// Matches GradientSky.Blend: eases from one color to another by elevation, then applies the energy multiplier.
static float3 blendSky(float3 from, float3 to, float elevation, float curve, float energy)
{
    float amount = clamp(1.0 - pow(1.0 - elevation, 1.0 / max(curve, 0.001)), 0.0, 1.0);
    return min(float3(1.0), mix(from, to, amount) * energy);
}

// Matches GradientSky.GetColor for the view ray through each pixel.
fragment float4 skyFragment(SkyOutput input [[stage_in]], constant Frame& frame [[buffer(0)]])
{
    if (frame.skyParameters.w == 0.0)
    {
        discard_fragment();
    }

    float4 near = frame.inverseViewProjection * float4(input.ndc, 0.0, 1.0);
    float4 far = frame.inverseViewProjection * float4(input.ndc, 1.0, 1.0);
    float3 direction = normalize(far.xyz / far.w - near.xyz / near.w);
    float angleFromTop = acos(clamp(direction.y, -1.0, 1.0));
    float3 color = angleFromTop <= M_PI_2_F
        ? blendSky(frame.skyHorizonColor.rgb, frame.skyTopColor.rgb, 1.0 - angleFromTop / M_PI_2_F, frame.skyParameters.x, frame.skyParameters.z)
        : blendSky(frame.groundHorizonColor.rgb, frame.groundColor.rgb, angleFromTop / M_PI_2_F - 1.0, frame.skyParameters.y, frame.skyParameters.z);
    return float4(color, 1.0);
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
    MeshOutput output;
    output.position = frame.viewProjection * instance.world * float4(float3(source.position), 1.0);
    output.normal = (instance.rotation * float4(float3(source.normal), 0.0)).xyz;
    output.color = instance.color;
    return output;
}

// Matches SceneRenderer.ShadeFace: the surface color times the ambient light plus each light facing the surface.
fragment float4 meshFragment(
    MeshOutput input [[stage_in]],
    constant Frame& frame [[buffer(0)]],
    const device Light* lights [[buffer(1)]])
{
    float3 normal = normalize(input.normal);
    float3 received = float3(frame.ambientLight);
    for (uint i = 0; i < frame.lightCount; i++)
    {
        float facing = dot(normal, lights[i].direction.xyz);
        if (facing > 0.0)
        {
            received += lights[i].color.rgb * facing;
        }
    }

    float3 color = min(float3(1.0), input.color.rgb * received);
    return float4(color * input.color.a, input.color.a);
}
