using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DocxodusService.Tests;

public class ParseEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ParseEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ParseEndpoint_EmptyBase64_ReturnsBadRequest()
    {
        var payload = new { filename = "test.docx", docx_base64 = "" };
        var response = await _client.PostAsJsonAsync("/parse", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ParseEndpoint_InvalidBase64_ReturnsBadRequest()
    {
        var payload = new { filename = "test.docx", docx_base64 = "not-valid-base64!!!" };
        var response = await _client.PostAsJsonAsync("/parse", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ParseEndpoint_ValidDocx_ReturnsSuccess()
    {
        var docxBase64 = CreateMinimalDocxBase64();
        var payload = new { filename = "hello.docx", docx_base64 = docxBase64 };

        var response = await _client.PostAsJsonAsync("/parse", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(
            doc.RootElement.TryGetProperty("content", out _) ||
            doc.RootElement.TryGetProperty("Content", out _),
            "Response should contain a 'content' field"
        );
    }

    [Fact]
    public async Task ParseEndpoint_MissingBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(
            "/parse",
            new StringContent("", Encoding.UTF8, "application/json")
        );

        // Empty JSON string "" is not valid JSON for the endpoint.
        // The framework may return 400 (bad request) or 415 (unsupported media type).
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnsupportedMediaType,
            $"Expected 400 or 415 but got {(int)response.StatusCode}"
        );
    }

    [Theory]
    [InlineData("Hello World", "Second paragraph.")]
    [InlineData("Clause \ud83d\udcdc — café & terms", "第二条: résumé")]
    public async Task ParseEndpoint_PreservesTextAndAnnotationOffsets(string first, string second)
    {
        var response = await _client.PostAsJsonAsync("/parse", new
        {
            filename = "contract.docx",
            docx_base64 = CreateMinimalDocxBase64(first, second),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var content = root.GetProperty("content").GetString()!;
        Assert.Equal($"{first}\n{second}\n", content);
        Assert.True(root.GetProperty("pageCount").GetInt32() > 0);
        Assert.NotEmpty(root.GetProperty("pawlsFileContent").EnumerateArray());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("docLabels").ValueKind);

        var paragraphs = root.GetProperty("labelledText").EnumerateArray()
            .Where(a => a.GetProperty("annotationLabel").GetString() == "PARAGRAPH")
            .ToArray();
        Assert.Equal(2, paragraphs.Length);
        var expectedStart = 0;
        foreach (var (annotation, expectedText) in paragraphs.Zip(new[] { first, second }))
        {
            Assert.True(annotation.GetProperty("structural").GetBoolean());
            Assert.Equal(expectedText, annotation.GetProperty("rawText").GetString());
            var span = annotation.GetProperty("annotationJson");
            var start = span.GetProperty("start").GetInt32();
            var end = span.GetProperty("end").GetInt32();
            // .NET and the browser both address strings in UTF-16 code units.
            Assert.Equal(expectedStart, start);
            Assert.Equal(expectedText, content[start..end]);
            expectedStart += expectedText.Length + 1;
        }
    }

    [Fact]
    public async Task ParseEndpoint_InvalidDocx_ReturnsUnprocessableEntity()
    {
        var response = await _client.PostAsJsonAsync("/parse", new
        {
            filename = "invalid.docx",
            docx_base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("not a DOCX archive")),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>
    /// Creates a minimal valid DOCX file as a base64 string.
    /// A DOCX is a ZIP archive containing [Content_Types].xml, _rels/.rels,
    /// and word/document.xml with at least one paragraph.
    /// </summary>
    private static string CreateMinimalDocxBase64(params string[] paragraphs)
    {
        if (paragraphs.Length == 0) paragraphs = new[] { "Hello World" };
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/word/document.xml\" " +
                "ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                "</Types>");

            AddEntry(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" " +
                "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" " +
                "Target=\"word/document.xml\"/>" +
                "</Relationships>");

            AddEntry(zip, "word/document.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                "<w:body>" +
                string.Concat(paragraphs.Select(text =>
                    $"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(text)}</w:t></w:r></w:p>")) +
                "</w:body>" +
                "</w:document>");
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
