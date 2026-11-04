# Image sources and loading

`Image.Source` accepts loose PNG/JPEG/SVG assets, HTTP(S) URLs, explicit embedded
resources, and compiled SVG sources. The sample's **Images & SVG** page includes
`resource://NativeUI.Sample.Shared/Assets/google.svg`.

## Application assets

On desktop, `Assets/logo.png` and `/Assets/logo.png` resolve under
`AppContext.BaseDirectory`. Copy loose assets into the build and publish output:

```xml
<Content Include="Assets/logo.png"
         CopyToOutputDirectory="PreserveNewest"
         CopyToPublishDirectory="PreserveNewest" />
```

On Web, place loose files under the host's `wwwroot/Assets`. Both spellings resolve
against the document base URL. For a deployment using `<base href="/app/">`, both
resolve to `/app/Assets/logo.png`. Use a full HTTP(S) URL to address a different
origin or a path outside that base. Unsupported URI schemes and network-relative
`//host/path` sources are rejected. NativeUI fetches runtime SVG before validation,
so cross-origin sources require the server's CORS permission. HTTP(S) redirects are
allowed to HTTP(S) destinations, with browser security rules still applying.

Desktop paths are checked with `Path.GetRelativePath`, including after resolving
filesystem links. Sibling directories with matching name prefixes are outside the
asset root. These checks do not provide an operating-system sandbox against another
process replacing links between validation and opening the file; do not expose a
writable asset directory to untrusted processes.

## Embedded resources

```xml
<EmbeddedResource Include="Assets/logo.svg" LogicalName="Assets/logo.svg" />
```

```xml
<Image Source="resource://MyControls/Assets/logo.svg" AltText="Logo" />
```

Assembly names preserve casing and must be simple names without `.dll`. Resource
paths use forward slashes; queries, fragments, traversal, control characters, and
encoded separators are rejected. Lookup checks the exact logical name before the
conventional `MyControls.Assets.logo.svg` name. Use explicit `LogicalName` when the
root namespace differs. The loader checks known assemblies first, then falls back
to loading by simple assembly name. A resource URI does not make an otherwise
unreferenced DLL part of a WebAssembly publication; reference the assembly and
ensure its resources survive publication. Compiled SVG source inputs must also be
declared through `NativeUISvg` when not available from the application's resources.

## Limits and validation

```csharp
image.LoadOptions = new ImageLoadOptions
{
    MaximumEncodedBytes = 64L * 1024 * 1024
};
```

The default encoded limit is 32 MiB per image. Replacing `LoadOptions` reloads the
active source. Nonpositive limits are rejected. This bounds fetched/file/resource
bytes independently of decoding. The Skia raster decoder rejects dimensions exceeding 32 million pixels before
raster decoding. The browser checks natural dimensions after loading. SVG retains its element, depth, geometry, reference, and
external-content restrictions independently of the encoded limit.

Format recognition checks PNG/JPEG signatures or an SVG XML root with DTD and
external entity resolution disabled. Recognizing the root does **not** establish
SVG safety: full SVG validation runs before decoding or browser presentation.

## Compiled SVG definitions

`Svg` derives from `UIElement`. Its bindable `Definition` references immutable
`SvgNode` artwork; it does not own a collection of mutable child controls. Generated
factories create a new `Svg` and a fresh definition per active image attachment.
They retain no static artwork instances:

```csharp
internal static Svg CreateLogo() => new Svg { Definition = CreateLogoDefinition() };
```

Definitions contain typed node kinds, geometry, brushes, colors, lengths, viewBox
rectangles, transforms, and presentation properties. The compiler parses literal
SVG values once and emits their constructors. On Web, C# creates SVG DOM elements
and writes their attributes directly from typed values, with definition IDs scoped
per rendered image. It does not serialize the definition into XML or build attribute
dictionaries during presentation. Desktop drawing likewise consumes typed data.

For edits, replace `Svg.Definition` with an updated immutable definition. This
invalidates only that visual; other instances keep their own definitions.
The sample's editable image demonstrates this replacement.

Explicit runtime SVG sources pass through `SvgParser.Parse` for security validation
and typed conversion. Parsing is separate from `SvgNode`; compiled definitions
contain neither XML parsing nor markup serialization APIs.
