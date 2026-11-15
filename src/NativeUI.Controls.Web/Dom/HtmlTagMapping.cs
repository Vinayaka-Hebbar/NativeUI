namespace NativeUI.Controls.Web;

/// <summary>
/// Defines the HTML element created for each control, so DOM creation and generated CSS selectors cannot disagree.
/// A control without an equivalent HTML element is an autonomous custom element named <c>nui-</c> followed by its tag name.
/// </summary>
internal static class HtmlTagMapping
{
    private const string TextTag = "span";
    private const string ContainerTag = "div";
    private const string ButtonTag = "button";
    private const string CanvasTag = "canvas";
    private const string InputTag = "input";
    private const string DialogTag = "dialog";
    private const string SwitchTag = "nui-switch";
    private const string CheckBoxTag = "nui-checkbox";
    private const string ToggleTag = "nui-toggle";
    private const string ToggleGroupTag = "nui-togglegroup";
    private const string ListTag = "nui-list";
    private const string VirtualizedListTag = "nui-virtualizedlist";
    private const string ListItemTag = "nui-listitem";
    private const string TableTag = "nui-table";
    private const string VirtualizedTableTag = "nui-virtualizedtable";
    private const string TableRowTag = "nui-tablerow";
    private const string TableCellTag = "nui-tablecell";
    private const string SeparatorTag = "nui-separator";
    private const string BadgeTag = "nui-badge";
    private const string AvatarTag = "nui-avatar";
    private const string BreadcrumbTag = "nui-breadcrumb";
    private const string BreadcrumbItemTag = "nui-breadcrumbitem";
    private const string ButtonGroupTag = "nui-buttongroup";
    private const string PopoverTag = "nui-popover";
    private const string TooltipTag = "nui-tooltip";
    private const string DropdownMenuTag = "nui-dropdownmenu";
    private const string ContextMenuTag = "nui-contextmenu";
    private const string MenuGroupTag = "nui-menugroup";
    private const string MenuSeparatorTag = "nui-menuseparator";
    private const string MenuItemTag = "nui-menuitem";

    /// <summary>
    /// Gets the tag created for <paramref name="element"/>.
    /// </summary>
    public static string GetTag(UIElement element)
    {
        if (element is Label) return "label";
        if (element is Form) return "form";
        if (element is Fieldset) return "fieldset";
        if (element is Legend) return "legend";
        if (element is Image) return "img";
        if (element is Svg or Shapes.Shape) return "svg";

        if (element is Text)
        {
            return TextTag;
        }

        if (element is Button)
        {
            return ButtonTag;
        }

        if (element is Input)
        {
            return InputTag;
        }

        if (element is Dialog)
        {
            return DialogTag;
        }

        if (element is CanvasView)
        {
            return CanvasTag;
        }

        if (element is Switch)
        {
            return SwitchTag;
        }

        if (element is CheckBox)
        {
            return CheckBoxTag;
        }

        if (element is Toggle)
        {
            return ToggleTag;
        }

        if (element is ToggleGroup)
        {
            return ToggleGroupTag;
        }

        if (element is List)
        {
            return ListTag;
        }

        if (element is VirtualizedList)
        {
            return VirtualizedListTag;
        }

        if (element is ListItem)
        {
            return ListItemTag;
        }

        if (element is Table)
        {
            return TableTag;
        }

        if (element is VirtualizedTable)
        {
            return VirtualizedTableTag;
        }

        if (element is TableRow)
        {
            return TableRowTag;
        }

        if (element is TableCell)
        {
            return TableCellTag;
        }

        if (element is MenuGroup) return MenuGroupTag;
        if (element is MenuSeparator) return MenuSeparatorTag;

        if (element is Separator)
        {
            return SeparatorTag;
        }

        if (element is Badge)
        {
            return BadgeTag;
        }

        if (element is Avatar)
        {
            return AvatarTag;
        }

        if (element is Breadcrumb)
        {
            return BreadcrumbTag;
        }

        if (element is BreadcrumbItem)
        {
            return BreadcrumbItemTag;
        }

        if (element is ButtonGroup)
        {
            return ButtonGroupTag;
        }

        if (element is ContextMenu)
        {
            return ContextMenuTag;
        }

        if (element is DropdownMenu)
        {
            return DropdownMenuTag;
        }

        if (element is MenuItem)
        {
            return MenuItemTag;
        }

        if (element is Popover)
        {
            return PopoverTag;
        }

        if (element is Tooltip)
        {
            return TooltipTag;
        }

        return ContainerTag;
    }

