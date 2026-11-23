using NativeUI.Layout;

namespace NativeUI.Graphics3D;

/// <summary>
/// Implemented by a <see cref="NativeUI.Graphics.DrawingContext"/> whose backend renders a <see cref="Scene3D"/> itself,
/// in place of the polygons that <see cref="SceneRenderer"/> draws.
/// </summary>
internal interface ISceneDrawingContext
{
    /// <summary>
    /// Draws <paramref name="scene"/> as <paramref name="camera"/> sees it, filling <paramref name="bounds"/>.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="bounds">The area to fill, in the context's logical coordinates.</param>
    void DrawScene(Scene3D scene, PerspectiveCamera camera, Rect bounds);
}
