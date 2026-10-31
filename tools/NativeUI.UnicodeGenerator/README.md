# Unicode table generation

`NativeUI.UnicodeGenerator` is an incremental Roslyn source-generator project under the solution's `tools` folder. NativeUI.Core references it as an analyzer, so normal IDE and solution builds generate the tables automatically. There is no console entry point or separate generation command.

The generator receives official Unicode files through NativeUI.Core's `AdditionalFiles` items and reads the property enums from the consuming compilation. It does not read or write repository paths. It creates C# syntax trees and supplies `UnicodeDataTables.g.cs` directly to the compiler through `AddSource`. With `EmitCompilerGeneratedFiles` enabled, the SDK writes an inspection copy under NativeUI.Core's ignored `obj` directory. Generated files are not stored in the Layout source folder or committed to Git.

The project references the centrally versioned `Microsoft.CodeAnalysis.CSharp` NuGet package with `PrivateAssets=all` and is neither packable nor publishable. The analyzer project reference has `ReferenceOutputAssembly=false` and `PrivateAssets=all`; neither the generator nor its Roslyn package is a runtime dependency. Only the resulting tables are compiled into NativeUI.Core. The Unicode license is included in NativeUI.Core packages.

Generation works offline against the checked-in Unicode 17.0.0 inputs. It validates version headers and checksums and reports `NUIUNICODE001` as a compilation error for invalid or missing data. `.gitattributes` preserves the original input bytes across platforms.

The generator applies global and regional `@missing` defaults before explicit assignments. It verifies the packed lookup result for all 1,114,112 code points in each property table, validates bracket mappings and lookups, and checks private-use defaults. Output uses UTF-8 and LF line endings and contains no timestamps or machine paths.

## Updating Unicode

Download these files from the chosen version of the [official Unicode Character Database](https://www.unicode.org/Public/17.0.0/ucd/):

- `extracted/DerivedBidiClass.txt`
- `LineBreak.txt`
- `auxiliary/WordBreakProperty.txt`
- `BidiBrackets.txt`

Store the original bytes under a new version directory in `tools/Unicode/`. Update the `AdditionalFiles` version directory in NativeUI.Core's project, `UnicodeVersion`, and checksums in the generator. Review new property names against the consuming enums and algorithms. Keep the applicable [Unicode license](https://www.unicode.org/license.txt) under `tools/Unicode/LICENSE.txt` and update its checksum if the license changes.

Build the solution and run the Core verification suite before committing the inputs and generator changes. Do not commit generated compiler output. Correct property data does not by itself establish conformance of the bidirectional, wrapping, or word-boundary algorithms.