    /// <summary>
    /// Gets a selector with the specificity of one type that matches every tag used by instances of <paramref name="type"/>.
    /// </summary>
    public static string GetTagSelector(Type type)
    {
        if (typeof(Image).IsAssignableFrom(type)) return ":is(img, svg)";
        if (typeof(Svg).IsAssignableFrom(type) || typeof(Shapes.Shape).IsAssignableFrom(type)) return "svg";

        if (typeof(Text).IsAssignableFrom(type))
        {
            return TextTag;
        }

        if (typeof(Button).IsAssignableFrom(type))
        {
            return ButtonTag;
        }

        if (typeof(CanvasView).IsAssignableFrom(type))
        {
            return CanvasTag;
        }

        if (typeof(Input).IsAssignableFrom(type))
        {
            return InputTag;
        }

        if (typeof(Dialog).IsAssignableFrom(type))
        {
            return DialogTag;
        }

        if (typeof(Switch).IsAssignableFrom(type))
        {
            return SwitchTag;
        }

        if (typeof(CheckBox).IsAssignableFrom(type))
        {
            return CheckBoxTag;
        }

        if (typeof(Toggle).IsAssignableFrom(type))
        {
            return ToggleTag;
        }

        if (typeof(ToggleGroup).IsAssignableFrom(type))
        {
            return ToggleGroupTag;
        }

        if (typeof(List).IsAssignableFrom(type))
        {
            return ListTag;
        }

        if (typeof(VirtualizedList).IsAssignableFrom(type))
        {
            return VirtualizedListTag;
        }

        if (typeof(ListItem).IsAssignableFrom(type))
        {
            return ListItemTag;
        }

        if (typeof(Table).IsAssignableFrom(type))
        {
            return TableTag;
        }

        if (typeof(VirtualizedTable).IsAssignableFrom(type))
        {
            return VirtualizedTableTag;
        }

        if (typeof(TableRow).IsAssignableFrom(type))
        {
            return TableRowTag;
        }

        if (typeof(TableCell).IsAssignableFrom(type))
        {
            return TableCellTag;
        }

        if (typeof(MenuGroup).IsAssignableFrom(type)) return MenuGroupTag;
        if (typeof(MenuSeparator).IsAssignableFrom(type)) return MenuSeparatorTag;

        if (typeof(Separator).IsAssignableFrom(type))
        {
            return $":is({SeparatorTag}, {MenuSeparatorTag})";
        }

        if (typeof(Badge).IsAssignableFrom(type))
        {
            return BadgeTag;
        }

        if (typeof(Avatar).IsAssignableFrom(type))
        {
            return AvatarTag;
        }

        if (typeof(Breadcrumb).IsAssignableFrom(type))
        {
            return BreadcrumbTag;
        }

        if (typeof(BreadcrumbItem).IsAssignableFrom(type))
        {
            return BreadcrumbItemTag;
        }

        if (typeof(ButtonGroup).IsAssignableFrom(type))
        {
            return ButtonGroupTag;
        }

        if (typeof(ContextMenu).IsAssignableFrom(type))
        {
            return ContextMenuTag;
        }

        if (typeof(DropdownMenu).IsAssignableFrom(type))
        {
            return $":is({DropdownMenuTag}, {ContextMenuTag})";
        }

        if (typeof(MenuItem).IsAssignableFrom(type))
        {
            return MenuItemTag;
        }

        if (typeof(Popover).IsAssignableFrom(type))
        {
            return PopoverTag;
        }

        if (typeof(Tooltip).IsAssignableFrom(type))
        {
            return TooltipTag;
        }

        // A base type or interface of Text also covers controls rendered with the container tag.
        return type.IsAssignableFrom(typeof(Text)) ? $":is({TextTag}, {ContainerTag})" : ContainerTag;
    }

    /// <summary>
    /// Gets the tag for a NativeUI element type name such as <c>text</c> or <c>view</c>.
    /// </summary>
    /// <exception cref="NotSupportedException">The name has no known HTML element.</exception>
    public static string GetTagSelector(string elementType)
    {
        return elementType.ToLowerInvariant() switch
        {
            "image" => ":is(img, svg)",
            "svg" or "path" or "rectangle" or "ellipse" or "line" or "polygon" or "polyline" => "svg",
            "text" => TextTag,
            "view" or "portal" or "progressbar" => ContainerTag,
            "dialog" => DialogTag,
            "button" => ButtonTag,
            "canvas" or "canvasview" or "viewport3d" => CanvasTag,
            "input" => InputTag,
            "switch" => SwitchTag,
            "checkbox" => CheckBoxTag,
            "toggle" => ToggleTag,
            "togglegroup" => ToggleGroupTag,
            "list" => ListTag,
            "virtualizedlist" => VirtualizedListTag,
            "listitem" => ListItemTag,
            "table" => TableTag,
            "virtualizedtable" => VirtualizedTableTag,
            "tablerow" => TableRowTag,
            "tablecell" => TableCellTag,
            "separator" => $":is({SeparatorTag}, {MenuSeparatorTag})",
            "menuseparator" => MenuSeparatorTag,
            "menugroup" => MenuGroupTag,
            "badge" => BadgeTag,
            "avatar" => AvatarTag,
            "breadcrumb" => BreadcrumbTag,
            "breadcrumbitem" => BreadcrumbItemTag,
            "buttongroup" => ButtonGroupTag,
            "dropdownmenu" => $":is({DropdownMenuTag}, {ContextMenuTag})",
            "contextmenu" => ContextMenuTag,
            "menuitem" => MenuItemTag,
            "popover" => PopoverTag,
            "tooltip" => TooltipTag,
            _ => throw new NotSupportedException($"The element type '{elementType}' has no HTML element mapping.")
        };
    }
}
