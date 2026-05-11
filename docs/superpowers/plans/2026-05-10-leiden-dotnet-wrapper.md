# Graphiphy: Graphify C# Reimplementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reimplement the graphify Python application in C# .NET 10 with a native Leiden clustering wrapper, covering AST extraction for 9 languages, graph building, deduplication, clustering, analysis, caching, and report generation. No visualization features in this phase.

**Architecture:** Layered pipeline mirroring the Python original: Detect → Extract → Build → Deduplicate → Cluster → Analyze → Report. Interface-based language extractors (`ILanguageExtractor`) replace Python's config-dataclass-with-callbacks pattern. Native Leiden algorithm accessed via C interop shim + P/Invoke.

**Tech Stack:**
- C# 13 / .NET 10
- TUnit 1.43.41 (testing + assertions)
- TreeSitter.DotNet 1.3.0 (AST parsing)
- QuikGraph 2.5.0 (graph operations)
- F23.StringSimilarity 7.0.1 (Jaro-Winkler)
- MinHashSharp 1.1.1 (MinHash/LSH)
- C++17 native interop shim for libleidenalg

---

## File Structure

```
native/
  CMakeLists.txt
  leiden_interop.h
  leiden_interop.cpp

src/
  Graphiphy/
    Graphiphy.csproj
    Models/
      Node.cs
      Edge.cs
      Extraction.cs
      FileType.cs
      Confidence.cs
      GraphData.cs
    Detection/
      FileClassifier.cs
      FileDetector.cs
      IgnorePatterns.cs
    Extraction/
      ILanguageExtractor.cs
      GenericTreeSitterExtractor.cs
      LanguageRegistry.cs
      Extractors/
        PythonExtractor.cs
        JavaScriptExtractor.cs
        TypeScriptExtractor.cs
        CExtractor.cs
        CppExtractor.cs
        CSharpExtractor.cs
        JavaExtractor.cs
        GoExtractor.cs
        RustExtractor.cs
    Validation/
      ExtractionValidator.cs
    Build/
      GraphBuilder.cs
    Dedup/
      EntityDeduplicator.cs
      UnionFind.cs
    Cluster/
      LeidenClustering.cs
      NativeMethods.cs
      NativeLibraryResolver.cs
    Analysis/
      GraphAnalyzer.cs
    Cache/
      ExtractionCache.cs
    Report/
      ReportGenerator.cs

tests/
  Graphiphy.Tests/
    Graphiphy.Tests.csproj
    Validation/
      ExtractionValidatorTests.cs
    Detection/
      FileClassifierTests.cs
      FileDetectorTests.cs
    Extraction/
      ExtractorTestBase.cs
      PythonExtractorTests.cs
      JavaScriptExtractorTests.cs
      TypeScriptExtractorTests.cs
      CExtractorTests.cs
      CppExtractorTests.cs
      CSharpExtractorTests.cs
      JavaExtractorTests.cs
      GoExtractorTests.cs
      RustExtractorTests.cs
    Build/
      GraphBuilderTests.cs
    Dedup/
      EntityDeduplicatorTests.cs
    Cluster/
      LeidenClusteringTests.cs
    Analysis/
      GraphAnalyzerTests.cs
    Cache/
      ExtractionCacheTests.cs
    Report/
      ReportGeneratorTests.cs
    Fixtures/
      sample.py
      sample_calls.py
      sample.js
      sample.ts
      sample.c
      sample.cpp
      sample.cs
      sample.java
      sample.go
      sample.rs

Graphiphy.sln
```

---

## Phase 1: Foundation

### Task 1: Create Solution Scaffold

**Files:**
- Create: `Graphiphy.sln`
- Create: `src/Graphiphy/Graphiphy.csproj`
- Create: `tests/Graphiphy.Tests/Graphiphy.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
cd /home/timm/graphiphy
dotnet new sln -n Graphiphy
mkdir -p src/Graphiphy
dotnet new classlib -n Graphiphy -o src/Graphiphy -f net10.0
rm src/Graphiphy/Class1.cs
mkdir -p tests/Graphiphy.Tests
dotnet new console -n Graphiphy.Tests -o tests/Graphiphy.Tests -f net10.0
rm tests/Graphiphy.Tests/Program.cs
dotnet sln add src/Graphiphy/Graphiphy.csproj
dotnet sln add tests/Graphiphy.Tests/Graphiphy.Tests.csproj
dotnet add tests/Graphiphy.Tests/Graphiphy.Tests.csproj reference src/Graphiphy/Graphiphy.csproj
```

- [ ] **Step 2: Configure the main library csproj**

Write `src/Graphiphy/Graphiphy.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Graphiphy</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="QuikGraph" Version="2.5.0" />
    <PackageReference Include="TreeSitter.DotNet" Version="1.3.0" />
    <PackageReference Include="F23.StringSimilarity" Version="7.0.1" />
    <PackageReference Include="MinHashSharp" Version="1.1.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Configure the test project csproj**

Write `tests/Graphiphy.Tests/Graphiphy.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.41" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Graphiphy\Graphiphy.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" LinkBase="Fixtures" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Verify solution builds**

Run: `dotnet build Graphiphy.sln`
Expected: Build succeeded with 0 errors

- [ ] **Step 5: Commit**

```bash
git init
git add Graphiphy.sln src/Graphiphy/Graphiphy.csproj tests/Graphiphy.Tests/Graphiphy.Tests.csproj
git commit -m "feat: scaffold .NET 10 solution with TUnit and dependencies"
```

---

### Task 2: Core Domain Models

**Files:**
- Create: `src/Graphiphy/Models/FileType.cs`
- Create: `src/Graphiphy/Models/Confidence.cs`
- Create: `src/Graphiphy/Models/Node.cs`
- Create: `src/Graphiphy/Models/Edge.cs`
- Create: `src/Graphiphy/Models/Extraction.cs`
- Create: `src/Graphiphy/Models/GraphData.cs`

- [ ] **Step 1: Write the FileType enum**

```csharp
// src/Graphiphy/Models/FileType.cs
namespace Graphiphy.Models;

public enum FileType
{
    Code,
    Document,
    Paper,
    Image,
    Rationale,
    Concept,
    Video,
}

public static class FileTypeExtensions
{
    public static string ToSchemaString(this FileType ft) => ft switch
    {
        FileType.Code => "code",
        FileType.Document => "document",
        FileType.Paper => "paper",
        FileType.Image => "image",
        FileType.Rationale => "rationale",
        FileType.Concept => "concept",
        FileType.Video => "video",
        _ => "concept",
    };

    public static FileType FromString(string? s) => s?.ToLowerInvariant() switch
    {
        "code" => FileType.Code,
        "document" => FileType.Document,
        "paper" => FileType.Paper,
        "image" => FileType.Image,
        "rationale" => FileType.Rationale,
        "concept" => FileType.Concept,
        "video" => FileType.Video,
        _ => FileType.Concept,
    };
}
```

- [ ] **Step 2: Write the Confidence enum**

```csharp
// src/Graphiphy/Models/Confidence.cs
namespace Graphiphy.Models;

public enum Confidence
{
    Extracted,
    Inferred,
    Ambiguous,
}

public static class ConfidenceExtensions
{
    public static string ToSchemaString(this Confidence c) => c switch
    {
        Confidence.Extracted => "EXTRACTED",
        Confidence.Inferred => "INFERRED",
        Confidence.Ambiguous => "AMBIGUOUS",
        _ => "EXTRACTED",
    };

    public static Confidence FromString(string? s) => s?.ToUpperInvariant() switch
    {
        "EXTRACTED" => Confidence.Extracted,
        "INFERRED" => Confidence.Inferred,
        "AMBIGUOUS" => Confidence.Ambiguous,
        _ => Confidence.Extracted,
    };
}
```

- [ ] **Step 3: Write the Node record**

```csharp
// src/Graphiphy/Models/Node.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class Node
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("file_type")]
    public required string FileTypeString { get; set; }

    [JsonPropertyName("source_file")]
    public required string SourceFile { get; set; }

    [JsonPropertyName("source_location")]
    public string? SourceLocation { get; set; }

    [JsonPropertyName("community")]
    public int? Community { get; set; }

    [JsonPropertyName("norm_label")]
    public string? NormLabel { get; set; }

    [JsonIgnore]
    public FileType FileType
    {
        get => FileTypeExtensions.FromString(FileTypeString);
        set => FileTypeString = value.ToSchemaString();
    }
}
```

- [ ] **Step 4: Write the Edge record**

```csharp
// src/Graphiphy/Models/Edge.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class Edge
{
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("target")]
    public required string Target { get; set; }

    [JsonPropertyName("relation")]
    public required string Relation { get; set; }

    [JsonPropertyName("confidence")]
    public required string ConfidenceString { get; set; }

    [JsonPropertyName("source_file")]
    public required string SourceFile { get; set; }

    [JsonPropertyName("source_location")]
    public string? SourceLocation { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [JsonIgnore]
    public Confidence Confidence
    {
        get => ConfidenceExtensions.FromString(ConfidenceString);
        set => ConfidenceString = value.ToSchemaString();
    }
}
```

- [ ] **Step 5: Write the Extraction container**

```csharp
// src/Graphiphy/Models/Extraction.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class Extraction
{
    [JsonPropertyName("nodes")]
    public List<Node> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<Edge> Edges { get; set; } = [];

    [JsonPropertyName("source_file")]
    public string? SourceFile { get; set; }
}
```

- [ ] **Step 6: Write the GraphData container**

```csharp
// src/Graphiphy/Models/GraphData.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class GraphData
{
    [JsonPropertyName("nodes")]
    public List<Node> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<Edge> Edges { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
```

- [ ] **Step 7: Verify build**

Run: `dotnet build src/Graphiphy/Graphiphy.csproj`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/Graphiphy/Models/
git commit -m "feat: add core domain models (Node, Edge, Extraction, FileType, Confidence)"
```

---

### Task 3: Extraction Validator (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Validation/ExtractionValidatorTests.cs`
- Create: `src/Graphiphy/Validation/ExtractionValidator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Validation/ExtractionValidatorTests.cs
using Graphiphy.Models;
using Graphiphy.Validation;

namespace Graphiphy.Tests.Validation;

public class ExtractionValidatorTests
{
    [Test]
    public async Task ValidExtraction_ReturnsNoErrors()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "mod::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges =
            [
                new Edge { Source = "mod::Foo", Target = "mod::Foo", Relation = "contains", ConfidenceString = "EXTRACTED", SourceFile = "foo.py" }
            ]
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task MissingNodeId_ReturnsError()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "", Label = "Foo", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges = []
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("id");
    }

    [Test]
    public async Task MissingNodeLabel_ReturnsError()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges = []
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("label");
    }

    [Test]
    public async Task InvalidFileType_ReturnsError()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "banana", SourceFile = "foo.py" }
            ],
            Edges = []
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("file_type");
    }

    [Test]
    public async Task InvalidConfidence_ReturnsError()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges =
            [
                new Edge { Source = "x::Foo", Target = "x::Foo", Relation = "calls", ConfidenceString = "MAYBE", SourceFile = "foo.py" }
            ]
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("confidence");
    }

    [Test]
    public async Task DanglingEdge_ReturnsError()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges =
            [
                new Edge { Source = "x::Foo", Target = "x::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "foo.py" }
            ]
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("dangling");
    }

    [Test]
    public async Task MissingEdgeSourceFile_ReturnsError()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges =
            [
                new Edge { Source = "x::Foo", Target = "x::Foo", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "" }
            ]
        };

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("source_file");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "ExtractionValidatorTests"`
Expected: Compilation error — `ExtractionValidator` does not exist

- [ ] **Step 3: Implement ExtractionValidator**

