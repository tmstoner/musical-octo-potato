using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace MusicalOctoPotato.Web.Services;

public sealed class LuceneIndexService
{
    private static readonly LuceneVersion Version = LuceneVersion.LUCENE_48;
    private readonly object _sync = new();
    private readonly Dictionary<string, IndexState> _indices = new(StringComparer.OrdinalIgnoreCase);
    private long _totalDocumentsUploaded;
    private long _totalQueries;

    public IReadOnlyCollection<string> GetIndices()
    {
        lock (_sync)
        {
            return _indices.Keys.OrderBy(k => k).ToArray();
        }
    }

    public void DefineFields(string indexName, IEnumerable<string> fields)
    {
        lock (_sync)
        {
            var index = GetOrCreateIndex(indexName);
            foreach (var field in fields.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                index.Fields.Add(field.Trim());
            }
        }
    }

    public void AddDocument(string indexName, IReadOnlyDictionary<string, string> fields)
    {
        lock (_sync)
        {
            var index = GetOrCreateIndex(indexName);
            var document = new Document();

            foreach (var pair in fields.Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)))
            {
                var key = pair.Key.Trim();
                var value = pair.Value.Trim();
                index.Fields.Add(key);
                document.Add(new TextField(key, value, Field.Store.YES));
            }

            if (document.Fields.Count == 0)
            {
                throw new ArgumentException("A document must contain at least one non-empty field.");
            }

            document.Add(new StringField("_id", Guid.NewGuid().ToString("N"), Field.Store.YES));

            index.Writer.AddDocument(document);
            index.Writer.Commit();
            index.DocumentCount++;
            _totalDocumentsUploaded++;
        }
    }

    public IReadOnlyCollection<IReadOnlyDictionary<string, string>> Query(string indexName, string? queryText)
    {
        lock (_sync)
        {
            _totalQueries++;

            if (!_indices.TryGetValue(indexName, out var index))
            {
                return Array.Empty<IReadOnlyDictionary<string, string>>();
            }

            using var reader = DirectoryReader.Open(index.Writer, applyAllDeletes: true);
            var searcher = new IndexSearcher(reader);

            Query query;
            if (string.IsNullOrWhiteSpace(queryText))
            {
                query = new MatchAllDocsQuery();
            }
            else
            {
                var fields = index.Fields.ToArray();
                if (fields.Length == 0)
                {
                    return Array.Empty<IReadOnlyDictionary<string, string>>();
                }

                var parser = new MultiFieldQueryParser(Version, fields, new StandardAnalyzer(Version));
                query = parser.Parse(queryText);
            }

            var hits = searcher.Search(query, 25).ScoreDocs;
            return hits
                .Select(hit => searcher.Doc(hit.Doc))
                .Select(doc => doc.Fields
                    .Where(f => f.Name != "_id")
                    .ToDictionary(f => f.Name, f => f.GetStringValue() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public object GetMetrics()
    {
        lock (_sync)
        {
            return new
            {
                totalIndices = _indices.Count,
                totalDocumentsUploaded = _totalDocumentsUploaded,
                totalQueries = _totalQueries,
                indices = _indices.Values.Select(i => new
                {
                    name = i.Name,
                    configuredFields = i.Fields.OrderBy(f => f).ToArray(),
                    documents = i.DocumentCount
                }).OrderBy(i => i.name).ToArray()
            };
        }
    }

    private IndexState GetOrCreateIndex(string indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("Index name is required.");
        }

        var cleanName = indexName.Trim();
        if (_indices.TryGetValue(cleanName, out var existing))
        {
            return existing;
        }

        var analyzer = new StandardAnalyzer(Version);
        var config = new IndexWriterConfig(Version, analyzer);
        var writer = new IndexWriter(new RAMDirectory(), config);
        var created = new IndexState(cleanName, writer);
        _indices.Add(cleanName, created);
        return created;
    }

    private sealed class IndexState
    {
        public IndexState(string name, IndexWriter writer)
        {
            Name = name;
            Writer = writer;
        }

        public string Name { get; }
        public IndexWriter Writer { get; }
        public HashSet<string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DocumentCount { get; set; }
    }
}
