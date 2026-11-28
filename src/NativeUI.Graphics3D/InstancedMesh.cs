using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Draws many copies of one <see cref="Mesh.Geometry"/> with one <see cref="Mesh.Material"/>. Each copy is transformed by
/// its matrix in <see cref="InstanceTransforms"/> and then by the mesh's <see cref="Mesh.Rotation"/> and
/// <see cref="Mesh.Position"/>.
/// </summary>
/// <remarks>
/// A GPU renderer draws all copies in one call. Changing either array replaces every copy's data.
/// </remarks>
public class InstancedMesh : Mesh
{
    /// <summary>
    /// Identifies the <see cref="InstanceTransforms"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<Matrix4x4>> InstanceTransformsProperty =
        BindableProperty.Create<InstancedMesh, ImmutableArray<Matrix4x4>>(nameof(InstanceTransforms), defaultValue: []);

    /// <summary>
    /// Identifies the <see cref="InstanceColors"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<Color>> InstanceColorsProperty =
        BindableProperty.Create<InstancedMesh, ImmutableArray<Color>>(nameof(InstanceColors), defaultValue: []);

    private MeshInstance[]? _instances;
    private int _instancesVersion;

    /// <summary>
    /// Gets or sets one transform per copy, applied before the mesh's own rotation and position. Its length is the number
    /// of copies.
    /// </summary>
    public ImmutableArray<Matrix4x4> InstanceTransforms
    {
        get => GetValue(InstanceTransformsProperty);
        set => SetValue(InstanceTransformsProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of each copy, by index. A copy without a color uses the material color.
    /// </summary>
    public ImmutableArray<Color> InstanceColors
    {
        get => GetValue(InstanceColorsProperty);
        set => SetValue(InstanceColorsProperty, value);
    }

    /// <summary>
    /// Returns one packed instance per copy, rebuilt only after this mesh or a node it owns changes.
    /// </summary>
    internal ReadOnlySpan<MeshInstance> GetInstances()
    {
        if (_instances is null || _instancesVersion != Version)
        {
            _instances = BuildInstances();
            _instancesVersion = Version;
        }

        return _instances;
    }

    private MeshInstance[] BuildInstances()
    {
        ImmutableArray<Matrix4x4> transforms = InstanceTransforms.IsDefault ? [] : InstanceTransforms;
        ImmutableArray<Color> colors = InstanceColors.IsDefault ? [] : InstanceColors;
        Matrix4x4 geometryTransform = SceneRenderer.GetGeometryTransform(Geometry);
        Matrix4x4 meshTransform = SceneRenderer.GetMeshTransform(this);
        Color surface = SceneRenderer.GetSurfaceColor(this);

        MeshInstance[] instances = new MeshInstance[transforms.Length];
        for (int i = 0; i < instances.Length; i++)
        {
            Matrix4x4 world = geometryTransform * transforms[i] * meshTransform;
            instances[i] = SceneBuffers.CreateInstance(world, i < colors.Length ? colors[i] : surface, Material);
        }

        return instances;
    }
}