```csharp
// src/Graphiphy/Validation/ExtractionValidator.cs
using Graphiphy.Models;

namespace Graphiphy.Validation;

public static class ExtractionValidator
{
    private static readonly HashSet<string> ValidFileTypes =
        ["code", "document", "paper", "image", "rationale", "concept"];

    private static readonly HashSet<string> ValidConfidences =
        ["EXTRACTED", "INFERRED", "AMBIGUOUS"];

    public static List<string> Validate(Extraction extraction)
    {
        var errors = new List<string>();
        var nodeIds = new HashSet<string>();

        for (int i = 0; i < extraction.Nodes.Count; i++)
        {
            var node = extraction.Nodes[i];

            if (string.IsNullOrWhiteSpace(node.Id))
                errors.Add($"Node[{i}]: missing required field 'id'");
            else
                nodeIds.Add(node.Id);

            if (string.IsNullOrWhiteSpace(node.Label))
                errors.Add($"Node[{i}]: missing required field 'label'");

            if (string.IsNullOrWhiteSpace(node.SourceFile))
                errors.Add($"Node[{i}]: missing required field 'source_file'");

            if (!ValidFileTypes.Contains(node.FileTypeString))
                errors.Add($"Node[{i}]: invalid file_type '{node.FileTypeString}' (must be one of: {string.Join(", ", ValidFileTypes)})");
        }

        for (int i = 0; i < extraction.Edges.Count; i++)
        {
            var edge = extraction.Edges[i];

            if (string.IsNullOrWhiteSpace(edge.Source))
                errors.Add($"Edge[{i}]: missing required field 'source'");

            if (string.IsNullOrWhiteSpace(edge.Target))
                errors.Add($"Edge[{i}]: missing required field 'target'");

            if (string.IsNullOrWhiteSpace(edge.Relation))
                errors.Add($"Edge[{i}]: missing required field 'relation'");

            if (string.IsNullOrWhiteSpace(edge.SourceFile))
                errors.Add($"Edge[{i}]: missing required field 'source_file'");

            if (!ValidConfidences.Contains(edge.ConfidenceString))
                errors.Add($"Edge[{i}]: invalid confidence '{edge.ConfidenceString}' (must be one of: {string.Join(", ", ValidConfidences)})");

            if (!string.IsNullOrWhiteSpace(edge.Source) && !nodeIds.Contains(edge.Source))
                errors.Add($"Edge[{i}]: dangling source '{edge.Source}' not found in nodes");

            if (!string.IsNullOrWhiteSpace(edge.Target) && !nodeIds.Contains(edge.Target))
                errors.Add($"Edge[{i}]: dangling target '{edge.Target}' not found in nodes");
        }

        return errors;
    }

    public static void AssertValid(Extraction extraction)
    {
        var errors = Validate(extraction);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Invalid extraction ({errors.Count} errors):\n" + string.Join("\n", errors));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "ExtractionValidatorTests"`
Expected: All 7 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Validation/ tests/Graphiphy.Tests/Validation/
git commit -m "feat: add extraction validator with TDD tests"
```

---

## Phase 2: Detection

### Task 4: File Classifier (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Detection/FileClassifierTests.cs`
- Create: `src/Graphiphy/Detection/FileClassifier.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Detection/FileClassifierTests.cs
using Graphiphy.Detection;
using Graphiphy.Models;

namespace Graphiphy.Tests.Detection;

public class FileClassifierTests
{
    [Test]
    [Arguments(".py", FileType.Code)]
    [Arguments(".js", FileType.Code)]
    [Arguments(".ts", FileType.Code)]
    [Arguments(".c", FileType.Code)]
    [Arguments(".cpp", FileType.Code)]
    [Arguments(".cs", FileType.Code)]
    [Arguments(".java", FileType.Code)]
    [Arguments(".go", FileType.Code)]
    [Arguments(".rs", FileType.Code)]
    [Arguments(".rb", FileType.Code)]
    [Arguments(".swift", FileType.Code)]
    [Arguments(".kt", FileType.Code)]
    public async Task CodeExtensions_ClassifiedAsCode(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".md", FileType.Document)]
    [Arguments(".txt", FileType.Document)]
    [Arguments(".rst", FileType.Document)]
    [Arguments(".adoc", FileType.Document)]
    public async Task DocExtensions_ClassifiedAsDocument(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".png", FileType.Image)]
    [Arguments(".jpg", FileType.Image)]
    [Arguments(".svg", FileType.Image)]
    public async Task ImageExtensions_ClassifiedAsImage(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".mp4", FileType.Video)]
    [Arguments(".webm", FileType.Video)]
    public async Task VideoExtensions_ClassifiedAsVideo(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task PdfInXcassets_ClassifiedAsImage()
    {
        var result = FileClassifier.Classify("Assets.xcassets/icon.imageset/logo.pdf");
        await Assert.That(result).IsEqualTo(FileType.Image);
    }

    [Test]
    public async Task PdfNormal_ClassifiedAsPaper()
    {
        var result = FileClassifier.Classify("research/attention.pdf");
        await Assert.That(result).IsEqualTo(FileType.Paper);
    }

    [Test]
    public async Task UnknownExtension_ReturnsNull()
    {
        var result = FileClassifier.ClassifyOrNull("data.xyz123");
        await Assert.That(result).IsNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "FileClassifierTests"`
Expected: Compilation error — `FileClassifier` does not exist

- [ ] **Step 3: Implement FileClassifier**

```csharp
// src/Graphiphy/Detection/FileClassifier.cs
using Graphiphy.Models;

namespace Graphiphy.Detection;

public static class FileClassifier
{
    private static readonly HashSet<string> CodeExtensions =
    [
        ".py", ".js", ".ts", ".jsx", ".tsx", ".c", ".h", ".cpp", ".hpp", ".cc", ".cxx",
        ".cs", ".java", ".go", ".rs", ".rb", ".swift", ".kt", ".kts", ".scala", ".sc",
        ".php", ".lua", ".zig", ".ps1", ".psm1", ".ex", ".exs", ".m", ".mm", ".jl",
        ".v", ".sv", ".f90", ".f95", ".f03", ".f", ".for", ".pas", ".pp", ".lpr",
        ".dpr", ".dart", ".groovy", ".gvy", ".sql", ".sh", ".bash", ".zsh", ".fish",
        ".r", ".R", ".pl", ".pm", ".t", ".vb", ".fs", ".fsx", ".clj", ".cljs",
        ".erl", ".hrl", ".hs", ".lhs", ".ml", ".mli", ".nim", ".cr", ".d",
        ".ada", ".adb", ".ads", ".cob", ".cbl", ".lisp", ".cl", ".el",
        ".vue", ".svelte", ".astro",
    ];

    private static readonly HashSet<string> DocExtensions =
    [
        ".md", ".markdown", ".txt", ".rst", ".adoc", ".asciidoc", ".org",
        ".wiki", ".textile", ".rtf", ".docx", ".odt", ".tex", ".html", ".htm",
        ".yaml", ".yml", ".toml", ".json", ".xml", ".csv", ".tsv",
    ];

    private static readonly HashSet<string> PaperExtensions = [".pdf"];

    private static readonly HashSet<string> ImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tif",
        ".svg", ".webp", ".ico", ".heic", ".heif", ".avif",
    ];

    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".webm", ".mkv", ".avi", ".mov", ".flv", ".wmv",
        ".m4v", ".ogv", ".3gp",
    ];

    public static FileType Classify(string filePath)
    {
        return ClassifyOrNull(filePath) ?? FileType.Concept;
    }

    public static FileType? ClassifyOrNull(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (CodeExtensions.Contains(ext))
            return FileType.Code;

        if (DocExtensions.Contains(ext))
            return FileType.Document;

        if (ImageExtensions.Contains(ext))
            return FileType.Image;

        if (VideoExtensions.Contains(ext))
            return FileType.Video;

        if (PaperExtensions.Contains(ext))
        {
            // PDFs inside .xcassets are icons, not papers
            if (filePath.Contains(".xcassets", StringComparison.OrdinalIgnoreCase))
                return FileType.Image;
            return FileType.Paper;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "FileClassifierTests"`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Detection/FileClassifier.cs tests/Graphiphy.Tests/Detection/
git commit -m "feat: add file type classifier with extension mapping"
```

---

### Task 5: File Detector and Ignore Patterns (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Detection/FileDetectorTests.cs`
- Create: `src/Graphiphy/Detection/IgnorePatterns.cs`
- Create: `src/Graphiphy/Detection/FileDetector.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Detection/FileDetectorTests.cs
using Graphiphy.Detection;
using Graphiphy.Models;

namespace Graphiphy.Tests.Detection;

public class FileDetectorTests
{
    [Test]
    public async Task Detect_FindsPythonFiles()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), "class Foo: pass");
        File.WriteAllText(Path.Combine(dir, "readme.md"), "# Hello");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).Contains(r => r.RelativePath == "main.py");
        await Assert.That(results.First(r => r.RelativePath == "main.py").FileType).IsEqualTo(FileType.Code);
    }

    [Test]
    public async Task Detect_RespectsGraphifyIgnore()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), "x = 1");
        File.WriteAllText(Path.Combine(dir, "generated.py"), "x = 2");
        File.WriteAllText(Path.Combine(dir, ".graphifyignore"), "generated.py");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath == "generated.py");
        await Assert.That(results).Contains(r => r.RelativePath == "main.py");
    }

    [Test]
    public async Task Detect_IgnoresHiddenDirs()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
        File.WriteAllText(Path.Combine(dir, ".git", "config"), "x");
        File.WriteAllText(Path.Combine(dir, "main.py"), "x = 1");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath.Contains(".git"));
    }

    [Test]
    public async Task Detect_IgnoresNodeModules()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "node_modules", "pkg"));
        File.WriteAllText(Path.Combine(dir, "node_modules", "pkg", "index.js"), "x");
        File.WriteAllText(Path.Combine(dir, "app.js"), "x");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath.Contains("node_modules"));
    }

    [Test]
    public async Task Detect_IgnoresSensitiveFiles()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, ".env"), "SECRET=x");
        File.WriteAllText(Path.Combine(dir, "id_rsa"), "-----BEGIN RSA PRIVATE KEY-----");
        File.WriteAllText(Path.Combine(dir, "main.py"), "x = 1");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath == ".env");
        await Assert.That(results).DoesNotContain(r => r.RelativePath == "id_rsa");
    }

    [Test]
    public async Task Detect_WildcardIgnorePattern()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "foo.gen.py"), "x");
        File.WriteAllText(Path.Combine(dir, "bar.gen.py"), "x");
        File.WriteAllText(Path.Combine(dir, "main.py"), "x");
        File.WriteAllText(Path.Combine(dir, ".graphifyignore"), "*.gen.py");

        var results = FileDetector.Detect(dir);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].RelativePath).IsEqualTo("main.py");
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "FileDetectorTests"`
Expected: Compilation error

- [ ] **Step 3: Implement IgnorePatterns**

```csharp
// src/Graphiphy/Detection/IgnorePatterns.cs
using System.Text.RegularExpressions;

namespace Graphiphy.Detection;

public sealed class IgnorePatterns
{
    private readonly List<Regex> _patterns = [];

    private static readonly string[] DefaultIgnoreDirs =
    [
        ".git", ".hg", ".svn", "__pycache__", "node_modules", ".tox",
        ".mypy_cache", ".pytest_cache", "dist", "build", ".eggs",
        "graphify-out", ".graphify-out", "venv", ".venv", "env",
        ".idea", ".vs", ".vscode", "bin", "obj",
    ];

    private static readonly string[] SensitivePatterns =
    [
        ".env", ".env.*", "*.pem", "id_rsa*", "id_ed25519*",
        "credentials.json", "service-account*.json", "*.key",
    ];

    public static IgnorePatterns Load(string rootDir)
    {
        var patterns = new IgnorePatterns();

        // Add sensitive file patterns
        foreach (var p in SensitivePatterns)
            patterns.Add(p);

        // Load .graphifyignore if present
        var ignoreFile = Path.Combine(rootDir, ".graphifyignore");
        if (File.Exists(ignoreFile))
        {
            foreach (var line in File.ReadAllLines(ignoreFile))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    patterns.Add(trimmed);
            }
        }

        return patterns;
    }

    public void Add(string globPattern)
    {
        _patterns.Add(GlobToRegex(globPattern));
    }

    public bool IsIgnored(string relativePath)
    {
        // Check default ignored directories
        var parts = relativePath.Split('/', '\\');
        foreach (var part in parts)
        {
            if (part.StartsWith('.') && part.Length > 1)
                return true;
            if (DefaultIgnoreDirs.Contains(part))
                return true;
        }

        // Check custom patterns
        var fileName = Path.GetFileName(relativePath);
        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(fileName) || pattern.IsMatch(relativePath))
                return true;
        }

        return false;
    }

    private static Regex GlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
```

- [ ] **Step 4: Implement FileDetector**

