using NativeUI.Core;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>Groups nested inlines under inherited text formatting.</summary>
[ContentProperty(nameof(Inlines))]
public class Span : Inline
{
    private InlineCollection? _inlines;

    /// <summary>Gets the nested inline content in source order.</summary>
    public InlineCollection Inlines => _inlines ??= new InlineCollection(this);

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        foreach (Inline inline in Inlines)
        {
            inline.SetInheritedDataContext(DataContext);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _inlines is not null)
        {
            foreach (Inline inline in _inlines.ToArray())
            {
                inline.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
