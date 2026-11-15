using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes read-only text, selection settings, and accessibility attributes with a span element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomText : HtmlDomElement
{
    private readonly Text _text;
    private readonly Dictionary<Inline, JSObject> _inlineNodes = [];

    internal HtmlDomText(Text text, JSObject? element) : base(text, element, "span")
    {
        _text = text;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("role", "text");
        SetAttribute("aria-label", _text.AriaLabel);
        UpdateSelection();
        _text.InlinesChanged += OnInlinesChanged;
        UpdateContent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Text.ValueProperty))
        {
            UpdateContent();
        }
        else if (ReferenceEquals(args.Property, Text.AriaLabelProperty))
        {
            SetAttribute("aria-label", _text.AriaLabel);
        }
        else if (ReferenceEquals(args.Property, Text.IsTextSelectionEnabledProperty))
        {
            UpdateSelection();
        }
    }

    /// <inheritdoc />
    protected override void UpdateTabIndex()
    {
        if (_text.HasHyperlinks)
        {
            SetAttribute("tabindex", "-1");
            return;
        }

        base.UpdateTabIndex();
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _text.InlinesChanged -= OnInlinesChanged;
        foreach (KeyValuePair<Inline, JSObject> entry in _inlineNodes)
        {
            if (entry.Key is Hyperlink)
            {
                DomInterop.ClearHyperlinkHandler(entry.Value);
            }

            entry.Value.Dispose();
        }

        _inlineNodes.Clear();
        base.OnDisposing();
    }

    private void OnInlinesChanged(object? sender, EventArgs args)
    {
        UpdateContent();
    }

    private void UpdateContent()
    {
        UpdateTabIndex();
        if (!_text.HasInlines)
        {
            ReleaseInlineNodes([]);
            SetTextContent(_text.Value);
            return;
        }

        if (Element is not JSObject element)
        {
            return;
        }

        SetAttribute("role", null);
        if (_inlineNodes.Count == 0)
        {
            SetTextContent(string.Empty);
        }

        HashSet<Inline> live = [];
        SynchronizeInlines(_text.Inlines, element, live);
        ReleaseInlineNodes(live);
    }

    private void SynchronizeInlines(InlineCollection inlines, JSObject parent, HashSet<Inline> live)
    {
        for (int index = 0; index < inlines.Count; index++)
        {
            Inline inline = inlines[index];
            live.Add(inline);
            if (!_inlineNodes.TryGetValue(inline, out JSObject? node))
            {
                node = DomInterop.CreateElement(inline is Hyperlink ? "a" : inline is LineBreak ? "br" : "span");
                _inlineNodes.Add(inline, node);
                if (inline is Hyperlink link)
                {
                    DomInterop.SetHyperlinkHandler(node, link.PerformClick);
                }
            }

            if (!DomInterop.IsChildAt(parent, node, index))
            {
                using JSObject? reference = DomInterop.GetChildAt(parent, index);
                DomInterop.InsertBefore(parent, node, reference);
            }

            string classes = inline.Class.ToString();
            if (DomInterop.GetClassName(node) != classes)
            {
                DomInterop.SetClassName(node, classes);
            }

            if (inline is not LineBreak)
            {
                DomInterop.SetStyleProperty(node, "display", "inline");
            }

            foreach ((string property, string? value) in StyleCssDeclarations.Get(inline.Style))
            {
                if (DomInterop.GetStyleProperty(node, property) == (value ?? string.Empty))
                {
                    continue;
                }

                if (value is null)
                {
                    DomInterop.RemoveStyleProperty(node, property);
                }
                else
                {
                    DomInterop.SetStyleProperty(node, property, value);
                }
            }

            switch (inline)
            {
                case Run run when DomInterop.GetTextContent(node) != run.Value:
                    DomInterop.SetTextContent(node, run.Value);
                    break;
                case Span span:
                    if (span is Hyperlink hyperlink)
                    {
                        if (hyperlink.NavigateUri is Uri uri)
                        {
                            DomInterop.SetAttribute(node, "href", uri.OriginalString);
                        }
                        else
                        {
                            DomInterop.RemoveAttribute(node, "href");
                        }
                    }

                    SynchronizeInlines(span.Inlines, node, live);
                    break;
            }
        }
    }

    private void ReleaseInlineNodes(HashSet<Inline> live)
    {
        foreach (Inline inline in _inlineNodes.Keys.ToArray())
        {
            if (live.Contains(inline))
            {
                continue;
            }

            JSObject node = _inlineNodes[inline];
            if (inline is Hyperlink)
            {
                DomInterop.ClearHyperlinkHandler(node);
            }

            DomInterop.RemoveElement(node);
            node.Dispose();
            _inlineNodes.Remove(inline);
        }
    }

    private void UpdateSelection()
    {
        SetStyleProperty("user-select", _text.IsTextSelectionEnabled ? null : "none");
    }
}
