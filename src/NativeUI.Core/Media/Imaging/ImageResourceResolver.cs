using System.Reflection;
using System.Xml;

namespace NativeUI.Media;

/// <summary>Resolves image sources to bounded encoded bytes independently of decoding.</summary>
internal static class ImageResourceResolver
{
    private static readonly HttpClient Client = new();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    internal static async ValueTask<byte[]> ReadAsync(string source, ImageResourceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(context);
        long maximumBytes = context.Options.MaximumEncodedBytes;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.StartsWith("resource://", StringComparison.Ordinal))
        {
            using Stream stream = OpenEmbeddedResourceStream(source, context.Assemblies);
            return await ReadBoundedAsync(stream, maximumBytes, cancellationToken).ConfigureAwait(false);
        }

        Uri? uri = ResolveRemoteUri(source, context.ApplicationBaseUri);
        if (uri is not null)
        {
            // Redirects are allowed between HTTP(S) destinations, subject to browser CORS on Web.
            using HttpResponseMessage response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is Uri destination && destination.Scheme is not ("http" or "https"))
            {
                throw new NotSupportedException("Image redirects must use HTTP(S).");
            }
            if (response.Content.Headers.ContentLength > maximumBytes)
            {
                throw new InvalidDataException("Image exceeds the configured encoded-byte limit.");
            }
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await ReadBoundedAsync(stream, maximumBytes, cancellationToken).ConfigureAwait(false);
        }

        string file = ResolveAssetPath(source, context.AssetRoot);
        using FileStream input = new(file, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ReadBoundedAsync(input, maximumBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static Uri? ResolveRemoteUri(string source, Uri? applicationBaseUri)
    {
        if (source.Any(char.IsControl) || source.Contains('\\') || source.StartsWith("//", StringComparison.Ordinal))
        {
            throw new FormatException("Invalid image asset path.");
        }
        if (source.Contains(':'))
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out Uri? absolute) || absolute.Scheme is not ("http" or "https"))
            {
                throw new NotSupportedException("Image sources support app assets, resource://, HTTP, and HTTPS.");
            }
            return absolute;
        }
        if (applicationBaseUri is null)
        {
            return null;
        }
        if (!applicationBaseUri.IsAbsoluteUri || applicationBaseUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The application base URI must be an absolute HTTP(S) URI.", nameof(applicationBaseUri));
        }
        // Both asset spellings are application-relative, including deployments under /app/.
        Uri resolved = new(applicationBaseUri, source.TrimStart('/'));
        string relative = applicationBaseUri.MakeRelativeUri(resolved).OriginalString;
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Image assets must remain within the application base URI.");
        }
        return resolved;
    }

    internal static string ResolveAssetPath(string source, string assetRoot)
    {
        string root = ResolveLinks(System.IO.Path.GetFullPath(assetRoot));
        string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, source.TrimStart('/')));
        EnsureContained(root, file);
        file = ResolveLinks(file);
        EnsureContained(root, file);
        return file;
    }

    private static void EnsureContained(string root, string file)
    {
        string relative = System.IO.Path.GetRelativePath(root, file);
        if (System.IO.Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Image assets must remain within the application directory.");
        }
    }

    private static string ResolveLinks(string path)
    {
        string root = System.IO.Path.GetPathRoot(path) ?? throw new InvalidDataException("An absolute asset root is required.");
        string current = root;
        foreach (string part in path[root.Length..].Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, part);
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (entry.LinkTarget is not null)
            {
                current = entry.ResolveLinkTarget(true)?.FullName ?? throw new InvalidDataException("Unable to resolve image asset link.");
            }
        }
        return current;
    }

    /// <summary>Identifies formats; SVG root recognition does not replace full SVG security validation.</summary>
    internal static string GetContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(PngSignature))
        {
            return "image/png";
        }
        if (bytes.StartsWith(JpegSignature))
        {
            return "image/jpeg";
        }
        try
        {
            using MemoryStream stream = new(bytes.ToArray());
            XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using XmlReader reader = XmlReader.Create(stream, settings);
            if (reader.MoveToContent() == XmlNodeType.Element && reader.LocalName == "svg" && reader.NamespaceURI is "" or "http://www.w3.org/2000/svg")
            {
                return "image/svg+xml";
            }
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Image content is not PNG, JPEG, or SVG.", exception);
        }
        throw new InvalidDataException("Only PNG, JPEG, and self-contained SVG images are supported.");
    }

    internal static (string AssemblyName, string Path) ParseResource(string source)
    {
        const string prefix = "resource://";
        if (!source.StartsWith(prefix, StringComparison.Ordinal) || source.Any(char.IsControl) || source.IndexOfAny(['\\', '?', '#']) >= 0)
        {
            throw new FormatException("Expected resource://AssemblyName/Path without query or fragment.");
        }
        int slash = source.IndexOf('/', prefix.Length);
        if (slash <= prefix.Length || slash == source.Length - 1)
        {
            throw new FormatException("Embedded images require an assembly name and resource path.");
        }
        string assembly = source[prefix.Length..slash];
        if (assembly.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')) || assembly is "." or ".." || assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Embedded images require a simple assembly name without .dll.");
        }
        string encodedPath = source[(slash + 1)..];
        string[] segments = encodedPath.Split('/');
        for (int index = 0; index < segments.Length; index++)
        {
            string segment = segments[index];
            for (int position = 0; position < segment.Length; position++)
            {
                if (segment[position] != '%') continue;
                if (position + 2 >= segment.Length || !Uri.IsHexDigit(segment[position + 1]) || !Uri.IsHexDigit(segment[position + 2]))
                {
                    throw new FormatException("Invalid resource path encoding.");
                }
                position += 2;
            }
            segment = Uri.UnescapeDataString(segment);
            if (segment is "" or "." or ".." || segment.Any(char.IsControl) || segment.IndexOfAny(['/', '\\', '?', '#', '%', ':']) >= 0)
            {
                throw new FormatException("Invalid resource path segment.");
            }
            segments[index] = segment;
        }
        return (assembly, string.Join('/', segments));
    }

    private static Assembly ResolveAssembly(string assemblyName, IReadOnlyCollection<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
            {
                return assembly;
            }
        }

        return Assembly.Load(new AssemblyName(assemblyName));
    }

    private static Stream OpenEmbeddedResourceStream(string source, IReadOnlyCollection<Assembly> assemblies)
    {
        (string assemblyName, string resourcePath) = ParseResource(source);
        Assembly assembly = ResolveAssembly(assemblyName, assemblies);

        Stream? stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream is not null)
        {
            return stream;
        }

        string conventionalResourceName = assemblyName + "." + resourcePath.Replace('/', '.');
        stream = assembly.GetManifestResourceStream(conventionalResourceName);
        if (stream is not null)
        {
            return stream;
        }

        throw new FileNotFoundException($"Embedded image '{resourcePath}' was not found in '{assemblyName}'.", source);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        using MemoryStream result = new();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (result.Length + count > maximumBytes)
            {
                throw new InvalidDataException("Image exceeds the configured encoded-byte limit.");
            }
            result.Write(buffer, 0, count);
        }
        return result.ToArray();
    }
}
