using System.Windows.Input;

namespace NativeUI.Core;

/// <summary>
/// Extends a command with observable asynchronous completion without requiring a particular MVVM library.
/// </summary>
public interface IAsyncCommand : ICommand
{
    /// <summary>Executes the command and returns its completion, including failures.</summary>
    Task ExecuteAsync(object? parameter);
}
