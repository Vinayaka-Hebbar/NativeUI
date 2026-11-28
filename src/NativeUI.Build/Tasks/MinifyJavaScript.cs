using Microsoft.Build.Framework;
using NUglify;
using NUglify.JavaScript;
using BuildTask = Microsoft.Build.Utilities.Task;

namespace NativeUI.Build;

/// <summary>
/// An MSBuild task that minifies each of <see cref="SourceFiles"/> with NUglify into the matching item of
/// <see cref="DestinationFiles"/>. A file that does not parse fails the build with the parser's errors.
/// </summary>
public sealed class MinifyJavaScript : BuildTask
{
    /// <summary>
    /// Gets or sets the scripts to minify.
    /// </summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the files that receive the minified scripts, one for each of <see cref="SourceFiles"/>, in order.
    /// </summary>
    [Required]
    public ITaskItem[] DestinationFiles { get; set; } = [];

    /// <inheritdoc />
    public override bool Execute()
    {
        if (SourceFiles.Length != DestinationFiles.Length)
        {
            Log.LogError($"{nameof(SourceFiles)} and {nameof(DestinationFiles)} must have the same number of items.");
            return false;
        }

        for (int i = 0; i < SourceFiles.Length; i++)
        {
            string source = SourceFiles[i].ItemSpec;
            UglifyResult result = Uglify.Js(File.ReadAllText(source), source, new CodeSettings());
            foreach (UglifyError error in result.Errors)
            {
                LogParserMessage(source, error);
            }

            if (!result.HasErrors)
            {
                string destination = DestinationFiles[i].ItemSpec;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllText(destination, result.Code);
            }
        }

        return !Log.HasLoggedErrors;
    }

    private void LogParserMessage(string file, UglifyError error)
    {
        if (error.IsError)
        {
            Log.LogError(null, error.ErrorCode, null, file, error.StartLine, error.StartColumn, error.EndLine, error.EndColumn, error.Message);
        }
        else
        {
            Log.LogWarning(null, error.ErrorCode, null, file, error.StartLine, error.StartColumn, error.EndLine, error.EndColumn, error.Message);
        }
    }
}
