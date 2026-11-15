# Tabs

`TabControl` derives from `Selector` and uses its `SelectedIndex`, `SelectedItem`,
`SelectionChanged`, `Items`, and `ItemsSource` APIs. `TabItem.Header` describes the
header; its inherited `Content` describes the page. `SelectedContent` exposes the
selected item's content.

```xml
<TabControl>
    <TabItem Header="Overview">
        <OverviewView />
    </TabItem>
    <TabItem Header="Reports">
        <ReportsView />
    </TabItem>
</TabControl>
```

Headers accept UI elements, including stacks of icons, text, badges, and buttons.
A focusable header descendant receives its own input without activating the tab.
For data-backed tabs, `ItemTemplate` builds the header and `ContentTemplate` builds
the page. Individual tabs can override `HeaderTemplate` and `ContentTemplate`.
Closing an editor tab is an application command that removes its document from the
items source; the control selects an available neighboring tab after removal.

## Styling and indicator

The template exposes `strip`, `items`, `indicator`, and `content` parts. The indicator
is a separate absolutely positioned view. Selection and observed header/strip bounds
update its position and extent. CSS controls thickness, color, edge placement,
background, radius, shadow, and stacking. It is not a tab border.

For horizontal tabs the control supplies the indicator's left coordinate and width;
CSS supplies its vertical positioning and thickness. For vertical tabs it supplies
the top coordinate and height; CSS supplies horizontal positioning and thickness.
`TabStripPlacement` accepts `Dock.Top`, `Bottom`, `Left`, and `Right`.

The shared sample's **Tabs** page demonstrates an underline, a rounded background
indicator, and editor headers with vector close buttons. Path data in XAML is
compiled into geometry. `Stroke="currentColor"` follows the computed text color.
The sample animates indicator position and size using CSS transitions backed by the
shared desktop animation clock and browser CSS. Page content switches immediately;
page-transition animations and drag-to-reorder behavior are not implemented.

## Ownership and input

`ItemsControl` owns the container collection and calls prepare/clear hooks. Tabs keep
no parallel item collection. Each tab item owns its retained page host; the tab
control mounts that host in the content region while the item belongs to the control.
Unselected pages are hidden, retaining their instances and bindings. Removing an
explicit tab detaches its page without disposing the caller-owned tab.

NativeUI currently has a single parent tree. The page host therefore copies its
owning tab's data context and effective enabled state; it is not a second logical
child of the tab header. This is an intentional difference from WPF's separate
logical and visual trees and from a selected-content presenter that recreates pages.

Horizontal arrows, or vertical arrows for side tabs, wrap between available headers.
Home/End select the first/last available header. Tab enters the selected page.
`KeyboardNavigation.IsTabStop` controls sequential navigation for any element; the
navigation engine has no radio-item or tab-item type checks. Direct focus is separate.

Web proxies expose `tablist`, `tab`, and `tabpanel` roles, selection, roving tabindex,
and ID-based panel associations. Shared controls use the desktop layout/Skia path.

## Verification

```sh
dotnet build NativeUI.slnx
dotnet run --project tests/NativeUI.Controls.Tests -- --tabs
dotnet run --project tests/NativeUI.Controls.Tests -- --focus
dotnet run --project tests/NativeUI.Controls.Web.Tests -- --tabs
```

Web proxy tests run headlessly and verify managed state and lifetimes. They do not
replace live-browser accessibility or visual inspection.
