using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Sample;

/// <summary>
/// Gallery page compiled from <c>UnderwaterSampleView.xaml</c>. Its View button opens <see cref="UnderwaterSceneView"/>
/// in a window of its own, and is disabled while that window is open.
/// </summary>
public partial class UnderwaterSampleView
{
    private static readonly Size s_windowSize = new(1024, 640);

    /// <summary>
    /// Initializes a new instance of the <see cref="UnderwaterSampleView"/> class.
    /// </summary>
    public UnderwaterSampleView()
    {
        InitializeComponent();
        ViewButton.Click += (_, _) => OpenSceneWindow();
    }

    private void OpenSceneWindow()
    {
        IWindow window;
        try
        {
            window = Application.Current.OpenWindow(new UnderwaterSceneView(), "Underwater", s_windowSize);
        }
        catch (NotSupportedException exception)
        {
            WindowStatus.Value = exception.Message;
            return;
        }

        ViewButton.IsEnabled = false;
        WindowStatus.Value = "The scene is open in its own window.";
        window.Closed += (_, _) =>
        {
            ViewButton.IsEnabled = true;
            WindowStatus.Value = "Opens the scene in its own window.";
        };
    }
}
