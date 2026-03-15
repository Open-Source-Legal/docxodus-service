using System.Text.Json.Serialization;

namespace DocxodusService.Models;

public class ParseRequest
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("docx_base64")]
    public string DocxBase64 { get; set; } = string.Empty;
}
