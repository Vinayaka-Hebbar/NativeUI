using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Graphics3D;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Sample;

/// <summary>
/// Underwater scene compiled from <c>UnderwaterSceneView.xaml</c>: a procedural seabed with boulders, rocks, branching
/// corals, seagrass, kelp groves, schools of fish, drifting marine snow, and moving caustics. The GPU sways the plants in
/// the wind, and the image is finished with glow, light shafts, tone mapping, and a vignette. Dragging orbits the camera around the center,
/// which otherwise circles slowly on its own.
/// </summary>
public partial class UnderwaterSceneView
{
    private const float SeabedSize = 320;
    private const int SeabedCells = 160;
    private const float ScatterRadius = 55;
    private const int BoulderCount = 32;
    private const int RockCount = 520;
    private const int CoralCount = 260;
    private const int GrassCount = 30000;
    private const int KelpGroveCount = 30;
    private const int KelpPerGrove = 40;
    private const int FishPerSchool = 90;
    private const int MarineSnowCount = 2200;
    private const float MarineSnowRadius = 30;
    private const float MarineSnowTop = 18;

    // Specks closer to the camera than this are hidden. A speck in front of the lens fills a large part of the view.
    private const float MarineSnowNearLimit = 2;

    // Kelp stays at least OrbitClearance away from the circle the camera orbits on.
    private const float OrbitRadius = 16;
    private const float OrbitClearance = 5;
    private const float OrbitRadiansPerSecond = 0.035f;
    private const float RadiansPerDragPixel = 0.005f;
    private const float MinimumPitch = -0.12f;
    private const float MaximumPitch = 1.1f;
    private const float CameraClearance = 0.8f;

    private static readonly Vector3 s_orbitTarget = new(0, 2, 0);
    private static readonly Color[] s_boulderColors = [Color.FromHex("#7E8B88"), Color.FromHex("#72807F"), Color.FromHex("#868A7C")];
    private static readonly Color[] s_rockColors = [Color.FromHex("#87908E"), Color.FromHex("#928C7C"), Color.FromHex("#7A8689")];
    private static readonly Color[] s_coralColors =
    [
        Color.FromHex("#C96A8A"), Color.FromHex("#A77BC4"), Color.FromHex("#E09A72"), Color.FromHex("#D9CFC4"), Color.FromHex("#B85C5C")
    ];
    private static readonly Color[] s_grassColors = [Color.FromHex("#4D7F45"), Color.FromHex("#3E6E3E"), Color.FromHex("#6E8A3C")];
    private static readonly Color[] s_kelpColors = [Color.FromHex("#466B2E"), Color.FromHex("#567A2A"), Color.FromHex("#365A28")];

    private static readonly FishSchool[] s_schools =
    [
        new(Radius: 11, Height: 5, RadiansPerSecond: 0.32f, Color.FromHex("#B9C9CF")),
        new(Radius: 24, Height: 9, RadiansPerSecond: -0.2f, Color.FromHex("#A9BCC6")),
        new(Radius: 7, Height: 1.5f, RadiansPerSecond: 0.45f, Color.FromHex("#D98B3A")),
        new(Radius: 32, Height: 12, RadiansPerSecond: 0.14f, Color.FromHex("#C3D2D6"))
    ];