```csharp
// src/Graphiphy/Detection/FileDetector.cs
using Graphiphy.Models;

namespace Graphiphy.Detection;

public sealed record DetectedFile(string RelativePath, string AbsolutePath, FileType FileType);

public static class FileDetector
{
    public static List<DetectedFile> Detect(string rootDir)
    {
        var ignore = IgnorePatterns.Load(rootDir);
        var results = new List<DetectedFile>();

        foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');

            if (ignore.IsIgnored(relativePath))
                continue;

            var fileType = FileClassifier.ClassifyOrNull(relativePath);
            if (fileType is null)
                continue;

            results.Add(new DetectedFile(relativePath, file, fileType.Value));
        }

        return results;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "FileDetectorTests"`
Expected: All 6 tests pass

- [ ] **Step 6: Commit**

```bash
git add src/Graphiphy/Detection/ tests/Graphiphy.Tests/Detection/
git commit -m "feat: add file detection with ignore patterns"
```

---

## Phase 3: Extraction Pipeline

### Task 6: ILanguageExtractor Interface and Registry

**Files:**
- Create: `src/Graphiphy/Extraction/ILanguageExtractor.cs`
- Create: `src/Graphiphy/Extraction/LanguageRegistry.cs`

- [ ] **Step 1: Define the extractor interface**

```csharp
// src/Graphiphy/Extraction/ILanguageExtractor.cs
using Graphiphy.Models;

namespace Graphiphy.Extraction;

public interface ILanguageExtractor
{
    /// <summary>
    /// File extensions this extractor handles (e.g. ".py", ".pyw").
    /// </summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>
    /// The tree-sitter language name (e.g. "python", "javascript").
    /// </summary>
    string TreeSitterLanguage { get; }

    /// <summary>
    /// Extract nodes and edges from source code.
    /// </summary>
    Extraction Extract(string filePath, string sourceCode);
}
```

- [ ] **Step 2: Implement the registry**

```csharp
// src/Graphiphy/Extraction/LanguageRegistry.cs
namespace Graphiphy.Extraction;

public sealed class LanguageRegistry
{
    private readonly Dictionary<string, ILanguageExtractor> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ILanguageExtractor> _extractors = [];

    public void Register(ILanguageExtractor extractor)
    {
        _extractors.Add(extractor);
        foreach (var ext in extractor.SupportedExtensions)
            _byExtension[ext] = extractor;
    }

    public ILanguageExtractor? GetExtractor(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return _byExtension.GetValueOrDefault(ext);
    }

    public IReadOnlyList<ILanguageExtractor> All => _extractors;

    public static LanguageRegistry CreateDefault()
    {
        var registry = new LanguageRegistry();
        // Extractors will be registered here as they are implemented
        return registry;
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Graphiphy/Graphiphy.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy/Extraction/ILanguageExtractor.cs src/Graphiphy/Extraction/LanguageRegistry.cs
git commit -m "feat: add ILanguageExtractor interface and registry"
```

---

### Task 7: Generic Tree-Sitter Extractor Base

**Files:**
- Create: `src/Graphiphy/Extraction/GenericTreeSitterExtractor.cs`

- [ ] **Step 1: Implement the generic extractor base class**

```csharp
// src/Graphiphy/Extraction/GenericTreeSitterExtractor.cs
using Graphiphy.Models;
using TreeSitter;

namespace Graphiphy.Extraction;

/// <summary>
/// Base class providing tree-sitter parsing and common AST walking patterns.
/// Subclasses override to define language-specific node type mappings.
/// </summary>
public abstract class GenericTreeSitterExtractor : ILanguageExtractor
{
    public abstract IReadOnlySet<string> SupportedExtensions { get; }
    public abstract string TreeSitterLanguage { get; }

    /// <summary>Node types that represent class/struct/interface declarations.</summary>
    protected abstract IReadOnlySet<string> ClassNodeTypes { get; }

    /// <summary>Node types that represent function/method declarations.</summary>
    protected abstract IReadOnlySet<string> FunctionNodeTypes { get; }

    /// <summary>Node types that represent import/using statements.</summary>
    protected abstract IReadOnlySet<string> ImportNodeTypes { get; }

    /// <summary>Node types that represent function/method calls.</summary>
    protected abstract IReadOnlySet<string> CallNodeTypes { get; }

    /// <summary>
    /// Extract the name from a class/function/import AST node.
    /// Returns null if the node should be skipped.
    /// </summary>
    protected abstract string? ExtractName(Node node, string nodeType);

    /// <summary>
    /// Extract import target from an import node.
    /// Returns the module/symbol being imported.
    /// </summary>
    protected abstract string? ExtractImportTarget(Node node);

    /// <summary>
    /// Extract the callee name from a call expression node.
    /// </summary>
    protected abstract string? ExtractCallTarget(Node node);

    /// <summary>
    /// Build the node ID prefix from the file path (e.g. "module::").
    /// </summary>
    protected virtual string BuildIdPrefix(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        // Strip special chars, keep alphanumeric and underscore
        var clean = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return clean + "::";
    }

    public Extraction Extract(string filePath, string sourceCode)
    {
        var extraction = new Extraction { SourceFile = filePath };
        var prefix = BuildIdPrefix(filePath);

        using var parser = new Parser();
        parser.Language = Languages.Get(TreeSitterLanguage);

        using var tree = parser.Parse(sourceCode);
        var root = tree.Root;

        var classNames = new HashSet<string>();
        var functionNames = new HashSet<string>();
        var nodeIds = new HashSet<string>();

        // Walk the tree
        WalkNode(root, extraction, prefix, filePath, classNames, functionNames, nodeIds, parent: null);

        return extraction;
    }

    private void WalkNode(
        Node node,
        Extraction extraction,
        string prefix,
        string filePath,
        HashSet<string> classNames,
        HashSet<string> functionNames,
        HashSet<string> nodeIds,
        string? parent)
    {
        var nodeType = node.Type;
        var location = $"L{node.StartPosition.Row + 1}";

        if (ClassNodeTypes.Contains(nodeType))
        {
            var name = ExtractName(node, nodeType);
            if (name is not null)
            {
                var id = prefix + name;
                if (nodeIds.Add(id))
                {
                    extraction.Nodes.Add(new Models.Node
                    {
                        Id = id,
                        Label = name,
                        FileTypeString = "code",
                        SourceFile = filePath,
                        SourceLocation = location,
                    });
                    classNames.Add(name);

                    if (parent is not null)
                    {
                        extraction.Edges.Add(new Edge
                        {
                            Source = parent,
                            Target = id,
                            Relation = "contains",
                            ConfidenceString = "EXTRACTED",
                            SourceFile = filePath,
                            SourceLocation = location,
                        });
                    }
                }

                // Walk children with this class as parent
                for (int i = 0; i < node.ChildCount; i++)
                    WalkNode(node.Child(i)!, extraction, prefix, filePath, classNames, functionNames, nodeIds, id);
                return;
            }
        }

        if (FunctionNodeTypes.Contains(nodeType))
        {
            var name = ExtractName(node, nodeType);
            if (name is not null)
            {
                var id = prefix + name;
                if (nodeIds.Add(id))
                {
                    extraction.Nodes.Add(new Models.Node
                    {
                        Id = id,
                        Label = name,
                        FileTypeString = "code",
                        SourceFile = filePath,
                        SourceLocation = location,
                    });
                    functionNames.Add(name);

                    if (parent is not null)
                    {
                        extraction.Edges.Add(new Edge
                        {
                            Source = parent,
                            Target = id,
                            Relation = "contains",
                            ConfidenceString = "EXTRACTED",
                            SourceFile = filePath,
                            SourceLocation = location,
                        });
                    }
                }

                // Walk children for calls within this function
                for (int i = 0; i < node.ChildCount; i++)
                    WalkNode(node.Child(i)!, extraction, prefix, filePath, classNames, functionNames, nodeIds, id);
                return;
            }
        }

        if (ImportNodeTypes.Contains(nodeType))
        {
            var target = ExtractImportTarget(node);
            if (target is not null)
            {
                var targetId = target;
                if (!nodeIds.Contains(targetId))
                {
                    nodeIds.Add(targetId);
                    extraction.Nodes.Add(new Models.Node
                    {
                        Id = targetId,
                        Label = target.Split("::").Last(),
                        FileTypeString = "code",
                        SourceFile = filePath,
                        SourceLocation = location,
                    });
                }

                var sourceId = parent ?? prefix.TrimEnd(':');
                extraction.Edges.Add(new Edge
                {
                    Source = sourceId,
                    Target = targetId,
                    Relation = "imports",
                    ConfidenceString = "EXTRACTED",
                    SourceFile = filePath,
                    SourceLocation = location,
                });
            }
        }

        if (CallNodeTypes.Contains(nodeType))
        {
            var target = ExtractCallTarget(node);
            if (target is not null && parent is not null)
            {
                var targetId = prefix + target;
                extraction.Edges.Add(new Edge
                {
                    Source = parent,
                    Target = targetId,
                    Relation = "calls",
                    ConfidenceString = "EXTRACTED",
                    SourceFile = filePath,
                    SourceLocation = location,
                });
            }
        }

        // Recurse into children
        for (int i = 0; i < node.ChildCount; i++)
            WalkNode(node.Child(i)!, extraction, prefix, filePath, classNames, functionNames, nodeIds, parent);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Graphiphy/Graphiphy.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/Graphiphy/Extraction/GenericTreeSitterExtractor.cs
git commit -m "feat: add generic tree-sitter extractor base class"
```

---

### Task 8: Python Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.py`
- Create: `tests/Graphiphy.Tests/Extraction/ExtractorTestBase.cs`
- Create: `tests/Graphiphy.Tests/Extraction/PythonExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/PythonExtractor.cs`

- [ ] **Step 1: Create the Python test fixture**

```python
# tests/Graphiphy.Tests/Fixtures/sample.py
import os
from pathlib import Path

class Transformer:
    def __init__(self, config):
        self.config = config

    def forward(self, x):
        return self._attention(x)

    def _attention(self, x):
        return x

class Pipeline(Transformer):
    def run(self):
        result = self.forward(None)
        os.path.join("a", "b")
        return result

def helper():
    p = Pipeline({})
    p.run()
```

- [ ] **Step 2: Create the test base class**

```csharp
// tests/Graphiphy.Tests/Extraction/ExtractorTestBase.cs
using Graphiphy.Extraction;
using Graphiphy.Models;

namespace Graphiphy.Tests.Extraction;

public abstract class ExtractorTestBase
{
    protected abstract ILanguageExtractor CreateExtractor();

    protected string FixturePath(string filename)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);
    }

    protected Graphiphy.Models.Extraction ExtractFixture(string filename)
    {
        var path = FixturePath(filename);
        var source = File.ReadAllText(path);
        return CreateExtractor().Extract(path, source);
    }

    protected static List<string> NodeLabels(Graphiphy.Models.Extraction extraction)
    {
        return extraction.Nodes.Select(n => n.Label).ToList();
    }

    protected static List<string> EdgeRelations(Graphiphy.Models.Extraction extraction, string sourceLabel, string targetLabel)
    {
        return extraction.Edges
            .Where(e =>
                extraction.Nodes.Any(n => n.Id == e.Source && n.Label == sourceLabel) &&
                (extraction.Nodes.Any(n => n.Id == e.Target && n.Label == targetLabel) ||
                 e.Target.EndsWith("::" + targetLabel)))
            .Select(e => e.Relation)
            .ToList();
    }
}
```

