using System.Runtime.CompilerServices;
using NativeUI.Media.Animation;
using NativeUI.Tests;

namespace NativeUI.Core.Tests;

internal static class AnimationTests
{
    public static void VerifyCancellationAndCompletion()
    {
        Animation animation = new();
        Assert.Equal(PlaybackState.Active, animation.State);
        int cancellations = 0;
        animation.SetCancelHandler(() =>
        {
            cancellations++;
            Assert.Equal(PlaybackState.Cancelling, animation.State);
            animation.Cancel();
            Assert.False(animation.Finished.IsCompleted);
        });
        animation.Cancel();
        animation.Cancel();
        Assert.Equal(1, cancellations);
        Assert.True(animation.Finished.IsCompletedSuccessfully);
        Assert.Equal(PlaybackState.Completed, animation.State);

        Animation completed = new();
        completed.Complete();
        Assert.Equal(PlaybackState.Completed, completed.State);
        completed.SetCancelHandler(() => cancellations++);
        completed.Cancel();
        Assert.Equal(1, cancellations);

        Animation failing = new();
        failing.SetCancelHandler(() => throw new InvalidOperationException("Cancellation failed."));
        try
        {
            failing.Cancel();
        }
        catch (InvalidOperationException)
        {
        }

        Assert.True(failing.Finished.IsCompletedSuccessfully);
        Assert.Equal(PlaybackState.Completed, failing.State);
    }

    public static void VerifyCompletedAnimationReleasesCallback()
    {
        Animation animation = CreateCompletedAnimation(out WeakReference callbackTarget);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(callbackTarget.IsAlive);
        GC.KeepAlive(animation);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Animation CreateCompletedAnimation(out WeakReference reference)
    {
        object target = new();
        reference = new WeakReference(target);
        Animation animation = new();
        animation.SetCancelHandler(() => GC.KeepAlive(target));
        animation.Complete();
        return animation;
    }
}
