using System.Text.Json;
using System.Text.Json.Serialization;
using DocxodusService.Models;
using Docxodus;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
};

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
        var filename = request.Filename ?? "document.docx";
        var wmlDoc = new WmlDocument(filename, docxBytes);
        var export = OpenContractExporter.Export(wmlDoc);
        return Results.Json(export, jsonOptions);
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