- [ ] **Step 3: Write the Python extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/PythonExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class PythonExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new PythonExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.py");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Transformer");
        await Assert.That(labels).Contains("Pipeline");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.py");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("forward");
        await Assert.That(labels).Contains("_attention");
        await Assert.That(labels).Contains("helper");
        await Assert.That(labels).Contains("run");
    }

    [Test]
    public async Task Extract_FindsContainsEdges()
    {
        var result = ExtractFixture("sample.py");
        var relations = EdgeRelations(result, "Transformer", "forward");

        await Assert.That(relations).Contains("contains");
    }

    [Test]
    public async Task Extract_FindsCallEdges()
    {
        var result = ExtractFixture("sample.py");

        // forward calls _attention
        var calls = result.Edges.Where(e => e.Relation == "calls").ToList();
        await Assert.That(calls).IsNotEmpty();
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.py");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task Extract_NodesHaveSourceLocations()
    {
        var result = ExtractFixture("sample.py");

        await Assert.That(result.Nodes).HasCount().GreaterThan(0);
        foreach (var node in result.Nodes)
        {
            await Assert.That(node.SourceLocation).IsNotNull();
            await Assert.That(node.SourceLocation!).StartsWith("L");
        }
    }

    [Test]
    public async Task Extract_AllEdgesHaveValidConfidence()
    {
        var result = ExtractFixture("sample.py");

        foreach (var edge in result.Edges)
        {
            await Assert.That(new[] { "EXTRACTED", "INFERRED", "AMBIGUOUS" }).Contains(edge.ConfidenceString);
        }
    }

    [Test]
    public async Task Extract_NoDuplicateNodeIds()
    {
        var result = ExtractFixture("sample.py");
        var ids = result.Nodes.Select(n => n.Id).ToList();
        var distinct = ids.Distinct().ToList();

        await Assert.That(ids.Count).IsEqualTo(distinct.Count);
    }

    [Test]
    public async Task SupportedExtensions_IncludesPy()
    {
        var extractor = new PythonExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".py");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "PythonExtractorTests"`
Expected: Compilation error — `PythonExtractor` does not exist

- [ ] **Step 5: Implement PythonExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/PythonExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class PythonExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".py", ".pyw", ".pyi" };

    public override string TreeSitterLanguage => "python";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_definition" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_definition" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_statement", "import_from_statement" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        // Python class/function definitions have a 'name' child of type 'identifier'
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        if (node.Type == "import_statement")
        {
            // import foo / import foo.bar
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "dotted_name")
                    return child.Text.Replace(".", "::");
            }
        }
        else if (node.Type == "import_from_statement")
        {
            // from foo import bar
            string? module = null;
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "dotted_name" || child.Type == "relative_import")
                {
                    module = child.Text.Replace(".", "::");
                    break;
                }
            }
            return module;
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        // call node structure: (call function: (...) arguments: (...))
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.FieldName == "function" || i == 0)
            {
                if (child.Type == "identifier")
                    return child.Text;
                if (child.Type == "attribute")
                {
                    // self.method() or obj.method() — extract the attribute name
                    for (int j = 0; j < child.ChildCount; j++)
                    {
                        var attr = child.Child(j)!;
                        if (attr.Type == "identifier" && j == child.ChildCount - 1)
                            return attr.Text;
                    }
                }
                break;
            }
        }
        return null;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "PythonExtractorTests"`
Expected: All 8 tests pass

- [ ] **Step 7: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/PythonExtractor.cs tests/Graphiphy.Tests/Extraction/ tests/Graphiphy.Tests/Fixtures/sample.py
git commit -m "feat: add Python extractor with tree-sitter AST walking"
```

---

### Task 9: JavaScript Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.js`
- Create: `tests/Graphiphy.Tests/Extraction/JavaScriptExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/JavaScriptExtractor.cs`

- [ ] **Step 1: Create JavaScript fixture**

```javascript
// tests/Graphiphy.Tests/Fixtures/sample.js
const fs = require('fs');
import { Parser } from './parser';

class EventEmitter {
    constructor() {
        this.listeners = {};
    }

    on(event, callback) {
        this.listeners[event] = callback;
    }

    emit(event, data) {
        const cb = this.listeners[event];
        if (cb) cb(data);
    }
}

function createServer(config) {
    const emitter = new EventEmitter();
    emitter.on('request', handleRequest);
    return emitter;
}

function handleRequest(req) {
    fs.readFileSync(req.path);
}

module.exports = { EventEmitter, createServer };
```

- [ ] **Step 2: Write JavaScript extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/JavaScriptExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class JavaScriptExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new JavaScriptExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.js");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("EventEmitter");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.js");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("createServer");
        await Assert.That(labels).Contains("handleRequest");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.js");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("on");
        await Assert.That(labels).Contains("emit");
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.js");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task Extract_FindsCallEdges()
    {
        var result = ExtractFixture("sample.js");
        var calls = result.Edges.Where(e => e.Relation == "calls").ToList();

        await Assert.That(calls).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions_IncludesJsAndJsx()
    {
        var extractor = new JavaScriptExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".js");
        await Assert.That(extractor.SupportedExtensions).Contains(".jsx");
    }
}
```

- [ ] **Step 3: Implement JavaScriptExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/JavaScriptExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class JavaScriptExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".js", ".jsx", ".mjs", ".cjs" };

    public override string TreeSitterLanguage => "javascript";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "class" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_declaration", "method_definition", "arrow_function" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_statement", "call_expression" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        // class_declaration/function_declaration have 'name' field
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "identifier" || child.Type == "property_identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        if (node.Type == "import_statement")
        {
            // import { X } from 'module'
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "string" || child.Type == "string_fragment")
                    return child.Text.Trim('\'', '"').Replace("/", "::");
            }
        }
        else if (node.Type == "call_expression")
        {
            // require('module')
            var fn = node.Child(0);
            if (fn?.Type == "identifier" && fn.Text == "require" && node.ChildCount > 1)
            {
                var args = node.Child(1);
                if (args is not null)
                {
                    for (int i = 0; i < args.ChildCount; i++)
                    {
                        var arg = args.Child(i)!;
                        if (arg.Type == "string" || arg.Type == "string_fragment")
                            return arg.Text.Trim('\'', '"', '(', ')').Replace("/", "::");
                    }
                }
            }
            return null; // Not a require call — let CallNodeTypes handle it
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        // Skip require() calls — handled by ImportNodeTypes
        if (fn.Type == "identifier" && fn.Text == "require")
            return null;

        if (fn.Type == "identifier")
            return fn.Text;

        if (fn.Type == "member_expression")
        {
            // obj.method() — extract method name
            var prop = fn.Child(fn.ChildCount - 1);
            if (prop?.Type == "property_identifier")
                return prop.Text;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "JavaScriptExtractorTests"`
Expected: All 6 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/JavaScriptExtractor.cs tests/Graphiphy.Tests/Extraction/JavaScriptExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.js
git commit -m "feat: add JavaScript extractor"
```

---

### Task 10: TypeScript Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.ts`
- Create: `tests/Graphiphy.Tests/Extraction/TypeScriptExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/TypeScriptExtractor.cs`

- [ ] **Step 1: Create TypeScript fixture**

```typescript
// tests/Graphiphy.Tests/Fixtures/sample.ts
import { EventEmitter } from 'events';

interface Config {
    port: number;
    host: string;
}

class HttpServer extends EventEmitter {
    private config: Config;

    constructor(config: Config) {
        super();
        this.config = config;
    }

    listen(): void {
        this.emit('listening', this.config.port);
    }

    handleRequest(path: string): Response {
        return new Response(200, path);
    }
}

class Response {
    constructor(public status: number, public body: string) {}
}

export function createApp(config: Config): HttpServer {
    const server = new HttpServer(config);
    server.listen();
    return server;
}
```

- [ ] **Step 2: Write TypeScript extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/TypeScriptExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class TypeScriptExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new TypeScriptExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("HttpServer");
        await Assert.That(labels).Contains("Response");
    }

    [Test]
    public async Task Extract_FindsInterfaces()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Config");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("createApp");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("listen");
        await Assert.That(labels).Contains("handleRequest");
    }

    [Test]
    public async Task SupportedExtensions_IncludesTsAndTsx()
    {
        var extractor = new TypeScriptExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".ts");
        await Assert.That(extractor.SupportedExtensions).Contains(".tsx");
    }
}
```

- [ ] **Step 3: Implement TypeScriptExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/TypeScriptExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class TypeScriptExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".ts", ".tsx", ".mts", ".cts" };

    public override string TreeSitterLanguage => "typescript";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "interface_declaration", "type_alias_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_declaration", "method_definition", "arrow_function" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_statement" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression", "new_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "type_identifier" || child.Type == "identifier" || child.Type == "property_identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "string" || child.Type == "string_fragment")
                return child.Text.Trim('\'', '"').Replace("/", "::");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        if (fn.Type == "identifier")
            return fn.Text;

        if (fn.Type == "member_expression")
        {
            var prop = fn.Child(fn.ChildCount - 1);
            if (prop?.Type == "property_identifier")
                return prop.Text;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "TypeScriptExtractorTests"`
Expected: All 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/TypeScriptExtractor.cs tests/Graphiphy.Tests/Extraction/TypeScriptExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.ts
git commit -m "feat: add TypeScript extractor"
```

---

### Task 11: C Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.c`
- Create: `tests/Graphiphy.Tests/Extraction/CExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/CExtractor.cs`

- [ ] **Step 1: Create C fixture**

```c
// tests/Graphiphy.Tests/Fixtures/sample.c
#include <stdio.h>
#include <stdlib.h>
#include "utils.h"

typedef struct {
    int x;
    int y;
} Point;

typedef struct Node {
    int value;
    struct Node* next;
} Node;

void print_point(Point* p) {
    printf("(%d, %d)\n", p->x, p->y);
}

Node* create_node(int value) {
    Node* n = malloc(sizeof(Node));
    n->value = value;
    n->next = NULL;
    return n;
}

void push(Node** head, int value) {
    Node* n = create_node(value);
    n->next = *head;
    *head = n;
}

int main(int argc, char** argv) {
    Point p = {1, 2};
    print_point(&p);

    Node* list = NULL;
    push(&list, 10);
    push(&list, 20);
    return 0;
}
```

- [ ] **Step 2: Write C extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/CExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class CExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new CExtractor();

    [Test]
    public async Task Extract_FindsStructs()
    {
        var result = ExtractFixture("sample.c");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Point");
        await Assert.That(labels).Contains("Node");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.c");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("print_point");
        await Assert.That(labels).Contains("create_node");
        await Assert.That(labels).Contains("push");
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task Extract_FindsIncludes()
    {
        var result = ExtractFixture("sample.c");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task Extract_FindsCallEdges()
    {
        var result = ExtractFixture("sample.c");
        var calls = result.Edges.Where(e => e.Relation == "calls").ToList();

        await Assert.That(calls).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions_IncludesCAndH()
    {
        var extractor = new CExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".c");
        await Assert.That(extractor.SupportedExtensions).Contains(".h");
    }
}
```

- [ ] **Step 3: Implement CExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/CExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class CExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".c", ".h" };

    public override string TreeSitterLanguage => "c";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "struct_specifier", "enum_specifier", "type_definition" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_definition" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "preproc_include" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "type_definition")
        {
            // typedef struct { ... } Name;  — name is the last identifier child
            for (int i = node.ChildCount - 1; i >= 0; i--)
            {
                var child = node.Child(i)!;
                if (child.Type == "type_identifier")
                    return child.Text;
            }
            return null;
        }

        // struct_specifier: struct Name { ... }
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "type_identifier" || child.Type == "identifier")
                return child.Text;
        }

        // function_definition: return_type name(params) { ... }
        if (nodeType == "function_definition")
        {
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "function_declarator")
                {
                    for (int j = 0; j < child.ChildCount; j++)
                    {
                        var fc = child.Child(j)!;
                        if (fc.Type == "identifier")
                            return fc.Text;
                    }
                }
            }
        }

        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        // #include "header.h" or #include <header.h>
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "string_literal" || child.Type == "system_lib_string")
                return child.Text.Trim('"', '<', '>').Replace("/", "::").Replace(".h", "");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        if (fn.Type == "identifier")
            return fn.Text;

        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "CExtractorTests"`
Expected: All 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/CExtractor.cs tests/Graphiphy.Tests/Extraction/CExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.c
git commit -m "feat: add C extractor"
```

---

### Task 12: C++ Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.cpp`
- Create: `tests/Graphiphy.Tests/Extraction/CppExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/CppExtractor.cs`

- [ ] **Step 1: Create C++ fixture**

```cpp
// tests/Graphiphy.Tests/Fixtures/sample.cpp
#include <iostream>
#include <vector>
#include "graph.h"

namespace algorithms {

class Graph {
public:
    Graph(int vertices);
    void addEdge(int u, int v);
    std::vector<int> bfs(int start);

private:
    int vertices_;
    std::vector<std::vector<int>> adj_;
};

Graph::Graph(int vertices) : vertices_(vertices), adj_(vertices) {}

void Graph::addEdge(int u, int v) {
    adj_[u].push_back(v);
    adj_[v].push_back(u);
}

std::vector<int> Graph::bfs(int start) {
    std::vector<int> result;
    // simplified
    return result;
}

template<typename T>
class Stack {
public:
    void push(T item);
    T pop();
    bool empty() const;
};

} // namespace algorithms

int main() {
    algorithms::Graph g(5);
    g.addEdge(0, 1);
    g.addEdge(1, 2);
    auto result = g.bfs(0);
    std::cout << result.size() << std::endl;
    return 0;
}
```

- [ ] **Step 2: Write C++ extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/CppExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class CppExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new CppExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.cpp");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Graph");
        await Assert.That(labels).Contains("Stack");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.cpp");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("addEdge");
        await Assert.That(labels).Contains("bfs");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.cpp");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task Extract_FindsIncludes()
    {
        var result = ExtractFixture("sample.cpp");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions_IncludesCpp()
    {
        var extractor = new CppExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".cpp");
        await Assert.That(extractor.SupportedExtensions).Contains(".hpp");
        await Assert.That(extractor.SupportedExtensions).Contains(".cc");
    }
}
```

- [ ] **Step 3: Implement CppExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/CppExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class CppExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".cpp", ".hpp", ".cc", ".cxx", ".hxx", ".h" };

    public override string TreeSitterLanguage => "cpp";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_specifier", "struct_specifier", "template_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_definition", "declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "preproc_include" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "template_declaration")
        {
            // template<...> class Name { ... }
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "class_specifier" || child.Type == "struct_specifier")
                    return ExtractName(child, child.Type);
            }
            return null;
        }

        if (nodeType == "function_definition")
        {
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "function_declarator")
                {
                    for (int j = 0; j < child.ChildCount; j++)
                    {
                        var fc = child.Child(j)!;
                        if (fc.Type == "identifier" || fc.Type == "field_identifier")
                            return fc.Text;
                        if (fc.Type == "qualified_identifier" || fc.Type == "scoped_identifier")
                        {
                            // Class::method — return the method name
                            var last = fc.Child(fc.ChildCount - 1);
                            return last?.Text;
                        }
                    }
                }
            }
            return null;
        }

        // class_specifier / struct_specifier
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "type_identifier" || child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "string_literal" || child.Type == "system_lib_string")
                return child.Text.Trim('"', '<', '>').Replace("/", "::").Replace(".h", "").Replace(".hpp", "");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        if (fn.Type == "identifier")
            return fn.Text;

        if (fn.Type == "field_expression" || fn.Type == "member_expression")
        {
            var prop = fn.Child(fn.ChildCount - 1);
            if (prop?.Type == "field_identifier" || prop?.Type == "identifier")
                return prop.Text;
        }

        if (fn.Type == "qualified_identifier" || fn.Type == "scoped_identifier")
        {
            var last = fn.Child(fn.ChildCount - 1);
            return last?.Text;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "CppExtractorTests"`
