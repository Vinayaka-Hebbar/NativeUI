using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class FormTests
{
    public static void VerifySubmissionAndReset()
    {
        Model model = new() { Name = "Initial" };
        using Form form = new() { DataContext = model };
        Input input = new() { Required = true };
        input.SetBinding(Input.ValueProperty, CompiledBinding.Create<Model, string>(nameof(Model.Name), static source => source.Name,
            static (source, value) => source.Name = value, BindingMode.TwoWay));
        CheckBox check = new() { IsChecked = true };
        Button save = new() { Type = ButtonType.Submit };
        Button reset = new() { Type = ButtonType.Reset };
        Button ordinary = new();
        form.Children.Add(input);
        form.Children.Add(check);
        form.Children.Add(save);
        form.Children.Add(reset);
        form.Children.Add(ordinary);
        Command command = new();
        form.SubmitCommand = command;
        Button? submitter = null;
        form.Submit += (_, args) => submitter = args.Submitter;
        ordinary.PerformClick();
        Assert.Equal(0, command.Executions);
        input.Value = "Edited";
        check.IsChecked = false;
        save.PerformClick();
        Assert.Equal(1, command.Executions);
        Assert.Same(save, submitter ?? throw new InvalidOperationException("Missing submitter."));
        EventHandler<RoutedEventArgs> suppressSubmit = static (_, args) => args.Handled = true;
        save.Click += suppressSubmit;
        save.PerformClick();
        Assert.Equal(1, command.Executions);
        save.Click -= suppressSubmit;
        reset.PerformClick();
        Assert.Equal("Initial", input.Value);
        Assert.Equal("Initial", model.Name);
        Assert.Equal(true, check.IsChecked);
        input.Value = string.Empty;
        save.PerformClick();
        Assert.Equal(1, command.Executions);
        Assert.False(form.IsValid);
        input.IsEnabled = false;
        Assert.True(form.Validate());
        input.IsEnabled = true;
        input.Value = "Valid";
        command.Allowed = false;
        save.PerformClick();
        Assert.Equal(1, command.Executions);
        form.DataContext = new Model { Name = "Replacement" };
        input.Value = "Changed";
        form.ResetForm();
        Assert.Equal("Replacement", input.Value);
        using Window host = new() { Content = form };
        input.Value = "Edited before detachment";
        host.Content = null;
        form.ResetForm();
        Assert.Equal("Replacement", input.Value);
    }

    public static void VerifyFieldsetLegendSemantics()
    {
        using Form form = new();
        Fieldset fieldset = new();
        Legend first = new();
        Legend second = new();
        Input exempt = new();
        Input ordinary = new() { Required = true };
        Input laterLegend = new();
        first.Children.Add(exempt);
        second.Children.Add(laterLegend);
        fieldset.Children.Add(first);
        fieldset.Children.Add(ordinary);
        fieldset.Children.Add(second);
        form.Children.Add(fieldset);
        using Window host = new() { Content = form };
        Assert.True(exempt.Focus(FocusState.Keyboard));
        fieldset.IsEnabled = false;
        Assert.True(exempt.IsEffectivelyEnabled);
        Assert.True(exempt.IsFocused);
        Assert.False(ordinary.IsEffectivelyEnabled);
        Assert.False(laterLegend.IsEffectivelyEnabled);
        Assert.True(form.Validate());
        fieldset.MoveChild(0, 2);
        Assert.False(exempt.IsEffectivelyEnabled);
        Assert.False(exempt.IsFocused);
        Assert.True(laterLegend.IsEffectivelyEnabled);
        form.IsEnabled = false;
        Assert.False(laterLegend.IsEffectivelyEnabled);
        Assert.True(ordinary.IsEnabled);
    }

    public static void VerifyIndependentBindingErrors()
    {
        Model first = new() { Name = "First" };
        Model second = new() { Name = "Second" };
        using Input input = new() { DataContext = first };
        using IDisposable binding = input.SetBinding(Input.ValueProperty, CompiledBinding.Create<Model, string>(nameof(Model.Name), static source => source.Name,
            static (source, value) => source.Name = value, BindingMode.TwoWay));
        Assert.Equal(1, first.ErrorSubscribers);
        first.SetError("Name is unavailable.");
        Assert.True(Validation.GetHasError(input));
        Assert.Equal("Name is unavailable.", Validation.GetErrors(input)[0].ErrorMessage);
        Assert.True(input.States.HasFlag(ElementStates.Invalid));
        Assert.Equal("First", input.Value);
        first.SetError(null);
        Assert.False(Validation.GetHasError(input));
        input.DataContext = second;
        Assert.Equal(0, first.ErrorSubscribers);
        Assert.Equal(1, second.ErrorSubscribers);
        first.SetError("Stale");
        Assert.False(Validation.GetHasError(input));
        binding.Dispose();
        Assert.Equal(0, second.ErrorSubscribers);
        second.SetError("Disposed");
        Assert.False(Validation.GetHasError(input));
    }

    public static void VerifyConversionAndLegacyErrors()
    {
        LegacyModel model = new() { Age = 21 };
        using Input input = new() { DataContext = model };
        using IDisposable binding = input.SetBinding(Input.ValueProperty, CompiledBinding.Create<LegacyModel, int>(nameof(LegacyModel.Age), static source => source.Age,
            static (source, value) => source.Age = value, BindingMode.TwoWay));
        Assert.Equal("21", input.Value);
        input.Value = "unfinished";
        Assert.Equal(21, model.Age);
        Assert.Equal("unfinished", input.Value);
        Assert.True(Validation.GetHasError(input));
        Assert.Equal(ValidationErrorKind.Conversion, Validation.GetErrors(input)[0].Kind);
        Assert.Same(Input.ValueProperty, Validation.GetErrors(input)[0].TargetProperty!);
        input.CheckValidity();
        Assert.Equal("unfinished", input.Value);
        input.Value = "17";
        Assert.Equal(17, model.Age);
        Assert.True(Validation.GetHasError(input));
        Assert.Equal("Must be at least 18.", Validation.GetErrors(input)[0].ErrorMessage);
        input.Value = "30";
        Assert.False(Validation.GetHasError(input));
        using Input path = new() { DataContext = model };
        using IDisposable pathBinding = PathBinding.Bind(path, Input.ValueProperty, nameof(LegacyModel.Age), BindingMode.TwoWay);
        path.Value = "bad";
        Assert.True(Validation.GetHasError(path));
        Assert.Equal(30, model.Age);
        path.Value = "42";
        Assert.Equal(42, model.Age);
        Assert.False(Validation.GetHasError(path));
    }

    public static void VerifyAsyncSubmission()
    {
        using Form form = new();
        AsyncCommand command = new();
        form.SubmitCommand = command;
        form.RequestSubmit();
        Assert.True(form.IsSubmitting);
        form.RequestSubmit();
        Assert.Equal(1, command.Executions);
        command.Completion.SetResult();
        form.SubmissionTask.GetAwaiter().GetResult();
        Assert.False(form.IsSubmitting);
        AsyncCommand failure = new();
        form.SubmitCommand = failure;
        form.RequestSubmit();
        failure.Completion.SetException(new InvalidOperationException("Save failed"));
        bool faulted = false;
        try
        {
            form.SubmissionTask.GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
            faulted = true;
        }

        Assert.True(faulted);
        Assert.False(form.IsSubmitting);
    }

    public static void VerifyNestedAndDynamicForms()
    {
        using Form outer = new();
        using Form inner = new();
        bool rejected = false;
        try
        {
            outer.Children.Add(inner);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert.True(rejected);
        Assert.Equal(0, outer.Children.Count);
        using StackPanel container = new();
        container.Children.Add(inner);
        rejected = false;
        try
        {
            outer.Children.Add(container);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert.True(rejected);
        Input required = new() { Required = true };
        outer.Children.Add(required);
        Assert.False(outer.IsValid);
        outer.Children.Remove(required);
        Assert.True(outer.IsValid);
        required.Value = "Detached";
        Assert.True(outer.IsValid);
        required.Dispose();
    }

    public static void VerifyValidationFocusAndReentrancy()
    {
        using Form form = new();
        Input required = new() { Required = true };
        form.Children.Add(required);
        Command command = new();
        form.SubmitCommand = command;
        using Window host = new() { Content = form };
        form.RequestSubmit();
        Assert.True(required.IsFocused);
        Assert.Equal(0, command.Executions);
        required.Value = "Valid";
        ProbeField probe = new();
        form.Children.Add(probe);
        probe.OnValidate = form.RequestSubmit;
        form.RequestSubmit();
        Assert.Equal(1, command.Executions);
        form.Submit += (_, args) => args.Cancel = true;
        form.RequestSubmit();
        Assert.Equal(1, command.Executions);
        int resets = 0;
        form.Reset += (_, _) => { resets++; form.ResetForm(); };
        form.ResetForm();
        Assert.Equal(1, resets);
    }

    public static void VerifyValidationLayers()
    {
        Model model = new() { Name = "Display" };
        using Text display = new() { DataContext = model };
        using IDisposable displayBinding = display.SetBinding(Text.ValueProperty,
            CompiledBinding.Create<Model, string>(nameof(Model.Name), static source => source.Name));
        using CheckBox check = new() { DataContext = model };
        using IDisposable checkBinding = check.SetBinding(CheckBox.IsCheckedProperty,
            CompiledBinding.Create<Model, bool?>(nameof(Model.Accepted), static source => source.Accepted,
                static (source, value) => source.Accepted = value, BindingMode.TwoWay));
        model.SetError("Invalid model value.");
        Assert.True(Validation.GetHasError(display));
        Assert.False(display.States.HasFlag(ElementStates.Invalid));
        Assert.True(Validation.GetHasError(check));
        Assert.True(check.Validity.Valid);
        Assert.True(check.States.HasFlag(ElementStates.Invalid));
        Assert.Same(CheckBox.IsCheckedProperty, Validation.GetErrors(check)[0].TargetProperty!);
        Assert.Equal(ValidationErrorKind.Data, Validation.GetErrors(check)[0].Kind);
        check.PerformClick();
        Assert.True(check.States.HasFlag(ElementStates.UserInvalid));
        model.SetError(null);
        Assert.True(check.CheckValidity());
        Assert.True(check.States.HasFlag(ElementStates.Valid));
        Assert.False(check.States.HasFlag(ElementStates.UserInvalid));
        check.Required = true;
        check.IsChecked = false;
        Assert.True(check.Validity.ValueMissing);
        Assert.False(Validation.GetHasError(check));
        check.IsEnabled = false;
        Assert.True(check.CheckValidity());
        Assert.False(check.States.HasFlag(ElementStates.Invalid));
        Assert.False(check.States.HasFlag(ElementStates.Valid));
    }

    public static void VerifyLabels()
    {
        using StackPanel root = new();
        Input first = new();
        Input second = new();
        Label label = new() { Target = first, Content = "Name" };
        CheckBox check = new();
        Text caption = new() { Value = "Accept terms" };
        StackPanel content = new();
        content.Children.Add(check);
        content.Children.Add(caption);
        Label implicitLabel = new() { Content = content };
        root.Children.Add(label);
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(implicitLabel);
        using Window host = new() { Content = root };
        Assert.Equal("Name", Label.GetAccessibleName(first));
        label.Activate();
        Assert.True(first.IsFocused);
        label.Target = second;
        Assert.Equal(string.Empty, Label.GetAccessibleName(first));
        Assert.Equal("Name", Label.GetAccessibleName(second));
        label.Activate();
        Assert.True(second.IsFocused);
        Assert.Equal("Accept terms", Label.GetAccessibleName(check));
        int clicks = 0;
        root.AddHandler(CheckBox.ClickEvent, (_, args) =>
        {
            Assert.Same(check, args.Source!);
            Assert.Equal(true, check.IsChecked);
            clicks++;
        });
        implicitLabel.Activate();
        Assert.Equal(true, check.IsChecked);
        Assert.Equal(1, clicks);
        check.IsEnabled = false;
        implicitLabel.Activate();
        Assert.Equal(true, check.IsChecked);
        Assert.Equal(1, clicks);
        caption.Value = "Accepter les conditions";
        Assert.Equal("Accepter les conditions", Label.GetAccessibleName(check));
        root.Children.Remove(label);
        Assert.Equal(string.Empty, Label.GetAccessibleName(second));
        label.Dispose();
        first.Dispose();
        implicitLabel.Dispose();
    }

    public static void VerifyCompiledSample()
    {
        using FormSampleView view = new();
        FormSampleViewModel model = (FormSampleViewModel)(view.DataContext ?? throw new InvalidOperationException("Missing model."));
        Assert.False(view.EditorForm.Validate());
        Assert.False(view.SaveButton.IsEffectivelyEnabled);
        model.Name = "Vinayaka";
        model.Email = "invalid";
        Assert.False(view.EditorForm.Validate());
        Assert.False(Validation.GetHasError(view.NameInput));
        Assert.Equal(string.Empty, view.NameErrors.Value);
        Assert.Equal("Enter a valid email address.", view.EmailErrors.Value);
        model.Email = "vinayaka@example.com";
        Assert.True(view.EditorForm.Validate());
        Assert.True(view.SaveButton.IsEffectivelyEnabled);
        Assert.Equal(Cursor.Pointer, view.SaveButton.ComputedStyle.Cursor);
        view.SaveButton.PerformClick();
        Assert.True(model.Status.StartsWith("Saved", StringComparison.Ordinal));
        view.EditorForm.ResetForm();
        Assert.Equal(string.Empty, model.Name);
        Assert.Equal(string.Empty, model.Email);
        Assert.False(view.SaveButton.IsEffectivelyEnabled);
        Assert.Equal(Cursor.NotAllowed, view.SaveButton.ComputedStyle.Cursor);

        using TestTheme theme = new();
        theme.Apply(view, Theme.Dark);
        Assert.Equal(Color.FromRgbBytes(248, 250, 252), view.NameInput.ComputedStyle.TextColor);
        Assert.Equal(Color.FromRgbBytes(252, 165, 165), view.EmailErrors.ComputedStyle.TextColor);
        theme.Apply(view, Theme.Light);
        Assert.Equal(Color.FromRgbBytes(15, 23, 42), view.NameInput.ComputedStyle.TextColor);
        Assert.Equal(Color.FromRgbBytes(185, 28, 28), view.EmailErrors.ComputedStyle.TextColor);
    }

    private sealed class Model : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private string _name = string.Empty;
        private string? _error;
        private EventHandler<DataErrorsChangedEventArgs>? _errorsChanged;
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
        public bool? Accepted { get; set; }
        public int ErrorSubscribers { get; private set; }
        public bool HasErrors => _error is not null;
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
        {
            add { _errorsChanged += value; ErrorSubscribers++; }
            remove { _errorsChanged -= value; ErrorSubscribers--; }
        }
        public IEnumerable GetErrors(string? propertyName) => propertyName is nameof(Name) or nameof(Accepted) && _error is not null ? new[] { _error } : Array.Empty<string>();
        public void SetError(string? error)
        {
            _error = error;
            _errorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(null));
        }
    }

    private sealed class LegacyModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private int _age;
        public int Age { get => _age; set { _age = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age))); } }
        public string Error => string.Empty;
        public string this[string propertyName] => propertyName == nameof(Age) && Age < 18 ? "Must be at least 18." : string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class Command : ICommand
    {
        public int Executions { get; private set; }
        public bool Allowed { get; set; } = true;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Allowed;
        public void Execute(object? parameter) => Executions++;
    }

    private sealed class AsyncCommand : IAsyncCommand
    {
        public int Executions { get; private set; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => throw new InvalidOperationException("Use asynchronous execution.");
        public Task ExecuteAsync(object? parameter) { Executions++; return Completion.Task; }
    }

    private sealed class ProbeField : UIElement, IFormControl
    {
        public override string TagName => "probe";
        public Action? OnValidate { get; set; }
        public bool WillValidate => IsEffectivelyEnabled;
        public ValidityState Validity => ValidityState.Empty;
        public string ValidationMessage => string.Empty;
        public bool CheckValidity() { OnValidate?.Invoke(); return true; }
        public bool ReportValidity() => CheckValidity();
        public void CaptureInitialValue() { }
        public void Reset() { }
    }
}
