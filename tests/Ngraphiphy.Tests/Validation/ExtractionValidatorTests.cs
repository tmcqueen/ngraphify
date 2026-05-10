using Ngraphiphy.Models;
using Ngraphiphy.Validation;

namespace Ngraphiphy.Tests.Validation;

public class ExtractionValidatorTests
{
    [Test]
    public async Task ValidExtraction_ReturnsNoErrors()
    {

        var extraction = new Models.Extraction
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

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task MissingNodeId_ReturnsError()
    {

        var extraction = new Models.Extraction
        var extraction = new Models.Extraction
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

        var extraction = new Models.Extraction
        var extraction = new Models.Extraction
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

        var extraction = new Models.Extraction
        var extraction = new Models.Extraction
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

        var extraction = new Models.Extraction
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

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("confidence");
    }

    [Test]
    public async Task DanglingEdge_ReturnsError()
    {

        var extraction = new Models.Extraction
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

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("dangling");
    }

    [Test]
    public async Task MissingEdgeSourceFile_ReturnsError()
    {

        var extraction = new Models.Extraction
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

        var errors = ExtractionValidator.Validate(extraction);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("source_file");
    }
}
