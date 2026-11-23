using NativeUI.Core;

namespace NativeUI.Sample;

/// <summary>
/// Demonstrates size observation and subscription lifetime.
/// </summary>
public partial class ResizeSampleView
{
    /// <summary>Initializes the sample and its interactive behavior.</summary>
    public ResizeSampleView()
    {
        InitializeComponent();

        int resizeEvents = 0;
        bool isObserving = true;
        bool isTall = false;

        void OnResizeChanged(object? sender, SizeChangedEventArgs e)
        {
            resizeEvents++;
            ResizeStatusText.Value = $"Size: {e.NewSize.Width:0}px × {e.NewSize.Height:0}px (Events: {resizeEvents})";
            ObserverStateText.Value = $"Observer Status: Active | IsSizeObserved = {ResizeTargetBox.IsSizeObserved}";
        }

        ResizeTargetBox.SizeChanged += OnResizeChanged;

        ResizeCompactButton.Click += (_, _) => ResizeTargetBox.Width = 180;
        ResizeMediumButton.Click += (_, _) => ResizeTargetBox.Width = 320;
        ResizeWideButton.Click += (_, _) => ResizeTargetBox.Width = 460;
        ResizeToggleHeightButton.Click += (_, _) =>
        {
            isTall = !isTall;
            ResizeTargetBox.Height = isTall ? 140 : 80;
        };

        ToggleObserveButton.Click += (_, _) =>
        {
            if (isObserving)
            {
                ResizeTargetBox.SizeChanged -= OnResizeChanged;
                isObserving = false;
                ToggleObserveButton.Text = "Attach Observer";
                ObserverStateText.Value = $"Observer Status: Inactive | IsSizeObserved = {ResizeTargetBox.IsSizeObserved}";
            }
            else
            {
                ResizeTargetBox.SizeChanged += OnResizeChanged;
                isObserving = true;
                ToggleObserveButton.Text = "Detach Observer";
                ObserverStateText.Value = $"Observer Status: Active | IsSizeObserved = {ResizeTargetBox.IsSizeObserved}";
            }
        };
    }
}
