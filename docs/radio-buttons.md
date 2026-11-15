# Radio groups and buttons

`RadioGroup.SelectedValue` binds a choice to the ViewModel. `RadioGroupItem.Value` identifies each option. Layout wrappers and separate labels are supported.

```xml
<RadioGroup SelectedValue="{Binding SelectedOption, Mode=TwoWay}"
            Required="True"
            Spacing="8">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <RadioGroupItem x:Name="OptionOne" Value="option-one" />
        <Label Target="{x:Reference OptionOne}" Content="Option One" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="12">
        <RadioGroupItem x:Name="OptionTwo" Value="option-two" />
        <Label Target="{x:Reference OptionTwo}" Content="Option Two" />
    </StackPanel>
</RadioGroup>
```

Initialize `SelectedOption` to `"option-one"` for the initial selection. An unbound group can instead set `SelectedValue="option-one"` directly in XAML. There is no separate DefaultValue property.

## Selection and input

Item values should be distinct and non-null within their group. Values are compared with object equality. Setting SelectedValue selects only the first matching item in tree order, including when duplicate values exist; a null value clears the selection. An unmatched value is retained until a matching item is added. Removing or disposing a selected item retains SelectedValue. A later matching item becomes checked. Changing an item's Value does not replace SelectedValue; its checked state is synchronized against the retained value. SelectionChanged bubbles exactly once for each SelectedValue transition with the removed and added values. Reentrant transitions are queued in order. Membership changes and synchronization alone do not raise SelectionChanged.

The nearest RadioGroup contains its descendant choices, including those inside labels and layout wrappers. Nested groups establish independent boundaries. Orientation and Spacing are inherited from StackPanel.

Pointer, Space, Enter, label, and programmatic PerformClick activation select an item. Activating the selected item keeps it selected. Left/Up move to the previous choice and Right/Down move to the next choice in either orientation. Navigation wraps and skips unavailable choices. Arrows remain unhandled when there is no eligible destination. NativeUI has no layout FlowDirection property yet; horizontal navigation currently follows LTR order. Tab stops at the selected eligible radio, or the first eligible radio when none is selected. Negative TabIndex values remain outside the Tab order.

RadioButton owns only its checked state, individual validation, activation, and routed Checked, Unchecked, and Click events. It has no group logic. RadioGroupItem adds Value and registers with the nearest RadioGroup when its ancestry changes, including ancestor-container moves before attachment. Removing, reparenting, or disposing an item unregisters it. RadioGroup keeps registered items in tree order and handles selection and keyboard navigation. It does not scan descendants or subscribe to child collections. Visibility changes use a separate notification from ancestry changes.

## Forms, validation, and reset

RadioGroup implements IFormControl. RadioGroup.Required is authoritative and checks that an enabled choice is selected. Disabled or disposed checked items do not satisfy the constraint. Visibility and focusability affect keyboard navigation, not constraint validity. Item-level Required is not aggregated by RadioGroup. Binding validation on SelectedValue belongs to the group, independently of item constraints. Validation.GetErrors(group), ValidationMessage, CheckValidity(), and ReportValidity() follow the shared form API. RequiredErrorMessage is application-localizable. ReportValidity focuses an eligible radio when the group is invalid.

Form calls CaptureInitialValue when registering a group. Standalone groups call CaptureInitialValue after configuration to define their baseline. Form reset restores that value through its binding. DataContext replacement also captures a baseline after the binding engine applies the new source values. Group items defer their form reset to the group. This prevents independent item resets from overwriting the selected-value baseline. Reset does not implement ViewModel rollback.

## Styling and Web mapping

RadioGroupItem exposes indicator and icon parts. RadioButton additionally exposes a content part. CSS controls dimensions, circle shape, colors, and spacing. Inherited RadioButton selectors also match RadioGroupItem. States include checked, unchecked, disabled, focus-visible, and form validity states.

Web renders RadioGroup as a div with role="radiogroup" and aria-orientation. Associate a Label with the group to provide its accessible name. RadioGroupItem uses a DOM custom element with role="radio", aria-checked, and group-aware tabindex. Shared C# controls selection and reset; native HTML value serialization is not provided.

The Toggles and Forms samples demonstrate the composed item/label API in both themes. Forms binds a single Delivery value.

## Verification

```sh
dotnet build NativeUI.slnx
dotnet run --project tests/NativeUI.Controls.Tests -- --radio
dotnet run --project tests/NativeUI.Controls.Web.Tests -- --radio
dotnet run --project tests/NativeUI.Controls.Tests -- --forms
```

Focused checks cover selection from code, labels, dynamic items, reentrant selection callbacks, grouping, keyboard navigation, required validation, reset, reparenting, and Web proxy lifetime. Live browser and assistive-technology verification remains required.
