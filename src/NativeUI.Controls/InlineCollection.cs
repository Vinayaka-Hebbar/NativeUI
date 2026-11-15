using System.Collections.ObjectModel;
using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>Owns ordered inline content and invalidates its containing text when content changes.</summary>
public sealed class InlineCollection : Collection<Inline>
{
    /// <summary>Initializes an independent inline collection.</summary>
    public InlineCollection()
    {
    }

    internal InlineCollection(BindableObject owner)
    {
        Owner = owner;
    }

    internal BindableObject? Owner { get; }

    /// <inheritdoc />
    protected override void InsertItem(int index, Inline item)
    {
        ValidateItem(item);
        base.InsertItem(index, item);
        item.ParentCollection = this;
        item.SetInheritedDataContext(Owner?.DataContext);
        NotifyChanged();
    }

    /// <inheritdoc />
    protected override void SetItem(int index, Inline item)
    {
        if (ReferenceEquals(this[index], item))
        {
            return;
        }

        ValidateItem(item);
        Inline previous = this[index];
        base.SetItem(index, item);
        previous.ParentCollection = null;
        previous.SetInheritedDataContext(null);
        item.ParentCollection = this;
        item.SetInheritedDataContext(Owner?.DataContext);
        NotifyChanged();
    }

    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        Inline item = this[index];
        base.RemoveItem(index);
        item.ParentCollection = null;
        item.SetInheritedDataContext(null);
        NotifyChanged();
    }

    /// <inheritdoc />
    protected override void ClearItems()
    {
        Inline[] previous = this.ToArray();
        base.ClearItems();
        foreach (Inline item in previous)
        {
            item.ParentCollection = null;
            item.SetInheritedDataContext(null);
        }

        NotifyChanged();
    }

    internal void NotifyChanged()
    {
        switch (Owner)
        {
            case Text text:
                text.OnInlinesChanged();
                break;
            case Inline inline:
                inline.ParentCollection?.NotifyChanged();
                break;
        }
    }

    private void ValidateItem(Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ObjectDisposedException.ThrowIf(item.IsDisposed, item);
        if (item.ParentCollection is not null)
        {
            throw new InvalidOperationException("An inline already belongs to a collection.");
        }

        for (BindableObject? current = Owner; current is Inline inline; current = inline.ParentCollection?.Owner)
        {
            if (ReferenceEquals(current, item))
            {
                throw new InvalidOperationException("An inline cannot contain itself or an ancestor.");
            }
        }
    }
}
