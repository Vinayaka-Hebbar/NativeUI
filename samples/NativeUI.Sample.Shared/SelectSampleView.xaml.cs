using NativeUI.Controls;

namespace NativeUI.Sample;

/// <summary>Demonstrates Select with a placeholder, a preselected item, a long scrolling list, and a disabled state.</summary>
public partial class SelectSampleView
{
    private static readonly string[] s_fruits = ["Apple", "Banana", "Blueberry", "Grapes", "Pineapple"];

    /// <summary>Fills the selects and connects the status text to the first one.</summary>
    public SelectSampleView()
    {
        InitializeComponent();

        FruitSelect.ItemsSource = s_fruits;
        PreselectedSelect.ItemsSource = s_fruits;
        DisabledSelect.ItemsSource = s_fruits;
        ScrolledSelect.ItemsSource = s_fruits;
        EdgeSelect.ItemsSource = s_fruits;
        PreselectedSelect.SelectedIndex = 1;
        DisabledSelect.SelectedIndex = 0;
        LongSelect.ItemsSource = Enumerable.Range(1, 60).Select(number => $"Item {number}").ToArray();

        FruitSelect.SelectionChanged += (_, _) =>
        {
            FruitStatus.Value = FruitSelect.SelectedItem is string fruit ? $"Selected: {fruit}" : "Nothing selected";
        };
    }
}
