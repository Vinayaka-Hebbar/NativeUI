using System.Runtime.CompilerServices;
using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Sample;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class RadioButtonTests
{
    public static void VerifyGrouping()
    {
        using RadioGroup root = new() { SelectedValue = "first" };
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem second = new() { Value = "second" };
        root.Children.Add(first);
        root.Children.Add(second);
        using Window host = new() { Content = root };
        int clicks = 0;
        int checkedCount = 0;
        int uncheckedCount = 0;
        second.Click += (_, _) => clicks++;
        second.Checked += (_, _) => checkedCount++;
        first.Unchecked += (_, _) => uncheckedCount++;
        second.PerformClick();
        second.PerformClick();
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(true, second.IsChecked);
        Assert.Equal(2, clicks);
        Assert.Equal(1, checkedCount);
        Assert.Equal(1, uncheckedCount);
        using RadioButton independent = new() { IsChecked = true };
        using RadioButton other = new() { IsChecked = true };
        independent.PerformClick();
        Assert.Equal(true, other.IsChecked);
    }

    public static void VerifyKeyboard()
    {
        using StackPanel root = new();
        Button before = new();
        RadioGroup group = new();
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem disabled = new() { Value = "disabled", IsEnabled = false };
        RadioGroupItem hidden = new() { Value = "hidden", IsVisible = false };
        RadioGroupItem last = new() { Value = "last" };
        Button after = new();
        root.Children.Add(before);
        group.Children.Add(first);
        group.Children.Add(disabled);
        group.Children.Add(hidden);
        group.Children.Add(last);
        root.Children.Add(group);
        root.Children.Add(after);
        using Window host = new() { Content = root };
        Assert.Same(first, KeyboardNavigation.GetNext(root, before, FocusNavigationDirection.Next)!);
        first.Focus(FocusState.Keyboard);
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
        Assert.Equal(true, last.IsChecked);
        Assert.True(last.IsFocused);
        Assert.True(last.States.HasFlag(ElementStates.FocusVisible));
        Assert.Equal(1, new[] { first, disabled, hidden, last }.Count(static item => item.IsTabStop));
        Assert.Same(after, KeyboardNavigation.GetNext(root, last, FocusNavigationDirection.Next)!);
        Assert.Same(last, KeyboardNavigation.GetNext(root, after, FocusNavigationDirection.Previous)!);
        last.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
        Assert.True(first.IsFocused);
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(false, last.IsChecked);
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowUp));
        Assert.True(last.IsFocused);
        KeyEventArgs crossAxis = new(Key.ArrowRight);
        last.RaiseEvent(UIElement.KeyDownEvent, crossAxis);
        Assert.True(crossAxis.Handled);
        Assert.True(first.IsFocused);
        group.Orientation = Orientation.Horizontal;
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowRight));
        Assert.True(last.IsFocused);
        last.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowRight));
        Assert.True(first.IsFocused);
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowLeft));
        Assert.True(last.IsFocused);
        last.IsEnabled = false;
        Assert.Same(first, KeyboardNavigation.GetNext(root, before, FocusNavigationDirection.Next)!);
        first.Focus(FocusState.Keyboard);
        KeyEventArgs single = new(Key.ArrowRight);
        first.RaiseEvent(UIElement.KeyDownEvent, single);
        Assert.False(single.Handled);
        first.IsEnabled = false;
        KeyEventArgs empty = new(Key.ArrowRight);
        first.RaiseEvent(UIElement.KeyDownEvent, empty);
        Assert.False(empty.Handled);
    }

    public static void VerifyFormsAndBindings()
    {
        using FormSampleView view = new();
        FormSampleViewModel model = (FormSampleViewModel)view.DataContext!;
        model.Name = "Vinayaka";
        model.Email = "vinayaka@example.com";
        Assert.True(view.EditorForm.Validate());
        Assert.Equal(true, view.StandardDelivery.IsChecked);
        view.ExpressDelivery.PerformClick();
        Assert.Equal("express", model.Delivery);
        Assert.Equal(false, view.StandardDelivery.IsChecked);
        model.Delivery = null;
        Assert.True(view.DeliveryGroup.Validity.ValueMissing);
        Assert.False(view.DeliveryGroup.CheckValidity());
        Assert.True(Validation.GetHasError(view.DeliveryGroup));
        Assert.False(view.EditorForm.IsValid);
        Assert.False(Validation.GetHasError(view.StandardDelivery));
        view.EditorForm.ResetForm();
        Assert.Equal("standard", model.Delivery);
        Assert.Equal(false, view.ExpressDelivery.IsChecked);
        Assert.True(view.DeliveryGroup.CheckValidity());
        view.DeliveryGroup.IsEnabled = false;
        model.Delivery = null;
        Assert.True(view.DeliveryGroup.CheckValidity());
    }

    public static void VerifyDynamicValidity()
    {
        using Form form = new();
        RadioGroup group = new() { Required = true, SelectedValue = "selected" };
        RadioGroupItem required = new() { Value = "required" };
        RadioGroupItem selected = new() { Value = "selected" };
        group.Children.Add(required);
        group.Children.Add(selected);
        form.Children.Add(group);
        using Window host = new() { Content = form };
        Assert.True(form.IsValid);
        group.Children.Remove(selected);
        Assert.False(form.IsValid);
        Assert.True(group.States.HasFlag(ElementStates.Invalid));
        selected.Dispose();
        group.IsEnabled = false;
        Assert.True(form.IsValid);
        group.IsEnabled = true;
        required.PerformClick();
        Assert.True(form.IsValid);
        Assert.True(group.States.HasFlag(ElementStates.Valid));
    }

    public static void VerifyExplicitGroups()
    {
        using StackPanel root = new();
        RadioGroup firstGroup = new() { Required = true };
        RadioGroup secondGroup = new();
        RadioGroup nestedGroup = new();
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem second = new() { Value = "second" };
        RadioGroupItem other = new() { Value = "other" };
        RadioGroupItem nested = new() { Value = "nested" };
        RadioButton outside = new() { IsChecked = true };
        Label label = new() { Content = first };
        StackPanel wrapper = new();
        wrapper.Children.Add(second);
        firstGroup.Children.Add(label);
        firstGroup.Children.Add(wrapper);
        nestedGroup.Children.Add(nested);
        firstGroup.Children.Add(nestedGroup);
        secondGroup.Children.Add(other);
        root.Children.Add(firstGroup);
        root.Children.Add(secondGroup);
        root.Children.Add(outside);
        firstGroup.SelectedValue = "first";
        secondGroup.SelectedValue = "other";
        nestedGroup.SelectedValue = "nested";
        using Window host = new() { Content = root };
        second.PerformClick();
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(true, second.IsChecked);
        Assert.Equal(true, other.IsChecked);
        Assert.Equal(true, nested.IsChecked);
        Assert.Equal(true, outside.IsChecked);
        Assert.True(firstGroup.CheckValidity());
        label.Activate();
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(false, second.IsChecked);
        Assert.Same(first, KeyboardNavigation.GetNext(root, root, FocusNavigationDirection.First)!);
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
        Assert.True(second.IsFocused);
        Assert.Equal(true, second.IsChecked);
        Assert.Equal(true, nested.IsChecked);
    }

    public static void VerifyValueSelection()
    {
        using RadioGroup group = new() { SelectedValue = "two", Required = true };
        RadioGroupItem first = new() { Value = "one" };
        RadioGroupItem second = new() { Value = "two" };
        StackPanel firstRow = new();
        StackPanel secondRow = new();
        Label label = new() { Target = first, Content = "First" };
        firstRow.Children.Add(first);
        firstRow.Children.Add(label);
        secondRow.Children.Add(second);
        group.Children.Add(firstRow);
        group.Children.Add(secondRow);
        using Window host = new() { Content = group };
        Assert.Equal(true, second.IsChecked);
        Assert.Equal(false, first.IsChecked);
        EventHandler<RoutedEventArgs> handled = static (_, args) => args.Handled = true;
        first.Checked += handled;
        label.Activate();
        Assert.Equal("one", group.SelectedValue);
        Assert.Equal(false, second.IsChecked);
        group.SelectedValue = "later";
        Assert.Equal(false, first.IsChecked);
        Assert.True(group.Validity.ValueMissing);
        Assert.False(Validation.GetHasError(group));
        RadioGroupItem late = new() { Value = "later" };
        group.Children.Add(late);
        Assert.Equal(true, late.IsChecked);
        late.Value = "changed";
        Assert.Equal("later", group.SelectedValue);
        Assert.Equal(false, late.IsChecked);
        group.SelectedValue = "changed";
        late.IsChecked = false;
        Assert.Equal("changed", group.SelectedValue);
        Assert.Equal(true, late.IsChecked);
        group.SelectedValue = "two";
        group.Children.Remove(secondRow);
        Assert.Equal("two", group.SelectedValue);
        Assert.Equal(false, first.IsChecked);
        Assert.True(group.Validity.ValueMissing);
        first.Checked -= handled;
        first.Checked += (_, _) => group.SelectedValue = "changed";
        object? notified = null;
        group.SelectionChanged += (_, args) => notified = args.AddedItems.FirstOrDefault();
        group.SelectedValue = "one";
        Assert.Equal("changed", group.SelectedValue);
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(true, late.IsChecked);
        Assert.Equal("changed", notified);
        secondRow.Dispose();
    }

    public static void VerifyEnabledSelectionValidity()
    {
        using Form form = new();
        RadioGroup group = new() { Required = true, SelectedValue = "first" };
        StackPanel wrapper = new() { IsEnabled = false };
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem second = new() { Value = "second" };
        wrapper.Children.Add(first);
        group.Children.Add(wrapper);
        group.Children.Add(second);
        form.Children.Add(group);
        using Window host = new() { Content = form };
        Assert.Equal(true, first.IsChecked);
        Assert.True(group.Validity.ValueMissing);
        Assert.False(form.IsValid);
        wrapper.IsEnabled = true;
        Assert.True(group.Validity.Valid);
        Assert.True(form.IsValid);
        wrapper.IsVisible = false;
        Assert.True(form.IsValid);
        Assert.False(first.IsTabStop);
        Assert.True(second.IsTabStop);
        wrapper.IsVisible = true;
        first.Focusable = false;
        Assert.True(form.IsValid);
        Assert.False(first.IsTabStop);
        first.Focusable = true;
        Assert.True(first.IsTabStop);
        first.IsEnabled = false;
        Assert.True(group.States.HasFlag(ElementStates.Invalid));
        Assert.False(form.IsValid);
        group.Required = false;
        Assert.True(group.CheckValidity());
        Assert.True(form.IsValid);
        group.SelectedValue = null;
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(false, second.IsChecked);
    }

    public static void VerifyStructuralChangesDuringSelection()
    {
        using RadioGroup group = new() { Required = true };
        StackPanel wrapper = new();
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem late = new() { Value = "late" };
        wrapper.Children.Add(first);
        group.Children.Add(wrapper);
        using Window host = new() { Content = group };
        first.Checked += (_, _) => group.Children.Add(late);
        group.SelectedValue = "first";
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(false, late.IsChecked);
        group.SelectedValue = "late";
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(true, late.IsChecked);
        group.Children.Remove(wrapper);
        first.Value = "detached";
        wrapper.Children.Add(new RadioGroupItem { Value = "unobserved" });
        Assert.Equal("late", group.SelectedValue);
        Assert.True(group.CheckValidity());
        group.Children.Remove(late);
        Assert.Equal("late", group.SelectedValue);
        Assert.False(group.CheckValidity());
        wrapper.Dispose();
        late.Dispose();
    }

    public static void VerifyUnattachedAncestorRegistration()
    {
        using RadioGroup first = new() { SelectedValue = "choice", Required = true };
        using RadioGroup second = new() { SelectedValue = "other" };
        StackPanel wrapper = new();
        StackPanel row = new();
        RadioGroupItem item = new() { Value = "choice" };
        row.Children.Add(item);
        wrapper.Children.Add(row);
        first.Children.Add(wrapper);
        Assert.Equal(true, item.IsChecked);
        Assert.True(first.CheckValidity());
        first.Children.Remove(wrapper);
        Assert.Equal("choice", first.SelectedValue);
        Assert.False(first.CheckValidity());
        second.Children.Add(wrapper);
        Assert.Same(row, item.Parent!);
        Assert.Same(second, item.Group!);
        Assert.Equal(false, item.IsChecked);
        second.SelectedValue = "choice";
        Assert.Equal(true, item.IsChecked);
        RadioGroup nested = new() { SelectedValue = "nested" };
        RadioGroupItem nestedItem = new() { Value = "nested" };
        nested.Children.Add(nestedItem);
        wrapper.Children.Add(nested);
        second.Children.Remove(wrapper);
        first.Children.Add(wrapper);
        Assert.Same(first, item.Group!);
        Assert.Same(nested, nestedItem.Group!);
        Assert.Equal(true, nestedItem.IsChecked);
        Assert.Equal("choice", second.SelectedValue);
        wrapper.IsVisible = false;
        Assert.True(first.CheckValidity());
        wrapper.IsVisible = true;
        Assert.True(first.CheckValidity());
    }

    public static void VerifyDuplicateTreeOrder()
    {
        using RadioGroup group = new() { SelectedValue = "same" };
        StackPanel lastRow = new();
        RadioGroupItem last = new() { Value = "same" };
        lastRow.Children.Add(last);
        group.Children.Add(lastRow);
        int changes = 0;
        group.SelectionChanged += (_, _) => changes++;
        StackPanel firstRow = new();
        RadioGroupItem first = new() { Value = "same" };
        firstRow.Children.Add(first);
        group.Children.Insert(0, firstRow);
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(false, last.IsChecked);
        group.MoveChild(0, 1);
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(true, last.IsChecked);
        group.Children.Remove(lastRow);
        Assert.Equal("same", group.SelectedValue);
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(0, changes);
        first.Dispose();
        Assert.Null(first.Group);
        Assert.Equal("same", group.SelectedValue);
        RadioGroupItem replacement = new() { Value = "same" };
        group.Children.Add(replacement);
        Assert.Equal(true, replacement.IsChecked);
        Assert.Equal(0, changes);
        lastRow.Dispose();
    }

    public static void VerifyReentrantSelectionEvents()
    {
        using RadioGroup group = new() { SelectedValue = "first" };
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem second = new() { Value = "second" };
        RadioGroupItem third = new() { Value = "third" };
        group.Children.Add(first);
        group.Children.Add(second);
        group.Children.Add(third);
        List<object?> changes = [];
        group.SelectionChanged += (_, args) => changes.Add(args.AddedItems.FirstOrDefault());
        second.Checked += (_, _) => group.SelectedValue = "third";
        group.SelectedValue = "second";
        Assert.Equal(2, changes.Count);
        Assert.Equal("second", changes[0]);
        Assert.Equal("third", changes[1]);
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(false, second.IsChecked);
        Assert.Equal(true, third.IsChecked);
        group.SelectionChanged += (_, args) =>
        {
            if (Equals(args.AddedItems.FirstOrDefault(), "first"))
            {
                group.SelectedValue = null;
            }
        };
        group.SelectedValue = "first";
        Assert.Equal(4, changes.Count);
        Assert.Equal("first", changes[2]);
        Assert.Null(changes[3]);
        Assert.Null(group.SelectedValue);
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(false, third.IsChecked);
    }

    public static void VerifyResetBaselineAndUnsubscription()
    {
        using Form form = new();
        FormSampleViewModel model = new() { Delivery = "express" };
        RadioGroup group = new() { DataContext = model, Required = true };
        group.SetBinding(RadioGroup.SelectedValueProperty,
            CompiledBinding.Create<FormSampleViewModel, string?>(nameof(FormSampleViewModel.Delivery),
                static source => source.Delivery, static (source, value) => source.Delivery = value, BindingMode.TwoWay));
        form.Children.Add(group);
        RadioGroupItem standard = new() { Value = "standard" };
        RadioGroupItem express = new() { Value = "express" };
        group.Children.Add(standard);
        group.Children.Add(express);
        Assert.Equal(true, express.IsChecked);
        model.Delivery = null;
        Assert.False(group.ReportValidity());
        Assert.True(group.States.HasFlag(ElementStates.UserInvalid));
        Assert.True(Validation.GetHasError(group));
        form.ResetForm();
        Assert.Equal("express", model.Delivery);
        Assert.Equal(true, express.IsChecked);
        Assert.False(group.States.HasFlag(ElementStates.UserInvalid));
        Assert.True(form.IsValid);
        group.DataContext = new FormSampleViewModel { Delivery = "standard" };
        group.SelectedValue = "express";
        group.Reset();
        Assert.Equal("standard", group.SelectedValue);
        Assert.Equal(true, standard.IsChecked);
        (RadioGroupItem item, WeakReference<RadioGroup> reference) = CreateDetachedItem();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.TryGetTarget(out _));
        GC.KeepAlive(item);
        item.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (RadioGroupItem Item, WeakReference<RadioGroup> Group) CreateDetachedItem()
    {
        RadioGroup group = new() { SelectedValue = "item" };
        RadioGroupItem item = new() { Value = "item" };
        group.Children.Add(item);
        group.Children.Remove(item);
        return (item, new WeakReference<RadioGroup>(group));
    }

    public static void VerifySeparateTreeNotifications()
    {
        using StackPanel root = new();
        using View wrapper = new();
        TreeProbe probe = new();
        wrapper.Children.Add(probe);
        root.Children.Add(wrapper);
        int ancestry = probe.AncestryChanges;
        wrapper.IsVisible = false;
        Assert.Equal(ancestry, probe.AncestryChanges);
        Assert.Equal(1, probe.VisibilityChanges);
        wrapper.IsVisible = true;
        Assert.Equal(ancestry, probe.AncestryChanges);
        Assert.Equal(2, probe.VisibilityChanges);
        root.Children.Remove(wrapper);
        Assert.Equal(ancestry + 1, probe.AncestryChanges);
        Assert.Equal(2, probe.VisibilityChanges);
    }

    private sealed class TreeProbe : UIElement
    {
        public override string TagName => "probe";
        public int AncestryChanges { get; private set; }
        public int VisibilityChanges { get; private set; }

        internal override void OnAncestorChanged()
        {
            base.OnAncestorChanged();
            AncestryChanges++;
        }

        internal override void OnVisibilityChanged()
        {
            base.OnVisibilityChanged();
            VisibilityChanges++;
        }
    }

    public static void VerifyDisposalAndFocusCallbacks()
    {
        using StackPanel root = new();
        RadioGroup firstGroup = new();
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem disposed = new() { Value = "disposed" };
        firstGroup.Children.Add(first);
        firstGroup.Children.Add(disposed);
        first.Checked += (_, _) => disposed.Dispose();
        firstGroup.SelectedValue = "first";
        Assert.True(disposed.IsDisposed);
        Assert.Null(disposed.Group);
        Assert.Equal(true, first.IsChecked);
        firstGroup.Children.Remove(disposed);
        RadioGroup secondGroup = new() { SelectedValue = "origin" };
        RadioGroupItem origin = new() { Value = "origin" };
        RadioGroupItem destination = new() { Value = "destination" };
        secondGroup.Children.Add(origin);
        secondGroup.Children.Add(destination);
        root.Children.Add(firstGroup);
        root.Children.Add(secondGroup);
        using Window host = new() { Content = root };
        destination.GotFocus += (_, _) =>
        {
            secondGroup.Children.Remove(destination);
            firstGroup.Children.Add(destination);
        };
        origin.Focus(FocusState.Keyboard);
        KeyEventArgs key = new(Key.ArrowDown);
        origin.RaiseEvent(UIElement.KeyDownEvent, key);
        Assert.Equal("origin", secondGroup.SelectedValue);
        Assert.Equal("first", firstGroup.SelectedValue);
        Assert.Same(firstGroup, destination.Group!);
        Assert.False(key.Handled);
    }

    public static void VerifyDisconnectedRegistration()
    {
        using RadioGroup group = new() { SelectedValue = "first", Required = true };
        using StackPanel removedRow = new();
        RadioGroupItem removed = new() { Value = "first" };
        removedRow.Children.Add(removed);
        group.Children.Add(removedRow);
        RadioGroupItem existing = new() { Value = "existing" };
        group.Children.Add(existing);
        removedRow.ParentChanged += (_, _) =>
        {
            if (removedRow.Parent is null)
            {
                group.Children.Insert(0, new RadioGroupItem { Value = "inserted" });
            }
        };
        group.Children.Remove(removedRow);
        Assert.Null(removed.Group);
        Assert.Equal("first", group.SelectedValue);
        Assert.False(group.CheckValidity());
        RadioGroupItem replacement = new() { Value = "first" };
        group.Children.Add(replacement);
        Assert.Equal(true, replacement.IsChecked);
        Assert.True(group.CheckValidity());
    }

    public static void VerifyQueuedNotificationsAfterDisposal()
    {
        using RadioGroup group = new() { SelectedValue = "first", Required = true };
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem second = new() { Value = "second" };
        RadioGroupItem third = new() { Value = "third" };
        group.Children.Add(first);
        group.Children.Add(second);
        group.Children.Add(third);
        second.Checked += (_, _) => group.SelectedValue = "third";
        List<object?> changes = [];
        group.SelectionChanged += (_, args) =>
        {
            object? value = args.AddedItems.FirstOrDefault();
            changes.Add(value);
            if (Equals(value, "second"))
            {
                third.Dispose();
            }
        };
        group.SelectedValue = "second";
        Assert.Equal(2, changes.Count);
        Assert.Equal("second", changes[0]);
        Assert.Equal("third", changes[1]);
        Assert.True(third.IsDisposed);
        Assert.Equal("third", group.SelectedValue);
        Assert.False(group.CheckValidity());
    }

    public static void VerifyRootChanges()
    {
        using RadioGroup firstRoot = new() { SelectedValue = "first" };
        using RadioGroup secondRoot = new() { SelectedValue = "second" };
        RadioGroupItem first = new() { Value = "first" };
        RadioGroupItem second = new() { Value = "second" };
        firstRoot.Children.Add(first);
        secondRoot.Children.Add(second);
        using Window firstHost = new() { Content = firstRoot };
        using Window secondHost = new() { Content = secondRoot };
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(true, second.IsChecked);
        firstRoot.Children.Remove(first);
        secondRoot.Children.Add(first);
        Assert.Equal(false, first.IsChecked);
        Assert.Equal(true, second.IsChecked);
        first.PerformClick();
        Assert.Equal(true, first.IsChecked);
        Assert.Equal(false, second.IsChecked);
        secondRoot.Children.Remove(first);
        first.Dispose();
        second.PerformClick();
        Assert.Equal(true, second.IsChecked);
    }
}
