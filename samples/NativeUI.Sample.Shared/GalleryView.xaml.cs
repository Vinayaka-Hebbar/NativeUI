namespace NativeUI.Sample;

/// <summary>
/// Gallery shell view hosting navigation and active page display.
/// </summary>
public partial class GalleryView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryView"/> class.
    /// </summary>
    public GalleryView()
    {
        InitializeComponent();
        DataContext = new GalleryViewModel();
    }
}
