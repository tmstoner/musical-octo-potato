using MusicalOctoPotato.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LuceneIndexService>();

var app = builder.Build();

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang=\"en\">
<head>
  <meta charset=\"utf-8\" />
  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
  <title>Musical Octo Potato</title>
  <style>
    body { font-family: sans-serif; margin: 2rem; }
    section { margin-bottom: 1.5rem; }
    input, textarea, button { width: 100%; max-width: 700px; margin-top: .4rem; }
    textarea { min-height: 120px; }
    pre { background: #f4f4f4; padding: .75rem; overflow: auto; }
  </style>
</head>
<body>
  <h1>Lucene.NET Index Manager</h1>
  <p>Configure index fields, upload documents, execute test queries, and view metrics.</p>

  <section>
    <label>Index name</label>
    <input id=\"indexName\" value=\"default\" />
  </section>

  <section>
    <h2>Define Custom Fields</h2>
    <label>Comma-separated fields</label>
    <input id=\"fields\" value=\"title,body\" />
    <button onclick=\"defineFields()\">Save Fields</button>
  </section>

  <section>
    <h2>Upload Document</h2>
    <label>JSON document fields</label>
    <textarea id=\"doc\">{"title":"hello","body":"sample content"}</textarea>
    <button onclick=\"uploadDocument()\">Upload</button>
  </section>

  <section>
    <h2>Test Query</h2>
    <label>Query text</label>
    <input id=\"query\" value=\"hello\" />
    <button onclick=\"runQuery()\">Search</button>
  </section>

  <section>
    <h2>Metrics</h2>
    <button onclick=\"loadMetrics()\">Refresh Metrics</button>
  </section>

  <h2>Output</h2>
  <pre id=\"output\"></pre>

  <script>
    const output = document.getElementById('output');
    const indexName = () => document.getElementById('indexName').value.trim();
    const show = (value) => output.textContent = JSON.stringify(value, null, 2);

    async function defineFields() {
      const fields = document.getElementById('fields').value.split(',').map(v => v.trim()).filter(Boolean);
      const response = await fetch(`/api/indices/${encodeURIComponent(indexName())}/fields`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ fields })
      });
      show({ status: response.status, message: 'fields saved' });
    }

    async function uploadDocument() {
      const fields = JSON.parse(document.getElementById('doc').value);
      const response = await fetch(`/api/indices/${encodeURIComponent(indexName())}/documents`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ fields })
      });
      show({ status: response.status, message: 'document uploaded' });
    }

    async function runQuery() {
      const q = document.getElementById('query').value;
      const response = await fetch(`/api/indices/${encodeURIComponent(indexName())}/query?q=${encodeURIComponent(q)}`);
      show(await response.json());
    }

    async function loadMetrics() {
      const response = await fetch('/api/metrics');
      show(await response.json());
    }
  </script>
</body>
</html>
""", "text/html"));

app.MapGet("/api/indices", (LuceneIndexService indexService) => Results.Ok(indexService.GetIndices()));

app.MapPost("/api/indices/{indexName}/fields", (string indexName, DefineFieldsRequest request, LuceneIndexService indexService) =>
{
    if (request.Fields.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one field name is required." });
    }

    indexService.DefineFields(indexName, request.Fields);
    return Results.NoContent();
});

app.MapPost("/api/indices/{indexName}/documents", (string indexName, UploadDocumentRequest request, LuceneIndexService indexService) =>
{
    if (request.Fields.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one document field is required." });
    }

    indexService.AddDocument(indexName, request.Fields);
    return Results.Accepted($"/api/indices/{indexName}/query");
});

app.MapGet("/api/indices/{indexName}/query", (string indexName, string? q, LuceneIndexService indexService) =>
{
    try
    {
        return Results.Ok(indexService.Query(indexName, q));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/metrics", (LuceneIndexService indexService) => Results.Ok(indexService.GetMetrics()));

app.Run();

public sealed record DefineFieldsRequest(IReadOnlyCollection<string> Fields);
public sealed record UploadDocumentRequest(Dictionary<string, string> Fields);

public partial class Program;