Expected: All 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/CppExtractor.cs tests/Graphiphy.Tests/Extraction/CppExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.cpp
git commit -m "feat: add C++ extractor"
```

---

### Task 13: C# Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.cs`
- Create: `tests/Graphiphy.Tests/Extraction/CSharpExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/CSharpExtractor.cs`

- [ ] **Step 1: Create C# fixture**

```csharp
// tests/Graphiphy.Tests/Fixtures/sample.cs
using System;
using System.Collections.Generic;

namespace SampleApp.Models
{
    public interface IRepository<T>
    {
        T GetById(int id);
        void Save(T entity);
    }

    public class UserRepository : IRepository<User>
    {
        private readonly DbContext _context;

        public UserRepository(DbContext context)
        {
            _context = context;
        }

        public User GetById(int id)
        {
            return _context.Find<User>(id);
        }

        public void Save(User entity)
        {
            _context.Add(entity);
            _context.SaveChanges();
        }

        private void Validate(User user)
        {
            if (string.IsNullOrEmpty(user.Name))
                throw new ArgumentException("Name required");
        }
    }

    public record User(int Id, string Name, string Email);
}
```

- [ ] **Step 2: Write C# extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/CSharpExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class CSharpExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new CSharpExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("UserRepository");
    }

    [Test]
    public async Task Extract_FindsInterfaces()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("IRepository");
    }

    [Test]
    public async Task Extract_FindsRecords()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("User");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("GetById");
        await Assert.That(labels).Contains("Save");
        await Assert.That(labels).Contains("Validate");
    }

    [Test]
    public async Task Extract_FindsUsings()
    {
        var result = ExtractFixture("sample.cs");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new CSharpExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".cs");
    }
}
```

- [ ] **Step 3: Implement CSharpExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/CSharpExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class CSharpExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".cs" };

    public override string TreeSitterLanguage => "c_sharp";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "interface_declaration", "struct_declaration",
                              "record_declaration", "enum_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "method_declaration", "constructor_declaration", "property_declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "using_directive" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "invocation_expression", "object_creation_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "identifier" || child.Type == "generic_name")
            {
                // generic_name: IRepository<T> — just take the name part
                if (child.Type == "generic_name")
                {
                    for (int j = 0; j < child.ChildCount; j++)
                    {
                        var gc = child.Child(j)!;
                        if (gc.Type == "identifier")
                            return gc.Text;
                    }
                }
                return child.Text;
            }
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        // using System.Collections.Generic;
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "qualified_name" || child.Type == "identifier_name" || child.Type == "identifier")
                return child.Text.Replace(".", "::");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        if (fn.Type == "identifier")
            return fn.Text;

        if (fn.Type == "member_access_expression")
        {
            var name = fn.Child(fn.ChildCount - 1);
            if (name?.Type == "identifier" || name?.Type == "generic_name")
                return name.Text;
        }

        // object_creation_expression: new Foo(...)
        if (node.Type == "object_creation_expression")
        {
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "identifier" || child.Type == "generic_name")
                    return child.Text;
            }
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "CSharpExtractorTests"`
Expected: All 6 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/CSharpExtractor.cs tests/Graphiphy.Tests/Extraction/CSharpExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.cs
git commit -m "feat: add C# extractor"
```

---

### Task 14: Java Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.java`
- Create: `tests/Graphiphy.Tests/Extraction/JavaExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/JavaExtractor.cs`

- [ ] **Step 1: Create Java fixture**

```java
// tests/Graphiphy.Tests/Fixtures/sample.java
package com.example.service;

import java.util.List;
import java.util.ArrayList;
import com.example.model.User;

public class UserService {
    private final UserRepository repository;

    public UserService(UserRepository repository) {
        this.repository = repository;
    }

    public User findUser(int id) {
        return repository.findById(id);
    }

    public List<User> listAll() {
        return new ArrayList<>(repository.findAll());
    }

    private void validate(User user) {
        if (user.getName() == null) {
            throw new IllegalArgumentException("Name required");
        }
    }
}

interface UserRepository {
    User findById(int id);
    List<User> findAll();
}
```

- [ ] **Step 2: Write Java extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/JavaExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class JavaExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new JavaExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.java");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("UserService");
    }

    [Test]
    public async Task Extract_FindsInterfaces()
    {
        var result = ExtractFixture("sample.java");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("UserRepository");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.java");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("findUser");
        await Assert.That(labels).Contains("listAll");
        await Assert.That(labels).Contains("validate");
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.java");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new JavaExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".java");
    }
}
```

- [ ] **Step 3: Implement JavaExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/JavaExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class JavaExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".java" };

    public override string TreeSitterLanguage => "java";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "interface_declaration", "enum_declaration", "record_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "method_declaration", "constructor_declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_declaration" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "method_invocation", "object_creation_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        // import com.example.Foo;
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "scoped_identifier" || child.Type == "identifier")
                return child.Text.Replace(".", "::");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        if (node.Type == "object_creation_expression")
        {
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "type_identifier" || child.Type == "identifier" || child.Type == "generic_type")
                    return child.Type == "generic_type" ? child.Child(0)?.Text : child.Text;
            }
            return null;
        }

        // method_invocation: object.method(args) or method(args)
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "identifier")
                return child.Text;
            if (child.Type == "field_access" || child.Type == "method_invocation")
            {
                var last = child.Child(child.ChildCount - 1);
                if (last?.Type == "identifier")
                    return last.Text;
            }
        }
        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "JavaExtractorTests"`
Expected: All 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/JavaExtractor.cs tests/Graphiphy.Tests/Extraction/JavaExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.java
git commit -m "feat: add Java extractor"
```

---

### Task 15: Go Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.go`
- Create: `tests/Graphiphy.Tests/Extraction/GoExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/GoExtractor.cs`

- [ ] **Step 1: Create Go fixture**

```go
// tests/Graphiphy.Tests/Fixtures/sample.go
package main

import (
	"fmt"
	"net/http"
)

type Server struct {
	Port   int
	Router *Router
}

type Router struct {
	routes map[string]http.HandlerFunc
}

func NewServer(port int) *Server {
	r := NewRouter()
	return &Server{Port: port, Router: r}
}

func NewRouter() *Router {
	return &Router{routes: make(map[string]http.HandlerFunc)}
}

func (s *Server) Start() error {
	addr := fmt.Sprintf(":%d", s.Port)
	return http.ListenAndServe(addr, nil)
}

func (r *Router) Handle(path string, handler http.HandlerFunc) {
	r.routes[path] = handler
}

func main() {
	s := NewServer(8080)
	s.Router.Handle("/", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "Hello")
	})
	s.Start()
}
```

- [ ] **Step 2: Write Go extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/GoExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class GoExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new GoExtractor();

    [Test]
    public async Task Extract_FindsStructs()
    {
        var result = ExtractFixture("sample.go");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Server");
        await Assert.That(labels).Contains("Router");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.go");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("NewServer");
        await Assert.That(labels).Contains("NewRouter");
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.go");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Start");
        await Assert.That(labels).Contains("Handle");
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.go");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new GoExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".go");
    }
}
```

- [ ] **Step 3: Implement GoExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/GoExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class GoExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".go" };

    public override string TreeSitterLanguage => "go";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "type_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_declaration", "method_declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_declaration", "import_spec" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "type_declaration")
        {
            // type_declaration contains type_spec children
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "type_spec")
                {
                    for (int j = 0; j < child.ChildCount; j++)
                    {
                        var tc = child.Child(j)!;
                        if (tc.Type == "type_identifier")
                            return tc.Text;
                    }
                }
            }
            return null;
        }

        if (nodeType == "method_declaration")
        {
            // func (r *Router) Handle(...) — name is the field_identifier
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "field_identifier")
                    return child.Text;
            }
            return null;
        }

        // function_declaration
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        if (node.Type == "import_spec")
        {
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "interpreted_string_literal")
                    return child.Text.Trim('"').Replace("/", "::");
            }
        }
        else if (node.Type == "import_declaration")
        {
            // Single import without parens
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i)!;
                if (child.Type == "import_spec")
                    return ExtractImportTarget(child);
                if (child.Type == "import_spec_list")
                {
                    // Multiple imports — take first only (others handled recursively)
                    for (int j = 0; j < child.ChildCount; j++)
                    {
                        var spec = child.Child(j)!;
                        if (spec.Type == "import_spec")
                            return ExtractImportTarget(spec);
                    }
                }
            }
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        if (fn.Type == "identifier")
            return fn.Text;

        if (fn.Type == "selector_expression")
        {
            var field = fn.Child(fn.ChildCount - 1);
            if (field?.Type == "field_identifier")
                return field.Text;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "GoExtractorTests"`
Expected: All 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/GoExtractor.cs tests/Graphiphy.Tests/Extraction/GoExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.go
git commit -m "feat: add Go extractor"
```

---

### Task 16: Rust Extractor (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Fixtures/sample.rs`
- Create: `tests/Graphiphy.Tests/Extraction/RustExtractorTests.cs`
- Create: `src/Graphiphy/Extraction/Extractors/RustExtractor.cs`

- [ ] **Step 1: Create Rust fixture**

