# docxodus-service

Minimal ASP.NET Core microservice that parses DOCX files into OpenContract JSON format using [Docxodus](https://www.nuget.org/packages/Docxodus).

## Endpoints

### `POST /parse`

Accepts a JSON body with a base64-encoded DOCX file and returns an OpenContractDocExport JSON document containing extracted text, structural annotations with character offsets, and page/token metadata.

**Request:**

```json
{
  "filename": "contract.docx",
  "docx_base64": "UEsDBBQAAAAI..."
}
```

**Response:** OpenContractDocExport JSON with fields such as `title`, `content`, `pageCount`, `pawlsFileContent`, `labelledText`, `docLabels`, and `relationships`.

### `GET /health`

Returns `{"status": "healthy"}` with HTTP 200.

## Docker

```bash
docker pull ghcr.io/open-source-legal/docxodus-service:latest
docker run -p 8080:8080 ghcr.io/open-source-legal/docxodus-service:latest
```

## Build from source

```bash
dotnet build
dotnet run
```

The service listens on port 8080 by default.

## License

AGPL-3.0
