using NativeUI.Controls;

namespace NativeUI.Sample;

/// <summary>
/// Sample page compiled from <c>AnimationSampleView.xaml</c>, showing progress bars, a CSS transition, and an animation
/// started from C#. <c>HelloView.xaml.css</c> styles it, because a nested view resolves styles through the root of its tree.
/// </summary>
public partial class AnimationSampleView
{
    private const double ProgressStep = 20;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationSampleView"/> class.
    /// </summary>
    public AnimationSampleView()
    {
        InitializeComponent();

        AddProgressButton.Click += (_, _) => UploadProgress.Value = Math.Min(UploadProgress.Maximum, UploadProgress.Value + ProgressStep);
        ResetProgressButton.Click += (_, _) => UploadProgress.Value = 0;
        PulseButton.Click += async (_, _) => await PlayPulseAsync();
    }

    private async Task PlayPulseAsync()
    {
        PulseStatus.Value = "Playing";
        Animation pulse = PulseBox.Animate("pulse", TimeSpan.FromMilliseconds(600));
        await pulse.Finished;
        PulseStatus.Value = "Finished";
    }
}
