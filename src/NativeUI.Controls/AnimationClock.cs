using System.Diagnostics;
using System.Runtime.ExceptionServices;
using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Advances targets on their host's screen refresh. Access is governed by the application dispatcher.
/// Reduced motion is an application-wide policy; browser CSS animations remain browser-owned.
/// </summary>
internal static class AnimationClock
{
    private static readonly Dictionary<IFrameTarget, Registration> s_registrations = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<UIElement, RootState> s_roots = new(ReferenceEqualityComparer.Instance);
    private static int s_driverCount;
    private static bool s_isMotionReduced;

    /// <summary>
    /// Occurs after a root changes from idle to active. Exceptions propagate; failed registration and attachment
    /// release their own changes. Hosts receive the root that needs screen refreshes.
    /// </summary>
    public static event Action<UIElement>? FrameRequested;

    /// <summary>
    /// Gets whether desktop style animations are enabled by driver availability and application motion policy.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            VerifyAccess();
            return s_driverCount > 0 && !s_isMotionReduced;
        }
    }

    /// <summary>
    /// Gets or sets the application-wide reduced-motion preference supplied by the platform application.
    /// </summary>
    public static bool IsMotionReduced
    {
        get
        {
            VerifyAccess();
            return s_isMotionReduced;
        }
        set
        {
            VerifyAccess();
            s_isMotionReduced = value;
        }
    }

    /// <summary>
    /// Gets milliseconds on the clock's monotonic timeline. Reading time is thread-safe.
    /// </summary>
    public static double Now => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// Gets whether this presentation has visible targets needing further ticks.
    /// </summary>
    public static bool HasActiveAnimations(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        VerifyAccess();
        return s_roots.TryGetValue(root, out RootState? state) && state.ActiveCount > 0;
    }

    /// <summary>
    /// Attaches a host's content root. Dispose the returned lifetime on the UI thread when presentation ends.
    /// Failed attachment releases only its own lifetime, preserving any reentrant attachment of the same root.
    /// </summary>
    public static IDisposable AttachRoot(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        VerifyAccess();
        if (s_roots.TryGetValue(root, out RootState? existing))
        {
            existing.AttachmentCount++;
            return new RootAttachment(existing);
        }

        RootState state = new(root);
        s_roots.Add(root, state);
        try
        {
            RefreshRoots();
            return new RootAttachment(state);
        }
        catch (Exception attachmentError)
        {
            try
            {
                ReleaseRoot(state);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Root attachment and rollback failed.", attachmentError, cleanupError);
            }

            throw;
        }
    }

    /// <summary>
    /// Registers a desktop frame driver without changing the application motion policy.
    /// </summary>
    public static void AttachDriver()
    {
        VerifyAccess();
        s_driverCount++;
    }

    /// <summary>
    /// Removes a registered driver. Unbalanced removal is an error.
    /// </summary>
    public static void DetachDriver()
    {
        VerifyAccess();
        if (s_driverCount == 0)
        {
            throw new InvalidOperationException("No animation frame driver is attached.");
        }

        s_driverCount--;
    }

    /// <summary>
    /// Advances this root's visible targets to the current time.
    /// </summary>
    public static void Tick(UIElement root)
    {
        Tick(root, Now);
    }

    /// <summary>
    /// Advances targets using milliseconds on this clock's timeline. New activations wait for the next tick.
    /// Nested ticks of the same root are ignored; a different root can advance independently.
    /// </summary>
    public static void Tick(UIElement root, double now)
    {
        ArgumentNullException.ThrowIfNull(root);
        VerifyAccess();
        if (s_roots.TryGetValue(root, out RootState? state))
        {
            state.Tick(now);
        }
    }

    /// <summary>
    /// Registers a target and observes its tree. Failed observation releases this registration and its subscriptions.
    /// </summary>
    public static void Add(IFrameTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        VerifyAccess();
        if (s_registrations.ContainsKey(target))
        {
            return;
        }

        Registration registration = new(target);
        s_registrations.Add(target, registration);
        try
        {
            registration.Observe();
        }
        catch
        {
            Remove(registration);
            throw;
        }
    }

    /// <summary>
    /// Unregisters a target and releases its ancestor subscriptions.
    /// </summary>
    public static void Remove(IFrameTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        VerifyAccess();
        if (s_registrations.Remove(target, out Registration? registration))
        {
            registration.Dispose();
        }
    }

    private static void Remove(Registration registration)
    {
        if (s_registrations.TryGetValue(registration.Target, out Registration? current) &&
            ReferenceEquals(current, registration))
        {
            s_registrations.Remove(registration.Target);
        }

        registration.Dispose();
    }

    private static void ReleaseRoot(RootState state)
    {
        VerifyAccess();
        if (!s_roots.TryGetValue(state.Element, out RootState? current) || !ReferenceEquals(current, state))
        {
            return;
        }

        if (--state.AttachmentCount == 0)
        {
            s_roots.Remove(state.Element);
            // Registrations retain the old state while deactivation updates its active count.
            RefreshRoots();
        }
    }

    private static void RefreshRoots()
    {
        List<Exception>? failures = null;
        // Host changes are infrequent. Callbacks can replace registrations during refresh.
        foreach (Registration registration in s_registrations.Values.ToArray())
        {
            try
            {
                registration.UpdateState();
            }
            catch (Exception error)
            {
                (failures ??= []).Add(error);
            }
        }

        if (failures is { Count: 1 })
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures is not null)
        {
            throw new AggregateException("Animation root refresh failed.", failures);
        }
    }

    private static void VerifyAccess()
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("Animation clock operations must run on the UI thread.");
        }
    }

    /// <summary>
    /// Owns one root attachment and makes repeated disposal harmless.
    /// </summary>
    private sealed class RootAttachment(RootState state) : IDisposable
    {
        private bool _disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            VerifyAccess();
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseRoot(state);
        }
    }

    /// <summary>
    /// Owns one presentation's attachments, active targets, and mutation-safe tick state.
    /// </summary>
    private sealed class RootState(UIElement element)
    {
        private readonly List<Registration?> _targets = [];
        private bool _isTicking;
        private bool _hasRemovedTargets;

        /// <summary>Gets the presented control root.</summary>
        public UIElement Element { get; } = element;

        /// <summary>Gets or sets the number of hosts retaining this presentation.</summary>
        public int AttachmentCount { get; set; } = 1;

        /// <summary>Gets the number of active frame targets.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Adds a visible target and notifies the host when the root leaves its idle state.</summary>
        public void Add(Registration registration)
        {
            registration.Slot = _targets.Count;
            _targets.Add(registration);
            if (++ActiveCount == 1)
            {
                FrameRequested?.Invoke(Element);
            }
        }

        /// <summary>Removes a target without shifting targets being visited by callbacks.</summary>
        public void Remove(Registration registration)
        {
            if (registration.Slot < 0)
            {
                return;
            }

            _targets[registration.Slot] = null;
            registration.Slot = -1;
            ActiveCount--;
            _hasRemovedTargets = true;
            if (!_isTicking)
            {
                Compact();
            }
        }

        /// <summary>Advances this root's targets using one timestamp.</summary>
        public void Tick(double now)
        {
            if (_isTicking)
            {
                return;
            }

            _isTicking = true;
            try
            {
                int count = _targets.Count;
                for (int i = 0; i < count; i++)
                {
                    _targets[i]?.Target.Tick(now);
                }
            }
            finally
            {
                _isTicking = false;
                Compact();
            }
        }

        private void Compact()
        {
            if (!_hasRemovedTargets)
            {
                return;
            }

            int count = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] is Registration registration)
                {
                    _targets[count] = registration;
                    registration.Slot = count++;
                }
            }

            _targets.RemoveRange(count, _targets.Count - count);
            _hasRemovedTargets = false;
        }
    }

    /// <summary>
    /// Observes the registered target's ancestor chain and updates active membership only when that chain or visibility changes.
    /// </summary>
    private sealed class Registration(IFrameTarget target) : IDisposable
    {
        private readonly List<UIElement> _ancestors = [];
        private bool _disposed;

        /// <summary>
        /// Gets the registered frame recipient.
        /// </summary>
        public IFrameTarget Target { get; } = target;

        /// <summary>
        /// Gets the nearest attached host root, or null when the target is detached.
        /// </summary>
        public RootState? Root { get; private set; }

        /// <summary>
        /// Gets or sets the active-list slot, or -1 while suspended.
        /// </summary>
        public int Slot { get; set; } = -1;

        /// <summary>
        /// Rebinds ancestor notifications after the target's logical tree changes.
        /// </summary>
        public void Observe()
        {
            if (_disposed)
            {
                return;
            }

            Unsubscribe();
            for (UIElement? current = Target.Target; current is not null; current = current.Parent)
            {
                _ancestors.Add(current);
                current.ParentChanged += OnParentChanged;
                current.VisibilityChanged += OnVisibilityChanged;
            }

            UpdateState();
        }

        /// <summary>
        /// Updates cached ownership and visibility after a host or observed ancestor changes.
        /// </summary>
        public void UpdateState()
        {
            if (_disposed)
            {
                return;
            }

            VerifyAccess();
            bool visible = true;
            RootState? root = null;
            foreach (UIElement ancestor in _ancestors)
            {
                if (!ancestor.IsVisible)
                {
                    visible = false;
                }

                if (s_roots.TryGetValue(ancestor, out root))
                {
                    break;
                }
            }

            if (!visible || !ReferenceEquals(Root, root))
            {
                Root?.Remove(this);
            }

            Root = root;
            if (visible && root is not null && Slot < 0)
            {
                root.Add(this);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Unsubscribe();
            Root?.Remove(this);
            Root = null;
        }

        private void OnParentChanged(object? sender, EventArgs e)
        {
            VerifyAccess();
            try
            {
                Observe();
            }
            catch
            {
                Remove(this);
                throw;
            }
        }

        private void OnVisibilityChanged(object? sender, EventArgs e)
        {
            VerifyAccess();
            try
            {
                UpdateState();
            }
            catch
            {
                Remove(this);
                throw;
            }
        }

        private void Unsubscribe()
        {
            foreach (UIElement ancestor in _ancestors)
            {
                ancestor.ParentChanged -= OnParentChanged;
                ancestor.VisibilityChanged -= OnVisibilityChanged;
            }

            _ancestors.Clear();
        }
    }
}