```rust
// tests/Graphiphy.Tests/Fixtures/sample.rs
use std::collections::HashMap;
use std::io::Read;

pub struct Config {
    pub port: u16,
    pub host: String,
}

pub trait Handler {
    fn handle(&self, request: &Request) -> Response;
}

pub struct Router {
    routes: HashMap<String, Box<dyn Handler>>,
}

impl Router {
    pub fn new() -> Self {
        Router { routes: HashMap::new() }
    }

    pub fn add_route(&mut self, path: &str, handler: Box<dyn Handler>) {
        self.routes.insert(path.to_string(), handler);
    }

    pub fn dispatch(&self, request: &Request) -> Response {
        match self.routes.get(&request.path) {
            Some(handler) => handler.handle(request),
            None => Response::not_found(),
        }
    }
}

fn main() {
    let mut router = Router::new();
    let config = Config { port: 8080, host: "localhost".to_string() };
    println!("Starting on {}:{}", config.host, config.port);
}
```

- [ ] **Step 2: Write Rust extractor tests**

```csharp
// tests/Graphiphy.Tests/Extraction/RustExtractorTests.cs
using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class RustExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new RustExtractor();

    [Test]
    public async Task Extract_FindsStructs()
    {
        var result = ExtractFixture("sample.rs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Config");
        await Assert.That(labels).Contains("Router");
    }

    [Test]
    public async Task Extract_FindsTraits()
    {
        var result = ExtractFixture("sample.rs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Handler");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.rs");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("main");
        await Assert.That(labels).Contains("new");
        await Assert.That(labels).Contains("add_route");
        await Assert.That(labels).Contains("dispatch");
    }

    [Test]
    public async Task Extract_FindsUseImports()
    {
        var result = ExtractFixture("sample.rs");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new RustExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".rs");
    }
}
```

- [ ] **Step 3: Implement RustExtractor**

```csharp
// src/Graphiphy/Extraction/Extractors/RustExtractor.cs
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class RustExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".rs" };

    public override string TreeSitterLanguage => "rust";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "struct_item", "enum_item", "trait_item" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_item", "impl_item" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "use_declaration" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "impl_item")
        {
            // impl blocks aren't named — skip and walk children for function_items
            return null;
        }

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "type_identifier" || child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        // use std::collections::HashMap;
        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i)!;
            if (child.Type == "scoped_identifier" || child.Type == "identifier" || child.Type == "use_wildcard")
                return child.Text.Replace("::", "::");
            if (child.Type == "scoped_use_list" || child.Type == "use_list")
                return child.Text.Split('{')[0].TrimEnd(':');
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var fn = node.Child(0);
        if (fn is null) return null;

        if (fn.Type == "identifier")
            return fn.Text;

        if (fn.Type == "scoped_identifier" || fn.Type == "field_expression")
        {
            var last = fn.Child(fn.ChildCount - 1);
            if (last?.Type == "identifier" || last?.Type == "field_identifier")
                return last.Text;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "RustExtractorTests"`
Expected: All 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Extraction/Extractors/RustExtractor.cs tests/Graphiphy.Tests/Extraction/RustExtractorTests.cs tests/Graphiphy.Tests/Fixtures/sample.rs
git commit -m "feat: add Rust extractor"
```

---

### Task 17: Register All Extractors in LanguageRegistry

**Files:**
- Modify: `src/Graphiphy/Extraction/LanguageRegistry.cs`

- [ ] **Step 1: Update CreateDefault to register all 9 extractors**

```csharp
    public static LanguageRegistry CreateDefault()
    {
        var registry = new LanguageRegistry();
        registry.Register(new Extractors.PythonExtractor());
        registry.Register(new Extractors.JavaScriptExtractor());
        registry.Register(new Extractors.TypeScriptExtractor());
        registry.Register(new Extractors.CExtractor());
        registry.Register(new Extractors.CppExtractor());
        registry.Register(new Extractors.CSharpExtractor());
        registry.Register(new Extractors.JavaExtractor());
        registry.Register(new Extractors.GoExtractor());
        registry.Register(new Extractors.RustExtractor());
        return registry;
    }
```

- [ ] **Step 2: Run full test suite**

Run: `dotnet test tests/Graphiphy.Tests/`
Expected: All extraction tests pass

- [ ] **Step 3: Commit**

```bash
git add src/Graphiphy/Extraction/LanguageRegistry.cs
git commit -m "feat: register all 9 language extractors in default registry"
```

---

## Phase 4: Graph Building

### Task 18: Graph Builder (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Build/GraphBuilderTests.cs`
- Create: `src/Graphiphy/Build/GraphBuilder.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Build/GraphBuilderTests.cs
using Graphiphy.Build;
using Graphiphy.Models;

namespace Graphiphy.Tests.Build;

public class GraphBuilderTests
{
    [Test]
    public async Task BuildFromExtraction_CreatesNodesAndEdges()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
                new Node { Id = "a::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "a.py" },
            ],
            Edges =
            [
                new Edge { Source = "a::Foo", Target = "a::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
            ]
        };

        var graph = GraphBuilder.Build([extraction]);

        await Assert.That(graph.VertexCount).IsEqualTo(2);
        await Assert.That(graph.EdgeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_MergesMultipleExtractions()
    {
        var ext1 = new Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = []
        };
        var ext2 = new Extraction
        {
            Nodes = [new Node { Id = "b::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "b.py" }],
            Edges = [new Edge { Source = "a::Foo", Target = "b::Bar", Relation = "imports", ConfidenceString = "EXTRACTED", SourceFile = "b.py" }]
        };

        var graph = GraphBuilder.Build([ext1, ext2]);

        await Assert.That(graph.VertexCount).IsEqualTo(2);
        await Assert.That(graph.EdgeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_DeduplicatesNodes()
    {
        var ext1 = new Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = []
        };
        var ext2 = new Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = []
        };

        var graph = GraphBuilder.Build([ext1, ext2]);

        await Assert.That(graph.VertexCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_NormalizesBackslashPaths()
    {
        var extraction = new Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = @"src\foo\a.py" }],
            Edges = []
        };

        var graph = GraphBuilder.Build([extraction]);
        var node = graph.Vertices.First();

        await Assert.That(node.SourceFile).IsEqualTo("src/foo/a.py");
    }

    [Test]
    public async Task Build_DefaultsNullFileTypeToConcept()
    {
        var extraction = new Extraction
        {
            Nodes = [new Node { Id = "a::X", Label = "X", FileTypeString = null!, SourceFile = "a.py" }],
            Edges = []
        };

        var graph = GraphBuilder.Build([extraction]);
        var node = graph.Vertices.First();

        await Assert.That(node.FileTypeString).IsEqualTo("concept");
    }

    [Test]
    public async Task Build_DropsDanglingEdgesSilently()
    {
        var extraction = new Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = [new Edge { Source = "a::Foo", Target = "external::Bar", Relation = "imports", ConfidenceString = "INFERRED", SourceFile = "a.py" }]
        };

        var graph = GraphBuilder.Build([extraction]);

        // Dangling edge (target not in nodes) is dropped
        await Assert.That(graph.EdgeCount).IsEqualTo(0);
    }

    [Test]
    public async Task ToGraphData_RoundTrips()
    {
        var extraction = new Extraction
        {
            Nodes =
            [
                new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
                new Node { Id = "a::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "a.py" },
            ],
            Edges =
            [
                new Edge { Source = "a::Foo", Target = "a::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
            ]
        };

        var graph = GraphBuilder.Build([extraction]);
        var data = GraphBuilder.ToGraphData(graph);

        await Assert.That(data.Nodes.Count).IsEqualTo(2);
        await Assert.That(data.Edges.Count).IsEqualTo(1);
        await Assert.That(data.Edges[0].Source).IsEqualTo("a::Foo");
        await Assert.That(data.Edges[0].Target).IsEqualTo("a::Bar");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "GraphBuilderTests"`
Expected: Compilation error

- [ ] **Step 3: Implement GraphBuilder**

```csharp
// src/Graphiphy/Build/GraphBuilder.cs
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Build;

public static class GraphBuilder
{
    public static BidirectionalGraph<Node, TaggedEdge<Node, Edge>> Build(IEnumerable<Extraction> extractions)
    {
        var graph = new BidirectionalGraph<Node, TaggedEdge<Node, Edge>>();
        var nodeIndex = new Dictionary<string, Node>();

        foreach (var extraction in extractions)
        {
            foreach (var node in extraction.Nodes)
            {
                // Normalize path separators
                node.SourceFile = node.SourceFile?.Replace('\\', '/') ?? "";

                // Default null file_type to concept
                if (string.IsNullOrEmpty(node.FileTypeString))
                    node.FileTypeString = "concept";

                if (!nodeIndex.ContainsKey(node.Id))
                {
                    nodeIndex[node.Id] = node;
                    graph.AddVertex(node);
                }
            }

            foreach (var edge in extraction.Edges)
            {
                edge.SourceFile = edge.SourceFile?.Replace('\\', '/') ?? "";

                // Drop dangling edges silently (external imports are expected)
                if (!nodeIndex.ContainsKey(edge.Source) || !nodeIndex.ContainsKey(edge.Target))
                    continue;

                var source = nodeIndex[edge.Source];
                var target = nodeIndex[edge.Target];
                var taggedEdge = new TaggedEdge<Node, Edge>(source, target, edge);
                graph.AddEdge(taggedEdge);
            }
        }

        return graph;
    }

    public static GraphData ToGraphData(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph)
    {
        return new GraphData
        {
            Nodes = graph.Vertices.ToList(),
            Edges = graph.Edges.Select(e => e.Tag).ToList()
        };
    }

    public static BidirectionalGraph<Node, TaggedEdge<Node, Edge>> FromGraphData(GraphData data)
    {
        var extractions = new[] { new Extraction { Nodes = data.Nodes, Edges = data.Edges } };
        return Build(extractions);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "GraphBuilderTests"`
Expected: All 7 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Build/ tests/Graphiphy.Tests/Build/
git commit -m "feat: add graph builder with QuikGraph"
```

---

## Phase 5: Deduplication

### Task 19: Entity Deduplicator (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Dedup/EntityDeduplicatorTests.cs`
- Create: `src/Graphiphy/Dedup/UnionFind.cs`
- Create: `src/Graphiphy/Dedup/EntityDeduplicator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Dedup/EntityDeduplicatorTests.cs
using Graphiphy.Dedup;
using Graphiphy.Models;

namespace Graphiphy.Tests.Dedup;

public class EntityDeduplicatorTests
{
    [Test]
    public async Task ExactDuplicates_AreMerged()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::foo", Label = "foo", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>
        {
            new() { Source = "a::Foo", Target = "b::foo", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
        };

        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LowEntropy_NotDeduped()
    {
        // Short single-character labels have low entropy — skip MinHash comparison
        var nodes = new List<Node>
        {
            new() { Id = "a::x", Label = "x", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::x", Label = "x", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>();

        var (dedupNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        // Low entropy labels still get exact-match merged
        await Assert.That(dedupNodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TypoMerging_JaroWinklerAboveThreshold()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::UserRepository", Label = "UserRepository", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::UserRepostory", Label = "UserRepostory", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>();

        var (dedupNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DifferentEntities_NotMerged()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::UserService", Label = "UserService", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::OrderService", Label = "OrderService", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>();

        var (dedupNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EdgeRewiring_AfterMerge()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::foo", Label = "foo", FileTypeString = "code", SourceFile = "b.py" },
            new() { Id = "c::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "c.py" },
        };
        var edges = new List<Edge>
        {
            new() { Source = "b::foo", Target = "c::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "b.py" }
        };

        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(2);
        await Assert.That(dedupEdges.Count).IsEqualTo(1);
        // Edge should be rewired to the winner node
        await Assert.That(dedupEdges[0].Source).IsEqualTo("a::Foo");
    }

    [Test]
    public async Task SelfLoops_DroppedAfterMerge()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::foo", Label = "foo", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>
        {
            new() { Source = "a::Foo", Target = "b::foo", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
        };

        var (_, dedupEdges) = EntityDeduplicator.Deduplicate(nodes, edges);

        // After merge, source==target → self-loop → dropped
        await Assert.That(dedupEdges.Count).IsEqualTo(0);
    }
}
```

- [ ] **Step 2: Implement UnionFind**

