using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeUI.Sample;

/// <summary>
/// Represents an editable document in the tab sample.
/// </summary>
public partial class TabDocument : ObservableObject
{
    [ObservableProperty]
    private string _text;

    /// <summary>
    /// Initializes a document and its collection-removal command.
    /// </summary>
    public TabDocument(string title, string text, ObservableCollection<TabDocument> documents)
    {
        Title = title;
        _text = text;
        CloseCommand = new RelayCommand(() => documents.Remove(this));
    }

    /// <summary>Gets the document's header text.</summary>
    public string Title { get; }

    /// <summary>Gets the command that closes this document.</summary>
    public ICommand CloseCommand { get; }
}
