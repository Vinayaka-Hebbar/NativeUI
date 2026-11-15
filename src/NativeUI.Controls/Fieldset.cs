using System.Collections.Specialized;
using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Groups controls with HTML fieldset disabled and first-legend semantics.
/// </summary>
public class Fieldset : Control
{
    /// <summary>
    /// Identifies the fieldset's form-associated name.
    /// </summary>
    public static readonly BindableProperty<string?> NameProperty =
        BindableProperty.Create<Fieldset, string?>(
            nameof(Name));

    /// <summary>
    /// Creates a fieldset and observes changes to its first legend.
    /// </summary>
    public Fieldset()
    {
        ((INotifyCollectionChanged)Children).CollectionChanged += OnChildrenChanged;
    }

    /// <summary>
    /// Gets or sets the fieldset's form name, independently of its element identifier.
    /// </summary>
    public string? Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc />
    public override string TagName => "fieldset";

    /// <inheritdoc />
    protected internal override bool IsEnabledForChild(UIElement child)
    {
        foreach (UIElement element in Children)
        {
            if (element is Legend)
            {
                if (ReferenceEquals(child, element))
                {
                    return IsEnabledCore && (Parent?.IsEnabledForChild(this) ?? true);
                }

                break;
            }
        }

        return base.IsEnabledForChild(child);
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (UIElement child in Children)
        {
            child.UpdateEffectiveEnabledState();
        }
    }
}