```csharp
// src/Graphiphy/Dedup/UnionFind.cs
namespace Graphiphy.Dedup;

internal sealed class UnionFind
{
    private readonly Dictionary<string, string> _parent = [];
    private readonly Dictionary<string, int> _rank = [];

    public string Find(string x)
    {
        if (!_parent.ContainsKey(x))
        {
            _parent[x] = x;
            _rank[x] = 0;
        }

        if (_parent[x] != x)
            _parent[x] = Find(_parent[x]); // path compression

        return _parent[x];
    }

    public void Union(string x, string y)
    {
        var rx = Find(x);
        var ry = Find(y);
        if (rx == ry) return;

        // Union by rank
        if (_rank[rx] < _rank[ry])
            _parent[rx] = ry;
        else if (_rank[rx] > _rank[ry])
            _parent[ry] = rx;
        else
        {
            _parent[ry] = rx;
            _rank[rx]++;
        }
    }

    public Dictionary<string, List<string>> Groups()
    {
        var groups = new Dictionary<string, List<string>>();
        foreach (var key in _parent.Keys)
        {
            var root = Find(key);
            if (!groups.TryGetValue(root, out var list))
            {
                list = [];
                groups[root] = list;
            }
            list.Add(key);
        }
        return groups;
    }
}
```

- [ ] **Step 3: Implement EntityDeduplicator**

```csharp
// src/Graphiphy/Dedup/EntityDeduplicator.cs
using F23.StringSimilarity;
using MinHashSharp;
using Graphiphy.Models;

namespace Graphiphy.Dedup;

public static class EntityDeduplicator
{
    private const double EntropyThreshold = 2.5;
    private const double LshThreshold = 0.7;
    private const double MergeThreshold = 0.92;
    private const int ShingleK = 3;

    public static (List<Node> Nodes, List<Edge> Edges) Deduplicate(List<Node> nodes, List<Edge> edges)
    {
        var uf = new UnionFind();

        // Index nodes by normalized label
        var byNorm = new Dictionary<string, List<Node>>();
        foreach (var node in nodes)
        {
            var norm = Normalize(node.Label);
            if (!byNorm.TryGetValue(norm, out var list))
            {
                list = [];
                byNorm[norm] = list;
            }
            list.Add(node);
        }

        // Pass 1: Exact normalization merging
        foreach (var (_, group) in byNorm)
        {
            if (group.Count < 2) continue;
            var winner = PickWinner(group);
            foreach (var n in group)
                uf.Union(winner.Id, n.Id);
        }

        // Pass 2: MinHash/LSH + Jaro-Winkler for high-entropy labels
        var highEntropy = nodes.Where(n => Entropy(n.Label) >= EntropyThreshold).ToList();
        if (highEntropy.Count > 1)
        {
            var lsh = new MinHashLSH(128, LshThreshold);
            var minhashes = new Dictionary<string, MinHash>();

            foreach (var node in highEntropy)
            {
                var shingles = Shingles(node.Label);
                var mh = new MinHash(128);
                foreach (var s in shingles)
                    mh.Add(s);
                minhashes[node.Id] = mh;
                lsh.Insert(node.Id, mh);
            }

            var jw = new JaroWinkler();
            var checked_ = new HashSet<(string, string)>();

            foreach (var node in highEntropy)
            {
                var candidates = lsh.Query(minhashes[node.Id]);
                foreach (var candidateId in candidates)
                {
                    if (candidateId == node.Id) continue;
                    var pair = (string.Compare(node.Id, candidateId, StringComparison.Ordinal) < 0)
                        ? (node.Id, candidateId) : (candidateId, node.Id);
                    if (!checked_.Add(pair)) continue;

                    var other = nodes.First(n => n.Id == candidateId);
                    var similarity = jw.Similarity(Normalize(node.Label), Normalize(other.Label));
                    if (similarity >= MergeThreshold)
                        uf.Union(node.Id, candidateId);
                }
            }
        }

        // Build remap table
        var remap = new Dictionary<string, string>();
        var groups = uf.Groups();
        var keptNodes = new Dictionary<string, Node>();

        foreach (var (root, members) in groups)
        {
            var memberNodes = members.Select(id => nodes.First(n => n.Id == id)).ToList();
            var winner = PickWinner(memberNodes);
            foreach (var id in members)
                remap[id] = winner.Id;
            keptNodes[winner.Id] = winner;
        }

        // Add nodes that were never in the UF (shouldn't happen, but safe)
        foreach (var node in nodes)
        {
            if (!remap.ContainsKey(node.Id))
            {
                remap[node.Id] = node.Id;
                keptNodes[node.Id] = node;
            }
        }

        // Rewire edges
        var dedupEdges = new List<Edge>();
        foreach (var edge in edges)
        {
            var newSource = remap.GetValueOrDefault(edge.Source, edge.Source);
            var newTarget = remap.GetValueOrDefault(edge.Target, edge.Target);

            // Drop self-loops
            if (newSource == newTarget) continue;

            dedupEdges.Add(new Edge
            {
                Source = newSource,
                Target = newTarget,
                Relation = edge.Relation,
                ConfidenceString = edge.ConfidenceString,
                SourceFile = edge.SourceFile,
                SourceLocation = edge.SourceLocation,
                Weight = edge.Weight,
                Context = edge.Context,
                ConfidenceScore = edge.ConfidenceScore,
            });
        }

        return (keptNodes.Values.ToList(), dedupEdges);
    }

    private static string Normalize(string label)
    {
        var chars = label.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();
        return new string(chars).Trim();
    }

    private static double Entropy(string s)
    {
        if (s.Length == 0) return 0;
        var freq = new Dictionary<char, int>();
        foreach (var c in s)
            freq[c] = freq.GetValueOrDefault(c) + 1;

        double entropy = 0;
        foreach (var count in freq.Values)
        {
            double p = (double)count / s.Length;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private static HashSet<string> Shingles(string label)
    {
        var norm = Normalize(label);
        var result = new HashSet<string>();
        for (int i = 0; i <= norm.Length - ShingleK; i++)
            result.Add(norm.Substring(i, ShingleK));
        return result;
    }

    private static Node PickWinner(IReadOnlyList<Node> candidates)
    {
        // Prefer: no chunk suffix, shorter ID, alphabetical
        return candidates
            .OrderBy(n => n.Id.Contains("__chunk") ? 1 : 0)
            .ThenBy(n => n.Id.Length)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .First();
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "EntityDeduplicatorTests"`
Expected: All 6 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy/Dedup/ tests/Graphiphy.Tests/Dedup/
git commit -m "feat: add entity deduplication with MinHash/LSH and Jaro-Winkler"
```

---

## Phase 6: Leiden Clustering

### Task 20: Native Leiden Interop Shim

**Files:**
- Create: `native/CMakeLists.txt`
- Create: `native/leiden_interop.h`
- Create: `native/leiden_interop.cpp`

This task uses the C shim code from the earlier Leiden wrapper plan. The full implementation is in the previous plan document — refer to Tasks 2–4 from that plan for the complete `leiden_interop.h`, `leiden_interop.cpp`, and `CMakeLists.txt` contents.

- [ ] **Step 1: Create `native/leiden_interop.h`** — (full header as specified in Phase 1 Leiden plan, Task 2)

- [ ] **Step 2: Create `native/leiden_interop.cpp`** — (full implementation as specified in Phase 1 Leiden plan, Task 3)

- [ ] **Step 3: Create `native/CMakeLists.txt`** — (as specified in Phase 1 Leiden plan, Task 4)

- [ ] **Step 4: Build native library**

```bash
cd /home/timm/graphiphy/native
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build
```

Expected: `native/build/lib/libleiden_interop.so` exists

- [ ] **Step 5: Verify exports**

Run: `nm -D native/build/lib/libleiden_interop.so | grep leiden_`
Expected: All expected symbols present

- [ ] **Step 6: Commit**

```bash
git add native/
git commit -m "feat: add native C interop shim for libleidenalg"
```

---

### Task 21: Leiden .NET Wrapper (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Cluster/LeidenClusteringTests.cs`
- Create: `src/Graphiphy/Cluster/NativeMethods.cs`
- Create: `src/Graphiphy/Cluster/NativeLibraryResolver.cs`
- Create: `src/Graphiphy/Cluster/LeidenClustering.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Cluster/LeidenClusteringTests.cs
using Graphiphy.Cluster;

namespace Graphiphy.Tests.Cluster;

public class LeidenClusteringTests
{
    [Test]
    public async Task Triangle_AllSameCommunity()
    {
        var edges = new (int, int)[] { (0, 1), (1, 2), (2, 0) };

        var result = LeidenClustering.FindCommunities(3, edges, PartitionType.Modularity);

        await Assert.That(result.Membership.Length).IsEqualTo(3);
        await Assert.That(result.Membership[0]).IsEqualTo(result.Membership[1]);
        await Assert.That(result.Membership[1]).IsEqualTo(result.Membership[2]);
    }

    [Test]
    public async Task TwoClusters_FindsTwo()
    {
        var edges = new (int, int)[]
        {
            (0, 1), (0, 2), (0, 3), (1, 2), (1, 3), (2, 3), // clique A
            (4, 5), (4, 6), (4, 7), (5, 6), (5, 7), (6, 7), // clique B
            (3, 4), // bridge
        };

        var result = LeidenClustering.FindCommunities(8, edges, PartitionType.CPM, resolution: 0.5, seed: 42);

        await Assert.That(result.NumCommunities).IsEqualTo(2);
        await Assert.That(result.Membership[0]).IsEqualTo(result.Membership[1]);
        await Assert.That(result.Membership[4]).IsEqualTo(result.Membership[5]);
        await Assert.That(result.Membership[0]).IsNotEqualTo(result.Membership[4]);
    }

    [Test]
    public async Task Deterministic_WithSeed()
    {
        var edges = new (int, int)[] { (0, 1), (1, 2), (2, 3), (3, 0), (0, 2) };

        var r1 = LeidenClustering.FindCommunities(4, edges, PartitionType.Modularity, seed: 99);
        var r2 = LeidenClustering.FindCommunities(4, edges, PartitionType.Modularity, seed: 99);

        await Assert.That(r1.Membership).IsEquivalentTo(r2.Membership);
    }
}
```

- [ ] **Step 2: Implement NativeMethods, NativeLibraryResolver, and LeidenClustering**

These follow the same pattern as Phase 1 Leiden plan Tasks 5–6. The `LeidenClustering` class uses the same `FindCommunities` static method pattern with `PartitionType` enum. Key files:

`src/Graphiphy/Cluster/NativeMethods.cs` — P/Invoke declarations (same as earlier plan Task 5)
`src/Graphiphy/Cluster/NativeLibraryResolver.cs` — DLL resolver (same as earlier plan Task 8)
`src/Graphiphy/Cluster/LeidenClustering.cs` — High-level API with `PartitionType` enum and `Result` record (same as earlier plan Task 6)

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "LeidenClusteringTests"`
Expected: All 3 tests pass

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy/Cluster/ tests/Graphiphy.Tests/Cluster/
git commit -m "feat: add Leiden clustering .NET wrapper"
```

---

## Phase 7: Analysis

### Task 22: Graph Analyzer (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Analysis/GraphAnalyzerTests.cs`
- Create: `src/Graphiphy/Analysis/GraphAnalyzer.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Analysis/GraphAnalyzerTests.cs
using Graphiphy.Analysis;
using Graphiphy.Build;
using Graphiphy.Models;

namespace Graphiphy.Tests.Analysis;

public class GraphAnalyzerTests
{
    private static Extraction MakeStarGraph(string hub, string[] spokes)
    {
        var nodes = new List<Node> { new() { Id = hub, Label = hub.Split("::").Last(), FileTypeString = "code", SourceFile = "a.py" } };
        var edges = new List<Edge>();
        foreach (var s in spokes)
        {
            nodes.Add(new Node { Id = s, Label = s.Split("::").Last(), FileTypeString = "code", SourceFile = "a.py" });
            edges.Add(new Edge { Source = hub, Target = s, Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" });
        }
        return new Extraction { Nodes = nodes, Edges = edges };
    }

    [Test]
    public async Task GodNodes_FindsMostConnected()
    {
        var ext = MakeStarGraph("a::Hub", ["a::S1", "a::S2", "a::S3", "a::S4", "a::S5"]);
        var graph = GraphBuilder.Build([ext]);

        var gods = GraphAnalyzer.GodNodes(graph, topN: 1);

        await Assert.That(gods.Count).IsEqualTo(1);
        await Assert.That(gods[0].Label).IsEqualTo("Hub");
    }

    [Test]
    public async Task SurprisingConnections_CrossFileEdges()
    {
        var ext = new Extraction
        {
            Nodes =
            [
                new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "b::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "b.py" },
            ],
            Edges =
            [
                new() { Source = "a::Foo", Target = "b::Bar", Relation = "calls", ConfidenceString = "AMBIGUOUS", SourceFile = "a.py" }
            ]
        };
        var graph = GraphBuilder.Build([ext]);

        var surprises = GraphAnalyzer.SurprisingConnections(graph, topN: 5);

        await Assert.That(surprises).IsNotEmpty();
    }

    [Test]
    public async Task GodNodes_ReturnsEmptyForEmptyGraph()
    {
        var ext = new Extraction { Nodes = [], Edges = [] };
        var graph = GraphBuilder.Build([ext]);

        var gods = GraphAnalyzer.GodNodes(graph, topN: 5);

        await Assert.That(gods).IsEmpty();
    }
}
```

