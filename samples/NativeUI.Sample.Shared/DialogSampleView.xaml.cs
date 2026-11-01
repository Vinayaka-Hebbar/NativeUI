using NativeUI.Controls;

namespace NativeUI.Sample;

/// <summary>
/// Sample page compiled from <c>DialogSampleView.xaml</c>, showing anchored and centered dialogs and a portal menu.
/// <c>HelloView.xaml.css</c> styles it, because a nested view resolves styles through the root of its tree.
/// </summary>
public partial class DialogSampleView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialogSampleView"/> class.
    /// </summary>
    public DialogSampleView()
    {
        InitializeComponent();

        DeleteButton.Click += (_, _) => ConfirmDialog.Show(DeleteButton);
        AboutButton.Click += (_, _) => AboutDialog.Show();
        MenuButton.Click += (_, _) => ActionMenu.Show(MenuButton);
        CancelButton.Click += (_, _) => ConfirmDialog.Close();
        ConfirmButton.Click += (_, _) => ConfirmDialog.Close();
        MenuRenameButton.Click += (_, _) => ActionMenu.Close();
        MenuMoveButton.Click += (_, _) => ActionMenu.Close();
        ConfirmDialog.Closed += (_, _) => DialogStatus.Value = "The anchored dialog closed";
        AboutDialog.Closed += (_, _) => DialogStatus.Value = "The centered dialog closed";
        ActionMenu.Closed += (_, _) => DialogStatus.Value = "The menu closed";
        CodeDialogButton.Click += async (_, _) => await ShowCodeDialogAsync();
    }

    /// <summary>
    /// Shows a dialog that has no parent, so it opens in <see cref="Dialog.Owner"/>, which defaults to the application's main view.
    /// </summary>
    private async Task ShowCodeDialogAsync()
    {
        Button noButton = new() { Class = ["btn-action"], Text = "No" };
        Button yesButton = new() { Class = ["btn-action"], Text = "Yes" };
        Dialog dialog = new()
        {
            Class = ["sample-dialog"],
            Title = "Created in code",
            Description = "This dialog waits for a result with ShowAsync.",
            Footer = [noButton, yesButton]
        };
        noButton.Click += (_, _) => dialog.Close(false);
        yesButton.Click += (_, _) => dialog.Close(true);

        bool? answer = await dialog.ShowAsync<bool?>();
        DialogStatus.Value = answer is null ? "The dialog closed without an answer" : $"The answer was {answer}";
        dialog.Dispose();
    }
}
