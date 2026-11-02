using NativeUI.Core;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class AnimationClockLifecycleTests
{
    public static void VerifyRootActivationNotifications()
    {
        using View root = new();
        using IDisposable attachment = AnimationClock.AttachRoot(root);
        FrameTarget first = new(root);
        FrameTarget second = new(root);
        int requests = 0;
        Action<UIElement> requested = element =>
        {
            Assert.Same(root, element);
            requests++;
        };
        AnimationClock.FrameRequested += requested;
        try
        {
            AnimationClock.Add(first);
            AnimationClock.Add(second);
            Assert.Equal(1, requests);
            root.IsVisible = false;
            root.IsVisible = true;
            Assert.Equal(2, requests);
            AnimationClock.Remove(first);
            AnimationClock.Add(first);
            Assert.Equal(2, requests);
        }
        finally
        {
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Remove(first);
            AnimationClock.Remove(second);
        }
    }

    public static void VerifyRegistrationNotificationFailure()
    {
        using View root = new();
        using IDisposable attachment = AnimationClock.AttachRoot(root);
        FrameTarget target = new(root);
        Action<UIElement> requested = _ => throw new InvalidOperationException("Cannot request a frame.");
        AnimationClock.FrameRequested += requested;
        try
        {
            ExpectFailure(() => AnimationClock.Add(target));
            Assert.False(AnimationClock.HasActiveAnimations(root));
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Add(target);
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
        }
        finally
        {
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyRollbackPreservesReplacement()
    {
        using View root = new();
        using IDisposable attachment = AnimationClock.AttachRoot(root);
        FrameTarget target = new(root);
        bool replace = true;
        Action<UIElement> requested = _ =>
        {
            if (replace)
            {
                replace = false;
                AnimationClock.Remove(target);
                AnimationClock.Add(target);
                throw new InvalidOperationException("Original registration failed.");
            }
        };
        AnimationClock.FrameRequested += requested;
        try
        {
            ExpectFailure(() => AnimationClock.Add(target));
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
            AnimationClock.Remove(target);
            Assert.False(AnimationClock.HasActiveAnimations(root));
        }
        finally
        {
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyObservationFailure()
    {
        using ThrowingView root = new();
        using IDisposable attachment = AnimationClock.AttachRoot(root);
        FrameTarget target = new(root);
        try
        {
            root.ThrowOnVisibilityRead = true;
            ExpectFailure(() => AnimationClock.Add(target));
            root.ThrowOnVisibilityRead = false;
            Assert.False(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Add(target);
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
        }
        finally
        {
            root.ThrowOnVisibilityRead = false;
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyFailedAttachmentRestoresPreviousRoot()
    {
        using View root = new();
        View dialog = new();
        root.Children.Add(dialog);
        using IDisposable attachment = AnimationClock.AttachRoot(root);
        FrameTarget target = new(dialog);
        AnimationClock.Add(target);
        Action<UIElement> requested = element =>
        {
            if (ReferenceEquals(element, dialog))
            {
                throw new InvalidOperationException("Dialog scheduling failed.");
            }
        };
        AnimationClock.FrameRequested += requested;
        try
        {
            ExpectFailure(() => AnimationClock.AttachRoot(dialog));
            Assert.False(AnimationClock.HasActiveAnimations(dialog));
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
        }
        finally
        {
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyFailedDetachNotificationKeepsCountsConsistent()
    {
        using View root = new();
        View dialog = new();
        root.Children.Add(dialog);
        using IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        using IDisposable dialogAttachment = AnimationClock.AttachRoot(dialog);
        FrameTarget target = new(dialog);
        AnimationClock.Add(target);
        Action<UIElement> requested = _ => throw new InvalidOperationException("Frame notification failed.");
        AnimationClock.FrameRequested += requested;
        try
        {
            ExpectFailure(dialogAttachment.Dispose);
            Assert.False(AnimationClock.HasActiveAnimations(dialog));
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.FrameRequested -= requested;
            dialogAttachment.Dispose();
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
        }
        finally
        {
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyFailedAttachmentPreservesReentrantLifetime()
    {
        using View root = new();
        FrameTarget target = new(root);
        AnimationClock.Add(target);
        IDisposable? retained = null;
        Action<UIElement> requested = _ =>
        {
            retained = AnimationClock.AttachRoot(root);
            throw new InvalidOperationException("First attachment failed.");
        };
        AnimationClock.FrameRequested += requested;
        try
        {
            ExpectFailure(() => AnimationClock.AttachRoot(root));
            Assert.NotNull(retained);
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
            retained?.Dispose();
            Assert.False(AnimationClock.HasActiveAnimations(root));
        }
        finally
        {
            AnimationClock.FrameRequested -= requested;
            AnimationClock.Remove(target);
            retained?.Dispose();
        }
    }

    public static void VerifyAttachmentLifetime()
    {
        using View root = new();
        using IDisposable first = AnimationClock.AttachRoot(root);
        using IDisposable second = AnimationClock.AttachRoot(root);
        FrameTarget target = new(root);
        AnimationClock.Add(target);
        try
        {
            first.Dispose();
            first.Dispose();
            Assert.True(AnimationClock.HasActiveAnimations(root));
            second.Dispose();
            Assert.False(AnimationClock.HasActiveAnimations(root));
            using IDisposable replacement = AnimationClock.AttachRoot(root);
            first.Dispose();
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);
        }
        finally
        {
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyNestedTicksAcrossRoots()
    {
        using View root = new();
        View dialog = new();
        root.Children.Add(dialog);
        using IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        using IDisposable dialogAttachment = AnimationClock.AttachRoot(dialog);
        FrameTarget nested = new(dialog);
        FrameTarget outer = new(root, () =>
        {
            AnimationClock.Tick(root, 999);
            AnimationClock.Tick(dialog, 20);
        });
        AnimationClock.Add(outer);
        AnimationClock.Add(nested);
        try
        {
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, outer.TickCount);
            Assert.Equal(1, nested.TickCount);
            Assert.Equal(20.0, nested.LastTime);
        }
        finally
        {
            AnimationClock.Remove(outer);
            AnimationClock.Remove(nested);
        }
    }

    public static void VerifyDispatcherAccess()
    {
        IDispatcher previousDispatcher = Application.Current.Dispatcher;
        TestDispatcher dispatcher = new();
        using View root = new();
        using IDisposable attachment = AnimationClock.AttachRoot(root);
        FrameTarget target = new(root);
        AnimationClock.Add(target);
        Application.Current.Dispatcher = dispatcher;
        try
        {
            Action[] operations =
            [
                () => AnimationClock.HasActiveAnimations(root),
                () => AnimationClock.Tick(root, 10),
                () => AnimationClock.Add(target),
                () => AnimationClock.Remove(target),
                attachment.Dispose,
                AnimationClock.AttachDriver,
                () => AnimationClock.IsMotionReduced = true
            ];
            dispatcher.HasAccess = false;
            foreach (Action operation in operations)
            {
                ExpectFailure(operation);
            }

            dispatcher.HasAccess = true;
            Exception? failure = null;
            Thread worker = new(() =>
            {
                try
                {
                    AnimationClock.Tick(root, 20);
                }
                catch (Exception error)
                {
                    failure = error;
                }
            });
            worker.Start();
            worker.Join();
            Assert.Null(failure);
            Assert.True(AnimationClock.HasActiveAnimations(root));
            Assert.Equal(1, target.TickCount);
        }
        finally
        {
            Application.Current.Dispatcher = previousDispatcher;
            AnimationClock.Remove(target);
        }
    }

    public static void VerifyDriverMotionPolicyAndBalancedRemoval()
    {
        bool originalMotion = AnimationClock.IsMotionReduced;
        int attached = 0;
        try
        {
            AnimationClock.IsMotionReduced = true;
            AnimationClock.AttachDriver();
            attached++;
            AnimationClock.AttachDriver();
            attached++;
            Assert.False(AnimationClock.IsEnabled);
            AnimationClock.DetachDriver();
            attached--;
            Assert.False(AnimationClock.IsEnabled);
            AnimationClock.IsMotionReduced = false;
            Assert.True(AnimationClock.IsEnabled);
            AnimationClock.DetachDriver();
            attached--;
            ExpectFailure(AnimationClock.DetachDriver);
            Assert.False(AnimationClock.IsEnabled);
            AnimationClock.AttachDriver();
            attached++;
            Assert.True(AnimationClock.IsEnabled);
        }
        finally
        {
            while (attached-- > 0)
            {
                AnimationClock.DetachDriver();
            }

            AnimationClock.IsMotionReduced = originalMotion;
        }
    }

    private static void ExpectFailure(Action action)
    {
        bool failed = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }

        Assert.True(failed, "The operation should have rejected the failure path.");
    }

    private sealed class TestDispatcher : IDispatcher
    {
        public bool HasAccess { get; set; } = true;

        public bool CheckAccess() => HasAccess;

        public void Invoke(Action action) => action();
    }

    private sealed class FrameTarget(UIElement target, Action? tick = null) : IFrameTarget
    {
        public UIElement Target => target;

        public int TickCount { get; private set; }

        public double LastTime { get; private set; }

        public void Tick(double now)
        {
            TickCount++;
            LastTime = now;
            tick?.Invoke();
        }
    }

    private sealed class ThrowingView : View
    {
        public bool ThrowOnVisibilityRead { get; set; }

        public override bool IsVisible
        {
            get => ThrowOnVisibilityRead ? throw new InvalidOperationException("Cannot read visibility.") : base.IsVisible;
            set => base.IsVisible = value;
        }
    }
}
