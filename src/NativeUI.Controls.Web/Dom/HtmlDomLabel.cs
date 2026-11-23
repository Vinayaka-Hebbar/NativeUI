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
    private JSObject? _events;
    internal HtmlDomLabel(Label label, JSObject? element) : base(label, element, "label") => _label = label;

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        if (Element is not null)
        {
            _events = DomInterop.ObserveLabel(Element, OnBrowserActivated);
        }
        _label.VisualInvalidated += OnLabelChanged;
        UpdateTarget();
    }
    /// <summary>Activates the associated shared control for a native label click.</summary>
    internal void OnBrowserActivated()
    {
        if (!IsDisposed)
        {
            _label.Activate();
        }
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
        using JSObject? events = _events;
        _events = null;
        if (events is not null)
        {
            DomInterop.UnobserveLabel(events);
        }

        _label.VisualInvalidated -= OnLabelChanged;
        if (_target is not null)
        {
            _target.PropertyChanged -= OnTargetChanged;
        }
        _target = null;
        base.OnDisposing();
    }
}
