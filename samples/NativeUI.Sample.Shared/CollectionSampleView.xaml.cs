using System.Collections.ObjectModel;
using NativeUI.Controls;

namespace NativeUI.Sample;

/// <summary>Demonstrates List, VirtualizedList, Table, and VirtualizedTable with data templates and sorting.</summary>
public partial class CollectionSampleView
{
    private static readonly string[] s_statuses = ["Active", "Pending", "Suspended"];

    /// <summary>Creates the examples and fills them with generated users.</summary>
    public CollectionSampleView()
    {
        InitializeComponent();

        UserList.ItemsSource = new ObservableCollection<SampleUser>(CreateUsers(8));
        UserList.SelectionChanged += (_, _) =>
            ListStatus.Value = UserList.SelectedItem is SampleUser user ? "Selected: " + user.Name : "No user selected";

        LargeList.ItemsSource = Enumerable.Range(1, 10_000).Select(static i => "Item " + i).ToList();
        LargeList.SelectionChanged += (_, _) => LargeListStatus.Value = "Selected: " + LargeList.SelectedIndices.Count;

        UserTable.ItemsSource = CreateUsers(500);
    }

    private static List<SampleUser> CreateUsers(int count)
    {
        List<SampleUser> users = [];
        for (int i = 1; i <= count; i++)
        {
            users.Add(new SampleUser(
                "User " + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                $"user{i}@example.com",
                s_statuses[i % s_statuses.Length],
                20 + (i * 7 % 45)));
        }

        return users;
    }
}
