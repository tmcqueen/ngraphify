using Graphiphy.Models;
using Graphiphy.Validation;

namespace Graphiphy.Tests.Validation;

public class ExtractionValidatorTests
{
    private static Models.Extraction Single(Node n) => new()
    {
        Nodes = [n],
        Edges = [],
    };

    [Test]
    public async Task ValidExtraction_ReturnsNoErrors()
    {
        var extraction = new Models.Extraction
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

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task MissingNodeId_ReturnsError()
    {
        var extraction = new Models.Extraction
        {
            Nodes =
            [
                new Node { Id = "", Label = "Foo", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges = []
        };

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("id");
    }

    [Test]
    public async Task MissingNodeLabel_ReturnsError()
    {
        var extraction = new Models.Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "", FileTypeString = "code", SourceFile = "foo.py" }
            ],
            Edges = []
        };

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("label");
    }

    [Test]
    public async Task InvalidFileType_ReturnsError()
    {
        var extraction = new Models.Extraction
        {
            Nodes =
            [
                new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "banana", SourceFile = "foo.py" }
            ],
            Edges = []
        };

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("file_type");
    }

    [Test]
    public async Task InvalidConfidence_ReturnsError()
    {
        var extraction = new Models.Extraction
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

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("confidence");
    }

    [Test]
    public async Task DanglingEdge_ReturnsError()
    {
        var extraction = new Models.Extraction
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

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("dangling");
    }

    [Test]
    public async Task MissingEdgeSourceFile_ReturnsError()
    {
        var extraction = new Models.Extraction
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

        var result = ExtractionValidator.Validate(extraction);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("source_file");
    }

    [Test]
    public async Task Validate_VideoFileType_ProducesWarningNotError()
    {
        var ext = Single(new Node
        {
            Id = "v1", Label = "demo.mp4",
            FileTypeString = "video", SourceFile = "demo.mp4",
        });

        var result = ExtractionValidator.Validate(ext);

        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.Warnings.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AssertValid_VideoFileType_DoesNotThrow()
    {
        var ext = Single(new Node
        {
            Id = "v1", Label = "demo.mp4",
            FileTypeString = "video", SourceFile = "demo.mp4",
        });

        var act = () => ExtractionValidator.AssertValid(ext);

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task Validate_UnknownFileType_IsError()
    {
        var ext = Single(new Node
        {
            Id = "x", Label = "weird",
            FileTypeString = "garbage-not-in-enum", SourceFile = "x",
        });

        var result = ExtractionValidator.Validate(ext);

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Validate_ValidCodeNode_NoErrorsOrWarnings()
    {
        var ext = Single(new Node
        {
            Id = "c1", Label = "MyClass",
            FileTypeString = "code", SourceFile = "MyClass.cs",
        });

        var result = ExtractionValidator.Validate(ext);

        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.Warnings).IsEmpty();
    }
}
