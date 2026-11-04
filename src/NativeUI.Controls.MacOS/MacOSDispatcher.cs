using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using NativeUI.Core;

namespace NativeUI.Controls.MacOS;

/// <summary>Marshals UI work to the AppKit main queue.</summary>
public sealed unsafe partial class MacOSDispatcher : IDispatcher
{
    private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
    private static readonly nint MainQueue = NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/system/libdispatch.dylib"), "_dispatch_main_q");

    /// <summary>Gets the shared AppKit main-queue dispatcher.</summary>
    public static MacOSDispatcher Shared { get; } = new();

    /// <inheritdoc />
    public bool CheckAccess() => pthread_main_np() != 0;

    /// <inheritdoc />
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CheckAccess())
        {
            action();
            return;
        }
        Work work = new(action);
        GCHandle handle = GCHandle.Alloc(work);
        try
        {
            dispatch_sync_f(MainQueue, GCHandle.ToIntPtr(handle), &Execute);
            work.Failure?.Throw();
        }
        finally { handle.Free(); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Execute(nint context)
    {
        if (GCHandle.FromIntPtr(context).Target is not Work work) return;
        try { work.Action(); }
        catch (Exception exception) { work.Failure = ExceptionDispatchInfo.Capture(exception); }
    }

    private sealed class Work(Action action)
    {
        public Action Action { get; } = action;
        public ExceptionDispatchInfo? Failure { get; set; }
    }

    [LibraryImport(SystemLibrary)]
    private static partial int pthread_main_np();

    [LibraryImport(SystemLibrary)]
    private static partial void dispatch_sync_f(nint queue, nint context, delegate* unmanaged[Cdecl]<nint, void> callback);
}
