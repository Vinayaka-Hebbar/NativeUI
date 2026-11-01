using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Base class for the objects a <c>Viewport3D</c> shows. Coordinates are left-handed: X points right, Y points up,
/// and Z points into the screen.
/// </summary>
public abstract class SceneNode : BindableObject
{
    /// <summary>
    /// Occurs when a property of this node changes, so its viewport must redraw.
    /// </summary>
    internal event EventHandler? Changed;

    /// <summary>
    /// Receives the data context of the node's owner.
    /// </summary>
    internal void InheritDataContext(object? dataContext)
    {
        SetInheritedDataContext(dataContext);
    }

    /// <summary>
    /// Replaces a node this node owns, so its changes count as changes to this node and it inherits this node's data context.
    /// </summary>
    private protected void ReplaceChild(SceneNode? oldChild, SceneNode? newChild)
    {
        if (oldChild is not null)
        {
            oldChild.Changed -= OnChildChanged;
        }

        if (newChild is not null)
        {
            newChild.Changed += OnChildChanged;
            newChild.InheritDataContext(DataContext);
        }
    }

    private void OnChildChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    /// <summary>
    /// Raises <see cref="Changed"/>.
    /// </summary>
    private protected void NotifyChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindableProperty property, object? oldValue, object? newValue)
    {
        base.OnPropertyChanged(property, oldValue, newValue);
        NotifyChanged();
    }
}

/// <summary>
/// Holds the lights and meshes of a <c>Viewport3D</c>.
/// </summary>
public class Scene3D : SceneNode
{
    private readonly ObservableCollection<SceneNode> _items = [];
    private readonly List<SceneNode> _subscribedItems = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene3D"/> class.
    /// </summary>
    public Scene3D()
    {
        _items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>
    /// Gets the lights and meshes in this scene.
    /// </summary>
    public IList<SceneNode> Items => _items;

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        foreach (SceneNode item in _items)
        {
            item.InheritDataContext(args.NewValue);
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (SceneNode item in _subscribedItems)
        {
            item.Changed -= OnItemChanged;
        }

        _subscribedItems.Clear();
        foreach (SceneNode item in _items)
        {
            item.Changed += OnItemChanged;
            item.InheritDataContext(DataContext);
            _subscribedItems.Add(item);
        }

        NotifyChanged();
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }
}

/// <summary>
/// Views a scene in perspective, from <see cref="Position"/> toward <see cref="LookAt"/>.
/// </summary>
public class PerspectiveCamera : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Position"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> PositionProperty =
        BindableProperty.Create<PerspectiveCamera, Vector3>(nameof(Position), defaultValue: new Vector3(0, 0, -5));

    /// <summary>
    /// Identifies the <see cref="LookAt"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> LookAtProperty =
        BindableProperty.Create<PerspectiveCamera, Vector3>(nameof(LookAt), defaultValue: Vector3.Zero);

    /// <summary>
    /// Identifies the <see cref="Fov"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> FovProperty =
        BindableProperty.Create<PerspectiveCamera, double>(nameof(Fov), defaultValue: 60);

    /// <summary>
    /// Gets or sets the camera's location. Defaults to five units in front of the origin.
    /// </summary>
    public Vector3 Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the point the camera faces. Defaults to the origin.
    /// </summary>
    public Vector3 LookAt
    {
        get => GetValue(LookAtProperty);
        set => SetValue(LookAtProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical field of view in degrees. Defaults to 60.
    /// </summary>
    public double Fov
    {
        get => GetValue(FovProperty);
        set => SetValue(FovProperty, value);
    }
}

/// <summary>
/// Lights every mesh from one direction, as distant light such as the sun does.
/// </summary>
public class DirectionalLight : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Create<DirectionalLight, Color>(nameof(Color), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="Direction"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> DirectionProperty =
        BindableProperty.Create<DirectionalLight, Vector3>(nameof(Direction), defaultValue: Vector3.UnitY);

    /// <summary>
    /// Gets or sets the light's color. Defaults to white.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the direction pointing from the scene toward the light. Its length does not matter. Defaults to straight up.
    /// </summary>
    public Vector3 Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }
}
