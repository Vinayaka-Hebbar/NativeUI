using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Provides Objective-C runtime and macOS AppKit / CoreFoundation P/Invoke bindings for native window hosting,
/// mouse/keyboard input routing, and pasteboard integration.
/// </summary>
internal static partial class AppKitInterop
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string FoundationFramework = "/System/Library/Frameworks/Foundation.framework/Foundation";
    private const ulong EventModifierFlagShift = 1UL << 17;
    private const ulong EventModifierFlagControl = 1UL << 18;
    private const ulong EventModifierFlagOption = 1UL << 19;
    private const ulong EventModifierFlagCommand = 1UL << 20;
    private const ulong TrackingMouseEnteredAndExited = 0x01;
    private const ulong TrackingMouseMoved = 0x02;
    private const ulong TrackingCursorUpdate = 0x04;
    private const ulong TrackingActiveInKeyWindow = 0x20;
    private const ulong TrackingInVisibleRect = 0x200;
    private const string WindowDidResignKeyNotification = "NSWindowDidResignKeyNotification";
    private const string WindowDidResizeNotification = "NSWindowDidResizeNotification";

    private static readonly ConcurrentDictionary<nint, Window> s_viewToWindow = new();
    private static bool s_frameworksLoaded;
    private static nint s_interactiveViewClass;

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;

        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;

        public CGSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;

        public CGRect(double x, double y, double width, double height)
        {
            Origin = new CGPoint(x, y);
            Size = new CGSize(width, height);
        }
    }

    public static void EnsureFrameworksLoaded()
    {
        if (s_frameworksLoaded || !OperatingSystem.IsMacOS())
        {
            return;
        }

        _ = NativeLibrary.Load(FoundationFramework);
        _ = NativeLibrary.Load(AppKitFramework);
        s_frameworksLoaded = true;
    }

    public static void RegisterWindowView(nint nsView, Window window)
    {
        if (nsView != nint.Zero)
        {
            s_viewToWindow[nsView] = window;
        }
    }

    public static void UnregisterWindowView(nint nsView)
    {
        if (nsView != nint.Zero)
        {
            s_viewToWindow.TryRemove(nsView, out _);
        }
    }

    [LibraryImport(ObjCLib, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetClass(string name);

    [LibraryImport(ObjCLib, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetSelector(string name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_allocateClassPair", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint AllocateClassPair(nint superclass, string name, nuint extraBytes);

    [LibraryImport(ObjCLib, EntryPoint = "objc_registerClassPair")]
    public static partial void RegisterClassPair(nint cls);

    [LibraryImport(ObjCLib, EntryPoint = "class_addMethod", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool ClassAddMethod(nint cls, nint name, nint imp, string types);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1, nint arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1, ulong arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageVoid(nint receiver, nint selector, nint arg1, nint arg2, nint arg3, nint arg4);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageTrackingAreaInit(
        nint receiver,
        nint selector,
        CGRect rect,
        ulong options,
        nint owner,
        nint userInfo);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial CGPoint SendMessageConvertPoint(nint receiver, nint selector, CGPoint point, nint view);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial CGRect SendMessageRect(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend_stret")]
    private static partial CGRect SendMessageRectStret(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial ulong SendMessageUInt64(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial long SendMessageInt64(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial CGPoint SendMessagePoint(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageVoid(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageVoid(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageWindowInit(
        nint receiver,
        nint selector,
        CGRect contentRect,
        ulong styleMask,
        ulong backing,
        [MarshalAs(UnmanagedType.I1)] bool defer);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageViewInit(nint receiver, nint selector, CGRect frameRect);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageImageInitWithData(nint receiver, nint selector, nint nsData);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageSetSize(nint receiver, nint selector, CGSize size);

    public static unsafe nint GetInteractiveImageViewClass()
    {
        if (s_interactiveViewClass != nint.Zero)
        {
            return s_interactiveViewClass;
        }

        EnsureFrameworksLoaded();
        nint baseClass = GetClass("NSImageView");
        nint existing = GetClass("NativeUIInteractiveImageView");
        if (existing != nint.Zero)
        {
            s_interactiveViewClass = existing;
            return existing;
        }

        nint subClass = AllocateClassPair(baseClass, "NativeUIInteractiveImageView", 0);
        if (subClass == nint.Zero)
        {
            return baseClass;
        }

        delegate* unmanaged[Cdecl]<nint, nint, byte> acceptsFirstResponderPtr = &OnAcceptsFirstResponder;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseDownPtr = &OnMouseDown;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseDraggedPtr = &OnMouseDragged;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseUpPtr = &OnMouseUp;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseMovedPtr = &OnMouseMoved;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseExitedPtr = &OnMouseExited;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> cursorUpdatePtr = &OnCursorUpdate;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> windowDidResignKeyPtr = &OnWindowDidResignKey;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> windowDidResizePtr = &OnWindowDidResize;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> keyDownPtr = &OnKeyDown;
        delegate* unmanaged[Cdecl]<nint, nint, nint, byte> performKeyEquivPtr = &OnPerformKeyEquivalent;

        _ = ClassAddMethod(subClass, GetSelector("acceptsFirstResponder"), (nint)acceptsFirstResponderPtr, "c@:");
        _ = ClassAddMethod(subClass, GetSelector("mouseDown:"), (nint)mouseDownPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseDragged:"), (nint)mouseDraggedPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseUp:"), (nint)mouseUpPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseMoved:"), (nint)mouseMovedPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseEntered:"), (nint)mouseMovedPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseExited:"), (nint)mouseExitedPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("cursorUpdate:"), (nint)cursorUpdatePtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("nativeUIWindowDidResignKey:"), (nint)windowDidResignKeyPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("nativeUIWindowDidResize:"), (nint)windowDidResizePtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("keyDown:"), (nint)keyDownPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("performKeyEquivalent:"), (nint)performKeyEquivPtr, "c@:@");

        RegisterClassPair(subClass);
        s_interactiveViewClass = subClass;
        return subClass;
    }

    /// <summary>
    /// Registers the view for pointer movement, entry, exit, and cursor callbacks and for window activation and resize notifications.
    /// </summary>
    public static void InstallPointerTracking(nint nsWindow, nint nsView)
    {
        SendMessageVoid(nsWindow, GetSelector("setAcceptsMouseMovedEvents:"), true);

        ulong options = TrackingMouseEnteredAndExited | TrackingMouseMoved | TrackingCursorUpdate |
            TrackingActiveInKeyWindow | TrackingInVisibleRect;
        nint allocated = SendMessage(GetClass("NSTrackingArea"), GetSelector("alloc"));
        nint trackingArea = SendMessageTrackingAreaInit(
            allocated,
            GetSelector("initWithRect:options:owner:userInfo:"),
            default,
            options,
            nsView,
            nint.Zero);
        SendMessageVoid(nsView, GetSelector("addTrackingArea:"), trackingArea);

        nint center = SendMessage(GetClass("NSNotificationCenter"), GetSelector("defaultCenter"));
        nint addObserver = GetSelector("addObserver:selector:name:object:");
        SendMessageVoid(center, addObserver, nsView, GetSelector("nativeUIWindowDidResignKey:"), CreateNSString(WindowDidResignKeyNotification), nsWindow);
        SendMessageVoid(center, addObserver, nsView, GetSelector("nativeUIWindowDidResize:"), CreateNSString(WindowDidResizeNotification), nsWindow);
    }

    /// <summary>
    /// Removes the notification registrations added by <see cref="InstallPointerTracking"/>.
    /// </summary>
    public static void RemovePointerTracking(nint nsView)
    {
        nint center = SendMessage(GetClass("NSNotificationCenter"), GetSelector("defaultCenter"));
        SendMessageVoid(center, GetSelector("removeObserver:"), nsView);
    }

    /// <summary>
    /// Applies the native I-beam cursor when <paramref name="useTextCursor"/> is set, or the arrow otherwise.
    /// </summary>
    public static void SetCursor(bool useTextCursor)
    {
        nint cursor = SendMessage(GetClass("NSCursor"), GetSelector(useTextCursor ? "IBeamCursor" : "arrowCursor"));
        _ = SendMessage(cursor, GetSelector("set"));
    }

    public static void SetPasteboardString(string text)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        EnsureFrameworksLoaded();
        nint pbClass = GetClass("NSPasteboard");
        nint generalPb = SendMessage(pbClass, GetSelector("generalPasteboard"));
        if (generalPb == nint.Zero)
        {
            return;
        }

        _ = SendMessage(generalPb, GetSelector("clearContents"));
        nint nsText = CreateNSString(text);
        nint nsType = CreateNSString("public.utf8-plain-text");
        _ = SendMessage(generalPb, GetSelector("setString:forType:"), nsText, nsType);
    }

    public static string? GetPasteboardString()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        EnsureFrameworksLoaded();
        nint pbClass = GetClass("NSPasteboard");
        nint generalPb = SendMessage(pbClass, GetSelector("generalPasteboard"));
        if (generalPb == nint.Zero)
        {
            return null;
        }

        nint nsType = CreateNSString("public.utf8-plain-text");
        nint valObj = SendMessage(generalPb, GetSelector("stringForType:"), nsType);
        if (valObj == nint.Zero)
        {
            return null;
        }

        nint utf8Ptr = SendMessage(valObj, GetSelector("UTF8String"));
        return Marshal.PtrToStringUTF8(utf8Ptr);
    }

    public static bool IsSystemDarkMode()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        EnsureFrameworksLoaded();
        nint defaultsClass = GetClass("NSUserDefaults");
        nint standardDefaults = SendMessage(defaultsClass, GetSelector("standardUserDefaults"));
        if (standardDefaults == nint.Zero)
        {
            return false;
        }

        nint key = CreateNSString("AppleInterfaceStyle");
        nint valObj = SendMessage(standardDefaults, GetSelector("stringForKey:"), key);
        if (valObj == nint.Zero)
        {
            return false;
        }

        nint utf8Ptr = SendMessage(valObj, GetSelector("UTF8String"));
        string? style = Marshal.PtrToStringUTF8(utf8Ptr);
        return string.Equals(style, "Dark", StringComparison.OrdinalIgnoreCase);
    }

    public static nint CreateNSString(string text)
    {
        nint nsStringClass = GetClass("NSString");
        nint alloc = SendMessage(nsStringClass, GetSelector("alloc"));
        IntPtr utf8Ptr = Marshal.StringToCoTaskMemUTF8(text);
        try
        {
            return SendMessage(alloc, GetSelector("initWithUTF8String:"), utf8Ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Ptr);
        }
    }

    public static unsafe nint CreateNSData(ReadOnlySpan<byte> bytes)
    {
        nint nsDataClass = GetClass("NSData");
        fixed (byte* ptr = bytes)
        {
            return SendMessage(nsDataClass, GetSelector("dataWithBytes:length:"), (nint)ptr, (ulong)bytes.Length);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnAcceptsFirstResponder(nint self, nint cmd)
    {
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseDown(nint self, nint cmd, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return;
        }

        long clicks = SendMessageInt64(nsEvent, GetSelector("clickCount"));
        window.HandlePointerPressed(GetTopDownPoint(self, nsEvent), (int)Math.Max(1, clicks), GetModifiers(nsEvent));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseDragged(nint self, nint cmd, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return;
        }

        window.HandlePointerMoved(GetTopDownPoint(self, nsEvent), isPressed: true, GetModifiers(nsEvent));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseUp(nint self, nint cmd, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return;
        }

        window.HandlePointerReleased(GetTopDownPoint(self, nsEvent), GetModifiers(nsEvent));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseMoved(nint self, nint cmd, nint nsEvent)
    {
        if (s_viewToWindow.TryGetValue(self, out Window? window) && nsEvent != nint.Zero)
        {
            window.HandlePointerMoved(GetTopDownPoint(self, nsEvent), isPressed: false, GetModifiers(nsEvent));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseExited(nint self, nint cmd, nint nsEvent)
    {
        if (s_viewToWindow.TryGetValue(self, out Window? window))
        {
            window.HandlePointerExited();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnCursorUpdate(nint self, nint cmd, nint nsEvent)
    {
        if (s_viewToWindow.TryGetValue(self, out Window? window))
        {
            window.HandleCursorUpdate();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnWindowDidResignKey(nint self, nint cmd, nint notification)
    {
        if (s_viewToWindow.TryGetValue(self, out Window? window))
        {
            window.HandlePointerCancelled();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnWindowDidResize(nint self, nint cmd, nint notification)
    {
        if (s_viewToWindow.TryGetValue(self, out Window? window))
        {
            CGRect bounds = GetBounds(self);
            window.HandleResized(new Size(bounds.Size.Width, bounds.Size.Height));
        }
    }

    private static CGRect GetBounds(nint nsView)
    {
        nint selector = GetSelector("bounds");
        return RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? SendMessageRectStret(nsView, selector)
            : SendMessageRect(nsView, selector);
    }

    /// <summary>
    /// Converts an event's window location to top-down logical coordinates using the view's current bounds.
    /// </summary>
    private static Point GetTopDownPoint(nint nsView, nint nsEvent)
    {
        CGPoint location = SendMessagePoint(nsEvent, GetSelector("locationInWindow"));
        CGPoint viewPoint = SendMessageConvertPoint(nsView, GetSelector("convertPoint:fromView:"), location, nint.Zero);
        CGRect bounds = GetBounds(nsView);
        return new Point(viewPoint.X - bounds.Origin.X, bounds.Origin.Y + bounds.Size.Height - viewPoint.Y);
    }

    private static PointerModifiers GetModifiers(nint nsEvent)
    {
        ulong flags = SendMessageUInt64(nsEvent, GetSelector("modifierFlags"));
        PointerModifiers modifiers = PointerModifiers.None;
        if ((flags & EventModifierFlagShift) != 0)
        {
            modifiers |= PointerModifiers.Shift;
        }

        if ((flags & EventModifierFlagControl) != 0)
        {
            modifiers |= PointerModifiers.Control;
        }

        if ((flags & EventModifierFlagOption) != 0)
        {
            modifiers |= PointerModifiers.Alt;
        }

        if ((flags & EventModifierFlagCommand) != 0)
        {
            modifiers |= PointerModifiers.Meta;
        }

        return modifiers;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnKeyDown(nint self, nint cmd, nint nsEvent)
    {
        _ = TryDispatchKeyEvent(self, nsEvent);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnPerformKeyEquivalent(nint self, nint cmd, nint nsEvent)
    {
        return TryDispatchKeyEvent(self, nsEvent) ? (byte)1 : (byte)0;
    }

    private static bool TryDispatchKeyEvent(nint self, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return false;
        }

        ulong flags = SendMessageUInt64(nsEvent, GetSelector("modifierFlags"));
        bool hasCommandOrControl = (flags & (EventModifierFlagCommand | EventModifierFlagControl)) != 0;

        nint charsObj = SendMessage(nsEvent, GetSelector("charactersIgnoringModifiers"));
        if (charsObj == nint.Zero)
        {
            return false;
        }

        nint utf8Ptr = SendMessage(charsObj, GetSelector("UTF8String"));
        string? key = Marshal.PtrToStringUTF8(utf8Ptr);
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return window.HandleKeyDownAsync(key, hasCommandOrControl).GetAwaiter().GetResult();
    }
}
