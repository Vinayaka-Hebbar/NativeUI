namespace NativeUI.Core;

/// <summary>
/// Represents an active binding attached to a <see cref="BindableObject"/> that reacts to <c>DataContext</c> changes.
/// </summary>
public interface IBindingSubscription : IDisposable
{
    /// <summary>
    /// Attaches or updates the binding against the specified <paramref name="dataContext"/>.
    /// </summary>
    /// <param name="dataContext">The active data context object, or <see langword="null"/> when detached.</param>
    void OnDataContextChanged(object? dataContext);
}
