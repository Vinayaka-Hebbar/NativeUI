# Forms and validation

`Form`, `Fieldset`, `Legend`, and `Label` are shared controls. Web proxies create real HTML elements; desktop uses shared layout and Skia drawing, with native text fields for editing.

```xml
<Form SubmitCommand="{Binding SaveCommand}">
    <StackPanel Spacing="16">
        <Fieldset>
            <Legend Content="Personal information" />
            <Label Target="{x:Reference EmailInput}" Content="Email" />
            <Input x:Name="EmailInput"
                   Value="{Binding Email, Mode=TwoWay}"
                   Required="True" />
        </Fieldset>
        <Button Type="Submit" Text="Save" />
        <Button Type="Reset" Text="Reset" />
    </StackPanel>
</Form>
```

## Validation ownership

The binding system observes `INotifyDataErrorInfo` and `IDataErrorInfo`, and captures conversion failures. `Validation.GetHasError(element)` and `Validation.GetErrors(element)` work on any `UIElement`, including display elements. Errors identify the source property, target bindable property, and error kind. Property bindings query only their own property's errors; an entity-wide error collection is not copied onto unrelated fields. DataAnnotations work through a validating ViewModel such as CommunityToolkit's `ObservableValidator`.

`IFormControl.Validity` describes HTML-style constraints independently of model errors. `Input`, `CheckBox`, and `RadioButton` implement the required-value constraint. For radios, a required group needs one checked option. Other constraint flags are available in `ValidityState` for custom controls; built-in pattern, range, length, and date controls are not implemented by this slice.

`CheckValidity()` combines constraints and binding errors. `ReportValidity()` also marks user interaction and focuses an invalid control. Only participating form controls receive `:valid`, `:invalid`, and `:user-invalid`. A display element can have attached binding errors without receiving these states.

Disabled controls and read-only inputs do not participate in validation. A disabled fieldset exempts descendants of its first direct legend from its own disabled state; restrictions from other ancestors still apply.

## Labels

An explicit `Label.Target` can use a forward `x:Reference` within the view's name scope. An implicit label finds the first contained `IFormControl`. Wrap multiple children in a `StackPanel`, since `Label` has one `Content` value. Activation focuses inputs and toggles checkboxes. Custom form controls can implement `IFormControl.Activate()`.

Web uses native `<label>` associations and accessible-name metadata. macOS native inputs receive label text and validation help. A complete desktop accessibility hierarchy for all Skia controls remains separate framework work.

## Submission and reset

`Button.Type` defaults to `Button`. Submit buttons call `Form.RequestSubmit()`; reset buttons call `ResetForm()`. Forms cannot nest. Submission validates participating descendants, focuses the first invalid field, raises the cancelable `Submit` event, then executes the command if allowed.

`IsSubmitting` remains true for commands implementing `NativeUI.Core.IAsyncCommand` until `ExecuteAsync` finishes. Plain `ICommand` execution is synchronous. Observe `SubmissionTask` for completion, cancellation, or exceptions.

Form reset restores each control's baseline captured on registration or DataContext replacement, through its normal bindings. Two-way bindings can write those values to the ViewModel; reset does not implement model rollback. Custom controls can supply their own baseline through `CaptureInitialValue()` and `Reset()`.

Localization belongs to the application. Bind label content and `RequiredErrorMessage` to localized values, and refresh model validation messages through the ViewModel. No localization service or culture-switching API is introduced here.

## Verification and sample

The sample navigation includes **Forms**. Build and run the focused checks with:

```sh
dotnet build NativeUI.slnx
dotnet run --project tests/NativeUI.Controls.Tests -- --forms
```

Headless checks exercise shared behavior and DOM proxy dispatch. They do not establish browser-native interaction or native text-field rendering correctness.
