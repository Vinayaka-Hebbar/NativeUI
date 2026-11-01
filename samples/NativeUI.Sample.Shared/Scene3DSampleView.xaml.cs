using System.Numerics;

namespace NativeUI.Sample;

/// <summary>
/// Sample page compiled from <c>Scene3DSampleView.xaml</c>, showing a cube that rotates once per screen refresh.
/// </summary>
public partial class Scene3DSampleView
{
    private const float DegreesPerSecond = 45;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene3DSampleView"/> class.
    /// </summary>
    public Scene3DSampleView()
    {
        InitializeComponent();

        CubeView.Rendering += (_, args) =>
        {
            float angle = (float)args.TotalTime.TotalSeconds * DegreesPerSecond;
            Cube.Rotation = new Vector3(angle / 2, angle, 0);
        };
    }
}
