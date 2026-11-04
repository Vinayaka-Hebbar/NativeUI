namespace NativeUI.Layout;

/// <summary>A replaced layout element whose content has natural dimensions.</summary>
public interface IIntrinsicSize
{
    /// <summary>Gets natural logical dimensions, resolving percentages against definite containing dimensions.</summary>
    Size GetIntrinsicSize(Size? containingSize = null);
}
