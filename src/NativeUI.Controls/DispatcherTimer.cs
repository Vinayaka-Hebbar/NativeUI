using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// A timer whose <see cref="Tick"/> event is raised on the application's <see cref="IDispatcher"/> at a recurring interval.
/// </summary>
public sealed class DispatcherTimer
{
    private TimeSpan _interval;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Gets or sets the time between ticks.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            if (_interval == value)
            {
                return;
            }

            _interval = value;
            if (IsEnabled)
            {
                Start();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the timer is running.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    public event EventHandler? Tick;

    /// <summary>
    /// Starts the timer. Calling <see cref="Start"/> while the timer is already running resets the current interval.
    /// </summary>
    public void Start()
    {
        CancelCurrentRun();
        CancellationTokenSource cancellationTokenSource = new();
        _cancellationTokenSource = cancellationTokenSource;
        IsEnabled = true;
        _ = RunAsync(cancellationTokenSource.Token);
    }

    /// <summary>
    /// Stops the timer.
    /// </summary>
    public void Stop()
    {
        IsEnabled = false;
        CancelCurrentRun();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan interval = _interval;
                if (interval > TimeSpan.Zero)
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Yield();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                IDispatcher dispatcher = Application.Current.Dispatcher;
                if (dispatcher.CheckAccess())
                {
                    RaiseTick(cancellationToken);
                }
                else
                {
                    dispatcher.Invoke(() => RaiseTick(cancellationToken));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void RaiseTick(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || !IsEnabled)
        {
            return;
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void CancelCurrentRun()
    {
        CancellationTokenSource? source = _cancellationTokenSource;
        _cancellationTokenSource = null;
        if (source is null)
        {
            return;
        }

        source.Cancel();
        source.Dispose();
    }
}
