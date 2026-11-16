using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NativeUI.Controls;

namespace NativeUI.Sample;

/// <summary>
/// Supplies observable menu settings and commands for the popup examples.
/// </summary>
public partial class PopupSampleViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSheetOpen;

    [ObservableProperty]
    private Dock _sheetPlacement = Dock.Right;

    [RelayCommand]
    private void OpenSheet(string? edge)
    {
        if (Enum.TryParse(edge, out Dock placement))
        {
            SheetPlacement = placement;
            IsSheetOpen = true;
        }
    }

    [RelayCommand]
    private void CloseSheet()
    {
        IsSheetOpen = false;
    }

    [ObservableProperty]
    private bool _showToolbar = true;

    [ObservableProperty]
    private bool _showSidebar = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _menuStatus = "Choose a file action. Save is enabled after New or Open.";

    [RelayCommand]
    private void New()
    {
        HasUnsavedChanges = true;
        MenuStatus = "New document created. Save is now enabled.";
    }

    [RelayCommand]
    private void Open()
    {
        HasUnsavedChanges = true;
        MenuStatus = "Open command invoked. Save is now enabled.";
    }

    [RelayCommand(CanExecute = nameof(HasUnsavedChanges))]
    private void Save()
    {
        HasUnsavedChanges = false;
        MenuStatus = "Changes saved. Save is disabled until the next edit.";
    }

    [RelayCommand]
    private void Export(string? format)
    {
        MenuStatus = $"Export {format} command invoked.";
    }
}