    private readonly FishMember[] _fish;
    private readonly Particle[] _marineSnow;
    private float _yaw;
    private float _pitch = 0.1f;
    private long? _dragPointerId;
    private Point _lastDragPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnderwaterSceneView"/> class and generates the scene.
    /// </summary>
    public UnderwaterSceneView()
    {
        InitializeComponent();

        Random random = new(7);
        Seabed.Geometry = UnderwaterGeometry.CreateSeabed(SeabedSize, SeabedCells);

        Boulders.Geometry = UnderwaterGeometry.CreateRock(seed: 5);
        (Boulders.InstanceTransforms, Boulders.InstanceColors) = CreateBoulders(random);

        Rocks.Geometry = UnderwaterGeometry.CreateRock(seed: 3);
        (Rocks.InstanceTransforms, Rocks.InstanceColors) = Scatter(
            random,
            RockCount,
            s_rockColors,
            static random =>
            {
                float size = 0.3f + (1.9f * random.NextSingle() * random.NextSingle());
                return new Vector3(size, size * (0.5f + (0.3f * random.NextSingle())), size);
            });

        Corals.Geometry = UnderwaterGeometry.CreateCoral(seed: 11);
        (Corals.InstanceTransforms, Corals.InstanceColors) = Scatter(
            random,
            CoralCount,
            s_coralColors,
            static random => new Vector3(0.8f + (1.6f * random.NextSingle())));

        Grass.Geometry = UnderwaterGeometry.CreateGrassBlade();
        SetPlants(Grass, CreateGrass(random));

        Kelp.Geometry = UnderwaterGeometry.CreateKelp(seed: 17);
        SetPlants(Kelp, CreateKelp(random));

        Fish.Geometry = UnderwaterGeometry.CreateFish();
        _fish = CreateFish(random);
        Fish.InstanceColors = [.. _fish.Select(static fish => s_schools[fish.School].Color)];

        _marineSnow = CreateMarineSnow(random);

        SceneView.PointerPressed += OnPointerPressed;
        SceneView.PointerMoved += OnPointerMoved;
        SceneView.PointerReleased += OnPointerReleased;
        SceneView.PointerCaptureLost += (_, _) => _dragPointerId = null;
        SceneView.Rendering += (_, args) => Animate((float)args.TotalTime.TotalSeconds, (float)args.DeltaTime.TotalSeconds);
        Animate(0, 0);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Buttons.HasFlag(PointerButtons.Primary) && SceneView.CapturePointer(e.PointerId))
        {
            _dragPointerId = e.PointerId;
            _lastDragPosition = e.Position;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPointerId != e.PointerId)
        {
            return;
        }

