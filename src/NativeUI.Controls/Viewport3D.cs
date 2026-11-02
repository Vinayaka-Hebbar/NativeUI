using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Graphics3D;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Provides data for <see cref="Viewport3D.Rendering"/>.
/// </summary>
/// <param name="TotalTime">The time since the first handler subscribed.</param>
/// <param name="DeltaTime">The time since the previous frame.</param>
public sealed record RenderingEventArgs(TimeSpan TotalTime, TimeSpan DeltaTime);

/// <summary>
/// Shows a <see cref="Scene3D"/> through a <see cref="PerspectiveCamera"/>, drawn as flat-shaded polygons on the same
/// surface as <see cref="CanvasView"/>. Size it like a canvas; without a height it does not draw.
/// </summary>
public class Viewport3D : CanvasView, IFrameTarget
{
    /// <summary>
    /// Identifies the <see cref="Camera"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<PerspectiveCamera?> CameraProperty =
        BindableProperty.Create<Viewport3D, PerspectiveCamera?>(
            nameof(Camera),
            defaultValue: null,
            propertyChanged: static (owner, oldCamera, newCamera) => owner.OnNodeReplaced(oldCamera, newCamera));

    /// <summary>
    /// Identifies the <see cref="Scene"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Scene3D?> SceneProperty =
        BindableProperty.Create<Viewport3D, Scene3D?>(
            nameof(Scene),
            defaultValue: null,
            propertyChanged: static (owner, oldScene, newScene) => owner.OnNodeReplaced(oldScene, newScene));

    private readonly PerspectiveCamera _defaultCamera = new();
    private EventHandler<RenderingEventArgs>? _rendering;
    private double _firstFrameTime;
    private double _lastFrameTime;

    UIElement IFrameTarget.Target => this;

    /// <summary>
    /// Occurs once per screen refresh while this viewport is shown, before it redraws. A subscription keeps the screen
    /// refresh running, so unsubscribe when the scene stops moving.
    /// </summary>
    public event EventHandler<RenderingEventArgs> Rendering
    {
        add
        {
            if (_rendering is null)
            {
                _firstFrameTime = AnimationClock.Now;
                _lastFrameTime = _firstFrameTime;
            }

            _rendering += value;
            AnimationClock.Add(this);
        }
        remove
        {
            _rendering -= value;
            if (_rendering is null)
            {
                AnimationClock.Remove(this);
            }
        }
    }

    /// <inheritdoc />
    public override string TagName => "viewport3d";

    /// <summary>
    /// Gets or sets the camera. Without one, the viewport looks at the origin from five units in front of it.
    /// </summary>
    public PerspectiveCamera? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    /// <summary>
    /// Gets or sets the scene to show.
    /// </summary>
    public Scene3D? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc />
    void IFrameTarget.Tick(double now)
    {
        TimeSpan totalTime = TimeSpan.FromMilliseconds(now - _firstFrameTime);
        TimeSpan deltaTime = TimeSpan.FromMilliseconds(now - _lastFrameTime);
        _lastFrameTime = now;
        _rendering?.Invoke(this, new RenderingEventArgs(totalTime, deltaTime));
    }

    /// <inheritdoc />
    protected override void OnDraw(DrawingContext context, Rect bounds)
    {
        base.OnDraw(context, bounds);
        if (Scene is Scene3D scene)
        {
            SceneRenderer.Render(context, bounds, scene, Camera ?? _defaultCamera);
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        Camera?.InheritDataContext(args.NewValue);
        Scene?.InheritDataContext(args.NewValue);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rendering = null;
            AnimationClock.Remove(this);
        }

        base.Dispose(disposing);
    }

    private void OnNodeReplaced(SceneNode? oldNode, SceneNode? newNode)
    {
        if (oldNode is not null)
        {
            oldNode.Changed -= OnNodeChanged;
        }

        if (newNode is not null)
        {
            newNode.Changed += OnNodeChanged;
            newNode.InheritDataContext(DataContext);
        }

        InvalidateVisual();
    }

    private void OnNodeChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}
