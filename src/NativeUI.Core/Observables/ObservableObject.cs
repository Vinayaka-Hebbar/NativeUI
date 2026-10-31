using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NativeUI.Core;

/// <summary>
/// Provides a base implementation of <see cref="INotifyPropertyChanged"/> for ViewModels and observable models.
/// Compatible with standard .NET data binding and CommunityToolkit.Mvvm conventions.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Compares the current backing field with <paramref name="newValue"/>, updates the field if different,
    /// and raises <see cref="PropertyChanged"/>.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="newValue">The new value to assign.</param>
    /// <param name="propertyName">The name of the changed property, automatically supplied by the compiler.</param>
    /// <returns><see langword="true"/> if the property value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T newValue,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }

        field = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the specified <paramref name="propertyName"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
