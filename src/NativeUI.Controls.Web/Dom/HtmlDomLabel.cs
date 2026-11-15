using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Maps shared label associations to a native HTML label.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomLabel : HtmlDomView
{
    private readonly Label _label;
    private UIElement? _target;
    internal HtmlDomLabel(Label label, JSObject? element) : base(label, element, "label") => _label = label;

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        _label.VisualInvalidated += OnLabelChanged;
        UpdateTarget();
    }
    private void OnLabelChanged(object? sender, EventArgs args) => UpdateTarget();
    private void UpdateTarget()
    {
        UIElement? target = _label.AssociatedControl;
        if (!ReferenceEquals(target, _target))
        {
            if (_target is not null)
            {
                _target.PropertyChanged -= OnTargetChanged;
            }
            _target = target;
            if (_target is not null)
            {
                _target.PropertyChanged += OnTargetChanged;
            }
        }

        SetAttribute("for", _label.Target is not null ? target?.Id : null);
    }
    private void OnTargetChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, UIElement.IdProperty))
        {
            UpdateTarget();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Label.TargetProperty))
        {
            UpdateTarget();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _label.VisualInvalidated -= OnLabelChanged;
        if (_target is not null)
            {
                _target.PropertyChanged -= OnTargetChanged;
            }
        _target = null;
        base.OnDisposing();
    }
}