        _yaw -= (float)(e.Position.X - _lastDragPosition.X) * RadiansPerDragPixel;
        _pitch = Math.Clamp(_pitch + ((float)(e.Position.Y - _lastDragPosition.Y) * RadiansPerDragPixel), MinimumPitch, MaximumPitch);
        _lastDragPosition = e.Position;
        UpdateCamera();
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (_dragPointerId == e.PointerId)
        {
            _dragPointerId = null;
            SceneView.ReleasePointerCapture(e.PointerId);
        }
    }

    private void Animate(float seconds, float deltaSeconds)
    {
        if (_dragPointerId is null)
        {
            _yaw += OrbitRadiansPerSecond * deltaSeconds;
        }

        UpdateCamera();
        SeabedCaustics.Time = TimeSpan.FromSeconds(seconds);
        Current.Time = TimeSpan.FromSeconds(seconds);
        Fish.InstanceTransforms = GetFishTransforms(seconds);
        MarineSnow.InstanceTransforms = GetMarineSnowTransforms(seconds);
    }

    /// <summary>
    /// Places the camera on a sphere around the orbit target at the current yaw and pitch, kept above the seabed.
    /// </summary>
    private void UpdateCamera()
    {
        Vector3 offset = new(
            MathF.Cos(_pitch) * MathF.Sin(_yaw),
            MathF.Sin(_pitch),
            -MathF.Cos(_pitch) * MathF.Cos(_yaw));
        Vector3 position = s_orbitTarget + (offset * OrbitRadius);
        position.Y = Math.Max(position.Y, UnderwaterGeometry.GetSeabedHeight(position.X, position.Z) + CameraClearance);
        Camera.Position = position;
        Camera.LookAt = s_orbitTarget;
    }

    /// <summary>
    /// Places each plant once. The scene's wind sways them on the GPU.
    /// </summary>
    private static void SetPlants(InstancedMesh mesh, Plant[] plants)
    {
        ImmutableArray<Matrix4x4>.Builder transforms = ImmutableArray.CreateBuilder<Matrix4x4>(plants.Length);
        ImmutableArray<Color>.Builder colors = ImmutableArray.CreateBuilder<Color>(plants.Length);
        foreach (Plant plant in plants)
        {
            transforms.Add(
                Matrix4x4.CreateScale(plant.Width, plant.Height, plant.Width) *
                Matrix4x4.CreateRotationY(plant.Yaw) *
                Matrix4x4.CreateTranslation(plant.Position));
            colors.Add(plant.Color);
        }

        mesh.InstanceTransforms = transforms.MoveToImmutable();
        mesh.InstanceColors = colors.MoveToImmutable();
    }

    private ImmutableArray<Matrix4x4> GetFishTransforms(float seconds)
    {
        ImmutableArray<Matrix4x4>.Builder transforms = ImmutableArray.CreateBuilder<Matrix4x4>(_fish.Length);
        foreach (FishMember fish in _fish)
        {
            FishSchool school = s_schools[fish.School];
            float angle = (seconds * school.RadiansPerSecond) + fish.Phase;
            Vector3 position = new(MathF.Cos(angle) * school.Radius, school.Height, MathF.Sin(angle) * school.Radius);
            Vector3 heading = new Vector3(-MathF.Sin(angle), 0, MathF.Cos(angle)) * MathF.Sign(school.RadiansPerSecond);
            float wiggle = 0.25f * MathF.Sin((seconds * 9) + (fish.Phase * 5));
            transforms.Add(
                Matrix4x4.CreateScale(fish.Size) *
                Matrix4x4.CreateRotationY(MathF.Atan2(heading.X, heading.Z) + wiggle) *
                Matrix4x4.CreateTranslation(position + fish.Offset));
        }

        return transforms.MoveToImmutable();
    }

    private ImmutableArray<Matrix4x4> GetMarineSnowTransforms(float seconds)
    {
        ImmutableArray<Matrix4x4>.Builder transforms = ImmutableArray.CreateBuilder<Matrix4x4>(_marineSnow.Length);
        foreach (Particle particle in _marineSnow)
        {
            float fall = (particle.Start.Y - (seconds * particle.FallSpeed)) % MarineSnowTop;
            float height = fall < 0 ? fall + MarineSnowTop : fall;
            Vector3 drift = new(
                0.4f * MathF.Sin((seconds * 0.3f) + particle.Phase),
                0,
                0.4f * MathF.Cos((seconds * 0.25f) + particle.Phase));
            Vector3 position = new Vector3(particle.Start.X, height - 2, particle.Start.Z) + drift;
            float scale = Vector3.Distance(position, Camera.Position) < MarineSnowNearLimit ? 0 : 1;
            transforms.Add(Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(position));
        }

        return transforms.MoveToImmutable();
    }

    private static (ImmutableArray<Matrix4x4> Transforms, ImmutableArray<Color> Colors) CreateBoulders(Random random)
    {
        ImmutableArray<Matrix4x4>.Builder transforms = ImmutableArray.CreateBuilder<Matrix4x4>(BoulderCount);
        ImmutableArray<Color>.Builder colors = ImmutableArray.CreateBuilder<Color>(BoulderCount);
        for (int i = 0; i < BoulderCount; i++)
        {
            float angle = random.NextSingle() * MathF.Tau;
            float distance = 22 + (40 * random.NextSingle());
            float x = MathF.Cos(angle) * distance;
            float z = MathF.Sin(angle) * distance;
            float size = 5 + (8 * random.NextSingle());
            transforms.Add(
                Matrix4x4.CreateScale(size, size * (0.6f + (0.6f * random.NextSingle())), size * (0.7f + (0.5f * random.NextSingle()))) *
                Matrix4x4.CreateRotationY(random.NextSingle() * MathF.Tau) *
                Matrix4x4.CreateTranslation(x, UnderwaterGeometry.GetSeabedHeight(x, z) - 1, z));
            colors.Add(s_boulderColors[random.Next(s_boulderColors.Length)]);
        }

        return (transforms.MoveToImmutable(), colors.MoveToImmutable());
    }

    private static (ImmutableArray<Matrix4x4> Transforms, ImmutableArray<Color> Colors) Scatter(
        Random random,
        int count,
        Color[] palette,
        Func<Random, Vector3> scale)
    {
        ImmutableArray<Matrix4x4>.Builder transforms = ImmutableArray.CreateBuilder<Matrix4x4>(count);
        ImmutableArray<Color>.Builder colors = ImmutableArray.CreateBuilder<Color>(count);
        for (int i = 0; i < count; i++)
        {
            transforms.Add(
                Matrix4x4.CreateScale(scale(random)) *
                Matrix4x4.CreateRotationY(random.NextSingle() * MathF.Tau) *
                Matrix4x4.CreateTranslation(GetScatterPosition(random, ScatterRadius)));
            colors.Add(palette[random.Next(palette.Length)]);
        }

        return (transforms.MoveToImmutable(), colors.MoveToImmutable());
    }

    private static Plant[] CreateGrass(Random random)
    {
        Plant[] grass = new Plant[GrassCount];
        for (int i = 0; i < grass.Length; i++)
        {
            grass[i] = new Plant(
                GetScatterPosition(random, ScatterRadius),
                Yaw: random.NextSingle() * MathF.Tau,
                Width: 0.7f + (0.5f * random.NextSingle()),
                Height: 0.5f + (1.1f * random.NextSingle()),
                s_grassColors[random.Next(s_grassColors.Length)]);
        }

        return grass;
    }

    private static Plant[] CreateKelp(Random random)
    {
        List<Plant> kelp = [];
        for (int grove = 0; grove < KelpGroveCount; grove++)
        {
            Vector3 center = GetScatterPosition(random, ScatterRadius);
            for (int i = 0; i < KelpPerGrove; i++)
            {
                float angle = random.NextSingle() * MathF.Tau;
                float spread = 6 * MathF.Sqrt(random.NextSingle());
                float x = center.X + (MathF.Cos(angle) * spread);
                float z = center.Z + (MathF.Sin(angle) * spread);
                if (MathF.Abs(MathF.Sqrt((x * x) + (z * z)) - OrbitRadius) < OrbitClearance)
                {
                    continue;
                }

                float height = 9 + (13 * random.NextSingle());
                kelp.Add(new Plant(
                    new Vector3(x, UnderwaterGeometry.GetSeabedHeight(x, z) - 0.1f, z),
                    Yaw: random.NextSingle() * MathF.Tau,
                    Width: height,
                    Height: height,
                    s_kelpColors[random.Next(s_kelpColors.Length)]));
            }
        }

        return [.. kelp];
    }

    private static FishMember[] CreateFish(Random random)
    {
        FishMember[] fish = new FishMember[s_schools.Length * FishPerSchool];
        for (int i = 0; i < fish.Length; i++)
        {
            Vector3 offset = new(
                (random.NextSingle() - 0.5f) * 5,
                (random.NextSingle() - 0.5f) * 3,
                (random.NextSingle() - 0.5f) * 5);
            fish[i] = new FishMember(
                School: i / FishPerSchool,
                Phase: random.NextSingle() * 0.6f,
                offset,
                Size: 0.2f + (0.15f * random.NextSingle()));
        }

        return fish;
    }

    private static Particle[] CreateMarineSnow(Random random)
    {
        Particle[] particles = new Particle[MarineSnowCount];
        for (int i = 0; i < particles.Length; i++)
        {
            float angle = random.NextSingle() * MathF.Tau;
            float distance = MarineSnowRadius * MathF.Sqrt(random.NextSingle());
            particles[i] = new Particle(
                new Vector3(MathF.Cos(angle) * distance, random.NextSingle() * MarineSnowTop, MathF.Sin(angle) * distance),
                FallSpeed: 0.08f + (0.15f * random.NextSingle()),
                Phase: random.NextSingle() * MathF.Tau);
        }

        return particles;
    }

    /// <summary>
    /// Returns a random point on the seabed within <paramref name="radius"/> of the center, denser toward the center.
    /// </summary>
    private static Vector3 GetScatterPosition(Random random, float radius)
    {
        float distance = radius * MathF.Sqrt(random.NextSingle()) * (0.25f + (0.75f * random.NextSingle()));
        float angle = random.NextSingle() * MathF.Tau;
        float x = MathF.Cos(angle) * distance;
        float z = MathF.Sin(angle) * distance;
        return new Vector3(x, UnderwaterGeometry.GetSeabedHeight(x, z) - 0.1f, z);
    }

    /// <summary>
    /// Holds a circle that one school of fish swims around the center.
    /// </summary>
    private readonly record struct FishSchool(float Radius, float Height, float RadiansPerSecond, Color Color);

    /// <summary>
    /// Holds one fish: its school, its lead along the circle in radians, its offset from the circle, and its size.
    /// </summary>
    private readonly record struct FishMember(int School, float Phase, Vector3 Offset, float Size);

    /// <summary>
    /// Holds one plant's base position, facing, size, and color.
    /// </summary>
    private readonly record struct Plant(Vector3 Position, float Yaw, float Width, float Height, Color Color);

    /// <summary>
    /// Holds one drifting particle's starting position, falling speed, and drift phase.
    /// </summary>
    private readonly record struct Particle(Vector3 Start, float FallSpeed, float Phase);
}