- [ ] **Step 2: Implement GraphAnalyzer**

```csharp
// src/Graphiphy/Analysis/GraphAnalyzer.cs
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Analysis;

public static class GraphAnalyzer
{
    public static List<Node> GodNodes(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph, int topN = 5)
    {
        if (graph.VertexCount == 0) return [];

        return graph.Vertices
            .Where(n => n.FileTypeString == "code")
            .OrderByDescending(n => graph.Degree(n))
            .Take(topN)
            .ToList();
    }

    public record SurprisingConnection(Edge Edge, Node Source, Node Target, double Score);

    public static List<SurprisingConnection> SurprisingConnections(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph, int topN = 10)
    {
        var nodeById = graph.Vertices.ToDictionary(n => n.Id);
        var results = new List<SurprisingConnection>();

        foreach (var edge in graph.Edges)
        {
            var tag = edge.Tag;
            var source = edge.Source;
            var target = edge.Target;

            double score = SurpriseScore(tag, source, target);
            if (score > 0)
                results.Add(new SurprisingConnection(tag, source, target, score));
        }

        return results.OrderByDescending(s => s.Score).Take(topN).ToList();
    }

    private static double SurpriseScore(Edge edge, Node source, Node target)
    {
        double score = 0;

        // Cross-file bonus
        if (source.SourceFile != target.SourceFile)
            score += 1.0;

        // Confidence bonus (AMBIGUOUS > INFERRED > EXTRACTED)
        score += edge.Confidence switch
        {
            Confidence.Ambiguous => 2.0,
            Confidence.Inferred => 1.0,
            _ => 0.0,
        };

        // Cross file-type bonus
        if (source.FileTypeString != target.FileTypeString)
            score += 1.5;

        return score;
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "GraphAnalyzerTests"`
Expected: All 3 tests pass

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy/Analysis/ tests/Graphiphy.Tests/Analysis/
git commit -m "feat: add graph analyzer (god nodes, surprising connections)"
```

---

## Phase 8: Caching

### Task 23: Extraction Cache (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Cache/ExtractionCacheTests.cs`
- Create: `src/Graphiphy/Cache/ExtractionCache.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Cache/ExtractionCacheTests.cs
using Graphiphy.Cache;
using Graphiphy.Models;

namespace Graphiphy.Tests.Cache;

public class ExtractionCacheTests
{
    [Test]
    public async Task FileHash_IsDeterministic()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "test.py");
        File.WriteAllText(file, "class Foo: pass");

        var hash1 = ExtractionCache.FileHash(file, dir);
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task FileHash_ChangesWhenContentChanges()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "test.py");

        File.WriteAllText(file, "class Foo: pass");
        var hash1 = ExtractionCache.FileHash(file, dir);

        File.WriteAllText(file, "class Bar: pass");
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [Test]
    public async Task CacheRoundtrip_SaveAndLoad()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, "cache");
        var file = Path.Combine(dir, "test.py");
        File.WriteAllText(file, "class Foo: pass");

        var extraction = new Extraction
        {
            Nodes = [new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "test.py" }],
            Edges = []
        };

        var cache = new ExtractionCache(cacheDir);
        var hash = ExtractionCache.FileHash(file, dir);
        cache.Save(hash, extraction);

        var loaded = cache.Load(hash);

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Nodes.Count).IsEqualTo(1);
        await Assert.That(loaded.Nodes[0].Label).IsEqualTo("Foo");
    }

    [Test]
    public async Task Load_ReturnsNullForMissingHash()
    {
        var dir = CreateTempDir();
        var cache = new ExtractionCache(Path.Combine(dir, "cache"));

        var result = cache.Load("nonexistent_hash");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task MarkdownFrontmatter_IgnoredInHash()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "doc.md");

        File.WriteAllText(file, "---\ntitle: V1\n---\n# Hello\nBody text");
        var hash1 = ExtractionCache.FileHash(file, dir);

        File.WriteAllText(file, "---\ntitle: V2\ndate: today\n---\n# Hello\nBody text");
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task MarkdownBody_ChangesHash()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "doc.md");

        File.WriteAllText(file, "---\ntitle: X\n---\n# Hello\nBody text");
        var hash1 = ExtractionCache.FileHash(file, dir);

        File.WriteAllText(file, "---\ntitle: X\n---\n# Hello\nDifferent body");
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_cache_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 2: Implement ExtractionCache**

```csharp
// src/Graphiphy/Cache/ExtractionCache.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Graphiphy.Models;

namespace Graphiphy.Cache;

public sealed class ExtractionCache
{
    private readonly string _cacheDir;

    public ExtractionCache(string cacheDir)
    {
        _cacheDir = cacheDir;
    }

    public static string FileHash(string filePath, string rootDir)
    {
        var relativePath = Path.GetRelativePath(rootDir, filePath).Replace('\\', '/');
        var content = File.ReadAllText(filePath);

        // Strip markdown frontmatter before hashing
        if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            content = StripFrontmatter(content);

        var input = relativePath + "\n" + content;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    public void Save(string hash, Extraction extraction)
    {
        Directory.CreateDirectory(_cacheDir);
        var path = CachePath(hash);

        // Atomic write via temp file + rename
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(extraction, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    public Extraction? Load(string hash)
    {
        var path = CachePath(hash);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Extraction>(json);
    }

    public void Clear()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private string CachePath(string hash) => Path.Combine(_cacheDir, hash + ".json");

    private static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
            return content;

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return content;

        // Return everything after the closing ---
        var bodyStart = endIndex + 4; // skip "\n---"
        if (bodyStart < content.Length && content[bodyStart] == '\n')
            bodyStart++;

        return bodyStart < content.Length ? content[bodyStart..] : "";
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "ExtractionCacheTests"`
Expected: All 6 tests pass

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy/Cache/ tests/Graphiphy.Tests/Cache/
git commit -m "feat: add extraction cache with SHA256 hashing and frontmatter stripping"
```

---

## Phase 9: Report Generation

### Task 24: Report Generator (Tests First)

**Files:**
- Create: `tests/Graphiphy.Tests/Report/ReportGeneratorTests.cs`
- Create: `src/Graphiphy/Report/ReportGenerator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Tests/Report/ReportGeneratorTests.cs
using Graphiphy.Build;
using Graphiphy.Models;
using Graphiphy.Report;

namespace Graphiphy.Tests.Report;

public class ReportGeneratorTests
{
    [Test]
    public async Task Generate_ProducesMarkdown()
    {
        var ext = new Extraction
        {
            Nodes =
            [
                new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py", Community = 0 },
                new() { Id = "a::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "a.py", Community = 0 },
                new() { Id = "b::Baz", Label = "Baz", FileTypeString = "code", SourceFile = "b.py", Community = 1 },
            ],
            Edges =
            [
                new() { Source = "a::Foo", Target = "a::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" },
                new() { Source = "a::Foo", Target = "b::Baz", Relation = "imports", ConfidenceString = "INFERRED", SourceFile = "a.py" },
            ]
        };
        var graph = GraphBuilder.Build([ext]);

        var report = ReportGenerator.Generate(graph);

        await Assert.That(report).Contains("# Graph Report");
        await Assert.That(report).Contains("node");
        await Assert.That(report).Contains("edge");
    }

    [Test]
    public async Task Generate_IncludesSummaryStats()
    {
        var ext = new Extraction
        {
            Nodes =
            [
                new() { Id = "a::X", Label = "X", FileTypeString = "code", SourceFile = "a.py" },
            ],
            Edges = []
        };
        var graph = GraphBuilder.Build([ext]);

        var report = ReportGenerator.Generate(graph);

        await Assert.That(report).Contains("1");  // 1 node
    }

    [Test]
    public async Task Generate_EmptyGraph_DoesNotThrow()
    {
        var ext = new Extraction { Nodes = [], Edges = [] };
        var graph = GraphBuilder.Build([ext]);

        var report = ReportGenerator.Generate(graph);

        await Assert.That(report).Contains("# Graph Report");
    }
}
```

- [ ] **Step 2: Implement ReportGenerator**

```csharp
// src/Graphiphy/Report/ReportGenerator.cs
using System.Text;
using Graphiphy.Analysis;
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Report;

public static class ReportGenerator
{
    public static string Generate(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Graph Report");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Nodes:** {graph.VertexCount}");
        sb.AppendLine($"- **Edges:** {graph.EdgeCount}");

        var communities = graph.Vertices
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value)
            .Distinct()
            .Count();
        if (communities > 0)
            sb.AppendLine($"- **Communities:** {communities}");

        // Confidence breakdown
        var confidenceCounts = graph.Edges
            .GroupBy(e => e.Tag.ConfidenceString)
            .ToDictionary(g => g.Key, g => g.Count());
        if (confidenceCounts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Confidence Breakdown");
            sb.AppendLine();
            foreach (var (conf, count) in confidenceCounts.OrderBy(kv => kv.Key))
            {
                var pct = graph.EdgeCount > 0 ? (100.0 * count / graph.EdgeCount) : 0;
                sb.AppendLine($"- {conf}: {count} ({pct:F1}%)");
            }
        }

        // God nodes
        var gods = GraphAnalyzer.GodNodes(graph, topN: 5);
        if (gods.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Most Connected Entities");
            sb.AppendLine();
            foreach (var node in gods)
            {
                var degree = graph.Degree(node);
                sb.AppendLine($"- **{node.Label}** ({node.SourceFile}) — {degree} connections");
            }
        }

        // Surprising connections
        var surprises = GraphAnalyzer.SurprisingConnections(graph, topN: 5);
        if (surprises.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Surprising Connections");
            sb.AppendLine();
            foreach (var s in surprises)
            {
                sb.AppendLine($"- {s.Source.Label} → {s.Target.Label} ({s.Edge.Relation}, {s.Edge.ConfidenceString}) — score {s.Score:F1}");
            }
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Graphiphy.Tests/ --filter "ReportGeneratorTests"`
Expected: All 3 tests pass

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy/Report/ tests/Graphiphy.Tests/Report/
git commit -m "feat: add markdown report generator"
```

---

## Phase 10: Full Pipeline Integration

### Task 25: Run Full Test Suite and Fix Issues

**Files:** (none new — verification only)

- [ ] **Step 1: Run entire test suite**

Run: `dotnet test Graphiphy.sln --logger "console;verbosity=detailed"`
Expected: All tests pass

- [ ] **Step 2: Fix any compilation or runtime issues discovered**

Address any issues found during full suite run.

- [ ] **Step 3: Final commit if fixups needed**

```bash
git add -A
git commit -m "fix: resolve integration issues from full test suite run"
```

---

## Summary

| Phase | Tasks | Components |
|---|---|---|
| 1: Foundation | 1–3 | Solution, models, validation |
| 2: Detection | 4–5 | File classifier, detector, ignore patterns |
| 3: Extraction | 6–17 | Interface, base extractor, 9 language extractors |
| 4: Graph Building | 18 | QuikGraph assembly from extractions |
| 5: Deduplication | 19 | MinHash/LSH + Jaro-Winkler + union-find |
| 6: Clustering | 20–21 | Native Leiden shim + .NET wrapper |
| 7: Analysis | 22 | God nodes, surprising connections |
| 8: Caching | 23 | SHA256 file hashing, JSON cache |
| 9: Report | 24 | Markdown report generation |
| 10: Integration | 25 | Full suite verification |

**Total: 25 tasks, ~2500 lines of production code, ~1200 lines of tests**

## What's Deferred

- Visualization (callflow_html, tree_html, SVG/HTML export)
- LLM backends (Claude, Gemini, Kimi, Ollama)
- MCP server
- CLI entry point (`__main__.py` equivalent)
- Incremental detection with manifest
- Watch mode
- Git hooks
- Google Workspace integration
- Audio/video transcription
- Neo4j/GraphML/Obsidian export
