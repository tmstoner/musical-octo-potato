# musical-octo-potato

A lightweight ASP.NET Core application that wraps **Lucene.NET** to provide:

- REST APIs for index management, custom field configuration, document uploads, and querying
- A built-in web UI for index configuration/testing and metric visibility
- Basic index and query metrics via API

## Run

```bash
dotnet run --project /tmp/workspace/tmstoner/musical-octo-potato/MusicalOctoPotato.Web
```

## API summary

- `GET /api/indices`
- `POST /api/indices/{indexName}/fields`
- `POST /api/indices/{indexName}/documents`
- `GET /api/indices/{indexName}/query?q=...`
- `GET /api/metrics`
