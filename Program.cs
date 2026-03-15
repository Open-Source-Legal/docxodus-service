using DocxodusService.Models;
using Docxodus;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/parse", (ParseRequest? request) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.DocxBase64))
    {
        return Results.BadRequest(new { error = "docx_base64 is required and must not be empty." });
    }

    byte[] docxBytes;
    try
    {
        docxBytes = Convert.FromBase64String(request.DocxBase64);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = "docx_base64 is not valid Base64." });
    }

    if (docxBytes.Length == 0)
    {
        return Results.BadRequest(new { error = "Decoded DOCX content is empty." });
    }

    try
    {
        using var stream = new MemoryStream(docxBytes);
        var result = OpenContractExporter.Export(stream, request.Filename);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 422,
            title: "Failed to parse DOCX"
        );
    }
});

app.Run();

// Make the implicit Program class visible to test projects
public partial class Program { }
