using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class AnimationTests
{
    public static void VerifyVisibleTreeTargets()
    {
        using View root = new();
        View ancestor = new();
        Text child = new();
        root.Children.Add(ancestor);
        ancestor.Children.Add(child);
        FrameTarget target = new(child);
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(target);
        try
        {
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);

            ancestor.IsVisible = false;
            Assert.False(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 20);
            Assert.Equal(1, target.TickCount);

            ancestor.IsVisible = true;
            AnimationClock.Tick(root, 30);
            Assert.Equal(2, target.TickCount);
            Assert.Equal(30.0, target.LastTime);

            ancestor.Children.Remove(child);
            Assert.False(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 40);
            Assert.Equal(2, target.TickCount);

            root.Children.Add(child);
            AnimationClock.Tick(root, 50);
            Assert.Equal(3, target.TickCount);
            root.IsVisible = false;
            Assert.False(AnimationClock.HasActiveAnimations(root));
        }
        finally
        {
            AnimationClock.Remove(target);
            rootAttachment.Dispose();
        }
    }

    public static void VerifyRegistrationChangesDuringTick()
    {
        using View root = new();
        FrameTarget second = new(root);
        FrameTarget added = new(root);
        FrameTarget first = new(root, _ =>
        {
            AnimationClock.Remove(second);
            AnimationClock.Add(second);
            AnimationClock.Add(added);
            AnimationClock.Tick(root, 999);
        });
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(first);
        AnimationClock.Add(first);
        AnimationClock.Add(second);
        try
        {
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, first.TickCount);
            Assert.Equal(0, second.TickCount);
            Assert.Equal(0, added.TickCount);

            AnimationClock.Remove(first);
            AnimationClock.Tick(root, 20);
            Assert.Equal(1, second.TickCount);
            Assert.Equal(1, added.TickCount);
            Assert.Equal(20.0, second.LastTime);
            Assert.Equal(second.LastTime, added.LastTime);
        }
        finally
        {
            AnimationClock.Remove(first);
            AnimationClock.Remove(second);
            AnimationClock.Remove(added);
            rootAttachment.Dispose();
        }
    }

    public static void VerifyTickRecoversAfterException()
    {
        using View root = new();
        FrameTarget following = new(root);
        FrameTarget failing = new(root, _ =>
        {
            AnimationClock.Remove(following);
            throw new InvalidOperationException("Frame callback failed.");
        });
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(failing);
        AnimationClock.Add(following);
        try
        {
            bool threw = false;
            try
            {
                AnimationClock.Tick(root, 10);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw);
            AnimationClock.Remove(failing);
            AnimationClock.Add(following);
            AnimationClock.Tick(root, 20);
            Assert.Equal(1, following.TickCount);
        }
        finally
        {
            AnimationClock.Remove(failing);
            AnimationClock.Remove(following);
            rootAttachment.Dispose();
        }
    }

    public static void VerifyClockDoesNotAllocatePerTick()
    {
        using View root = new();
        FrameTarget target = new(root);
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(target);
        try
        {
            for (int i = 0; i < 100; i++)
            {
                AnimationClock.Tick(root, i);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                AnimationClock.Tick(root, i);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated);
        }
        finally
        {
            AnimationClock.Remove(target);
            rootAttachment.Dispose();
        }
    }

    public static void VerifyAncestorChangesUpdateCachedMembership()
    {
        using View root = new();
        View visibleParent = new();
        View hiddenParent = new() { IsVisible = false };
        View branch = new();
        Text child = new();
        root.Children.Add(visibleParent);
        root.Children.Add(hiddenParent);
        visibleParent.Children.Add(branch);
        branch.Children.Add(child);
        FrameTarget target = new(child);
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(target);
        try
        {
            Assert.True(AnimationClock.HasActiveAnimations(root));
            visibleParent.Children.Remove(branch);
            hiddenParent.Children.Add(branch);
            Assert.False(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 10);
            Assert.Equal(0, target.TickCount);

            visibleParent.IsVisible = false;
            hiddenParent.IsVisible = true;
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 20);
            Assert.Equal(1, target.TickCount);

            AnimationClock.Remove(target);
            hiddenParent.IsVisible = false;
            hiddenParent.IsVisible = true;
            Assert.False(AnimationClock.HasActiveAnimations(root));
        }
        finally
        {
            AnimationClock.Remove(target);
            rootAttachment.Dispose();
        }
    }

    public static void VerifyHostedDialogRootOwnership()
    {
        using View root = new();
        View dialog = new();
        Text child = new();
        root.Children.Add(dialog);
        dialog.Children.Add(child);
        FrameTarget target = new(child);
        AnimationClock.Add(target);
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        IDisposable dialogAttachment = AnimationClock.AttachRoot(dialog);
        try
        {
            Assert.False(AnimationClock.HasActiveAnimations(root));
            Assert.True(AnimationClock.HasActiveAnimations(dialog));
            root.IsVisible = false;
            AnimationClock.Tick(root, 10);
            AnimationClock.Tick(dialog, 10);
            Assert.Equal(1, target.TickCount);

            dialogAttachment.Dispose();
            Assert.False(AnimationClock.HasActiveAnimations(dialog));
            Assert.False(AnimationClock.HasActiveAnimations(root));
            root.IsVisible = true;
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 20);
            Assert.Equal(2, target.TickCount);

            rootAttachment.Dispose();
            Assert.False(AnimationClock.HasActiveAnimations(root));
            rootAttachment = AnimationClock.AttachRoot(root);
            Assert.True(AnimationClock.HasActiveAnimations(root));
        }
        finally
        {
            AnimationClock.Remove(target);
            dialogAttachment.Dispose();
            rootAttachment.Dispose();
        }
    }

    public static void VerifyFramesDoNotReadAncestorVisibility()
    {
        using VisibilityTrackingView root = new();
        View branch = new();
        Text child = new();
        root.Children.Add(branch);
        branch.Children.Add(child);
        FrameTarget target = new(child);
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(target);
        try
        {
            int reads = root.VisibilityReads;
            for (int i = 0; i < 100; i++)
            {
                Assert.True(AnimationClock.HasActiveAnimations(root));
                AnimationClock.Tick(root, i);
            }

            Assert.Equal(reads, root.VisibilityReads);
            Assert.Equal(100, target.TickCount);
            root.IsVisible = false;
            Assert.False(AnimationClock.HasActiveAnimations(root));
        }
        finally
        {
            AnimationClock.Remove(target);
            rootAttachment.Dispose();
        }
    }

    public static void VerifyPortalVisibilityUpdatesMembership()
    {
        using View root = new();
        Portal portal = new();
        Text child = new();
        root.Children.Add(portal);
        portal.Children.Add(child);
        FrameTarget target = new(child);
        IDisposable rootAttachment = AnimationClock.AttachRoot(root);
        AnimationClock.Add(target);
        try
        {
            Assert.False(AnimationClock.HasActiveAnimations(root));
            portal.Show();
            Assert.True(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 10);
            Assert.Equal(1, target.TickCount);

            portal.Close();
            Assert.False(AnimationClock.HasActiveAnimations(root));
            AnimationClock.Tick(root, 20);
            Assert.Equal(1, target.TickCount);

            portal.Show();
            AnimationClock.Tick(root, 30);
            Assert.Equal(2, target.TickCount);
        }
        finally
        {
            AnimationClock.Remove(target);
            rootAttachment.Dispose();
            portal.Close();
        }
    }


    private sealed class FrameTarget(UIElement target, Action<double>? tick = null) : IFrameTarget
    {
        public UIElement Target => target;

        public int TickCount { get; private set; }

        public double LastTime { get; private set; }

        public void Tick(double now)
        {
            TickCount++;
            LastTime = now;
            tick?.Invoke(now);
        }
    }

    private sealed class VisibilityTrackingView : View
    {
        public int VisibilityReads { get; private set; }

        public override bool IsVisible
        {
            get
            {
                VisibilityReads++;
                return base.IsVisible;
            }
            set => base.IsVisible = value;
        }
    }
}
