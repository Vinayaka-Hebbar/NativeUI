using System.Numerics;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Base class for the shapes of a <see cref="Mesh"/>.
/// </summary>
public abstract class Geometry3D : SceneNode
{
}

/// <summary>
/// Describes a box centered on its mesh's position.
/// </summary>
public class BoxGeometry : Geometry3D
{
    /// <summary>
    /// Identifies the <see cref="Width"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> WidthProperty =
        BindableProperty.Create<BoxGeometry, double>(nameof(Width), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="Height"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> HeightProperty =
        BindableProperty.Create<BoxGeometry, double>(nameof(Height), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="Depth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> DepthProperty =
        BindableProperty.Create<BoxGeometry, double>(nameof(Depth), defaultValue: 1);

    /// <summary>
    /// Gets or sets the size along the X axis. The default is 1.
    /// </summary>
    public double Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the size along the Y axis. The default is 1.
    /// </summary>
    public double Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the size along the Z axis. The default is 1.
    /// </summary>
    public double Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }
}

/// <summary>
/// Base class for the surface appearances of a <see cref="Mesh"/>.
/// </summary>
public abstract class Material3D : SceneNode
{
}

/// <summary>
/// Describes a surface with one color, shaded by the scene's lights.
/// </summary>
public class StandardMaterial : Material3D
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Create<StandardMaterial, Color>(nameof(Color), defaultValue: Colors.White);

    /// <summary>
    /// Gets or sets the surface color. The default is white.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }
}

/// <summary>
/// Places a <see cref="Geometry3D"/> with a <see cref="Material3D"/> in a scene.
/// </summary>
public class Mesh : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Geometry"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Geometry3D?> GeometryProperty =
        BindableProperty.Create<Mesh, Geometry3D?>(
            nameof(Geometry),
            defaultValue: null,
            propertyChanged: static (owner, oldGeometry, newGeometry) => owner.ReplaceChild(oldGeometry, newGeometry));

    /// <summary>
    /// Identifies the <see cref="Material"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Material3D?> MaterialProperty =
        BindableProperty.Create<Mesh, Material3D?>(
            nameof(Material),
            defaultValue: null,
            propertyChanged: static (owner, oldMaterial, newMaterial) => owner.ReplaceChild(oldMaterial, newMaterial));

    /// <summary>
    /// Identifies the <see cref="Position"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> PositionProperty =
        BindableProperty.Create<Mesh, Vector3>(nameof(Position), defaultValue: Vector3.Zero);

    /// <summary>
    /// Identifies the <see cref="Rotation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> RotationProperty =
        BindableProperty.Create<Mesh, Vector3>(nameof(Rotation), defaultValue: Vector3.Zero);

    /// <summary>
    /// Gets or sets the shape of this mesh.
    /// </summary>
    public Geometry3D? Geometry
    {
        get => GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    /// <summary>
    /// Gets or sets the surface appearance of this mesh.
    /// </summary>
    public Material3D? Material
    {
        get => GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    /// <summary>
    /// Gets or sets the location of the mesh's center.
    /// </summary>
    public Vector3 Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the rotation about the X, Y, and Z axes in degrees.
    /// </summary>
    public Vector3 Rotation
    {
        get => GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        Geometry?.InheritDataContext(args.NewValue);
        Material?.InheritDataContext(args.NewValue);
    }
}
