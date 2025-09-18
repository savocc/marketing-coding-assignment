using CsvHelper;
using CsvHelper.Configuration;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Queries;
using Lucene.Net.Search;
using Lucene.Net.Search.Suggest;
using Lucene.Net.Search.Suggest.Analyzing;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MarketingCodingAssignment.Models;
using System.Globalization;
using System.IO;

namespace MarketingCodingAssignment.Services
{
    public class SearchEngine
    {
        // The code below is roughly based on sample code from: https://lucenenet.apache.org/

        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

        public SearchEngine()
        {

        }

        public List<FilmCsvRecord> ReadFilmsFromCsv()
        {
            List<FilmCsvRecord> records = new();
            string filePath = $"{System.IO.Directory.GetCurrentDirectory()}{@"\wwwroot\csv"}" + "\\" + "FilmsInfo.csv";
            using (StreamReader reader = new(filePath))
            using (CsvReader csv = new(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                records = csv.GetRecords<FilmCsvRecord>().ToList();

            }
            using (StreamReader r = new(filePath))
            {
                string csvFileText = r.ReadToEnd();
            }
            return records;
        }

        // Read the data from the csv and feed it into the lucene index
        public void PopulateIndexFromCsv()
        {
            // Get the list of films from the csv file
            var csvFilms = ReadFilmsFromCsv();

            // Convert to Lucene format
            List<FilmLuceneRecord> luceneFilms = csvFilms.Select(x => new FilmLuceneRecord
            {
                Id = x.Id,
                Title = x.Title,
                Overview = x.Overview,
                Runtime = int.TryParse(x.Runtime, out int parsedRuntime) ? parsedRuntime : 0,
                Tagline = x.Tagline,
                Revenue = long.TryParse(x.Revenue, out long parsedRevenue) ? parsedRevenue : 0,
                VoteAverage = double.TryParse(x.VoteAverage, out double parsedVoteAverage) ? parsedVoteAverage : 0,
                ReleaseDate = DateTime.TryParse(x.ReleaseDate, out DateTime releaseDate) ? releaseDate : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
            }).ToList();

            // Write the records to the lucene index
            PopulateIndex(luceneFilms);

            return;
        }

        public void PopulateIndex(List<FilmLuceneRecord> films)
        {
            // Construct a machine-independent path for the index
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string indexPath = Path.Combine(basePath, "index");
            using FSDirectory dir = FSDirectory.Open(indexPath);

            // Create an analyzer to process the text
            StandardAnalyzer analyzer = new(AppLuceneVersion);

            // Create an index writer
            IndexWriterConfig indexConfig = new(AppLuceneVersion, analyzer);
            using IndexWriter writer = new(dir, indexConfig);

            //Add to the index
            foreach (var film in films)
            {
                Document doc = new()
                {
                    new StringField("Id", film.Id, Field.Store.YES),
                    new TextField("Title", film.Title, Field.Store.YES),
                    new TextField("Overview", film.Overview, Field.Store.YES),
                    new Int32Field("Runtime", film.Runtime, Field.Store.YES),
                    new TextField("Tagline", film.Tagline, Field.Store.YES),
                    new Int64Field("Revenue", film.Revenue ?? 0, Field.Store.YES),
                    new DoubleField("VoteAverage", film.VoteAverage ?? 0.0, Field.Store.YES),
                    new TextField("CombinedText", film.Title + " " + film.Tagline + " " + film.Overview, Field.Store.NO),
                    new TextField("ReleaseDate", DateTools.DateToString(film.ReleaseDate ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), DateResolution.DAY), Field.Store.YES)
                };
                writer.AddDocument(doc);
            }

            writer.Flush(triggerMerge: false, applyAllDeletes: false);
            writer.Commit();

           return;
        }

        public void DeleteIndex()
        {
            DeleteIndexHelper("index");
            DeleteIndexHelper("suggester");
        }

        private void DeleteIndexHelper(string indexType)
        {
            // Delete everything from the index
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string indexPath = Path.Combine(basePath, indexType);
            using FSDirectory dir = FSDirectory.Open(indexPath);
            StandardAnalyzer analyzer = new(AppLuceneVersion);
            IndexWriterConfig indexConfig = new(AppLuceneVersion, analyzer);
            using IndexWriter writer = new(dir, indexConfig);
            writer.DeleteAll();
            writer.Commit();
            return;
        }

        public SearchResultsViewModel Search(string searchString, int startPage, int rowsPerPage, int? durationMinimum, int? durationMaximum, double? voteAverageMinimum, DateTime? releaseDateStart, DateTime? releaseDateEnd)
        {
            // Construct a machine-independent path for the index
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string indexPath = Path.Combine(basePath, "index");

            // Create paths and populate indices (on first run)
            // This fixes 'non-existing path' exceptions and allows user to interact with search without having to manually rebuild index.
            if (!System.IO.Directory.Exists(indexPath))
            {
                PopulateIndexFromCsv();
            }
            string suggesterPath = Path.Combine(basePath, "suggester");
            if (!System.IO.Directory.Exists(suggesterPath))
            {
                PopulateSuggesterIndex();
            }

            using FSDirectory dir = FSDirectory.Open(indexPath);
            using DirectoryReader reader = DirectoryReader.Open(dir);
            IndexSearcher searcher = new(reader);

            int hitsLimit = 1000;
            TopScoreDocCollector collector = TopScoreDocCollector.Create(hitsLimit, true);

            var query = this.GetLuceneQuery(searchString, durationMinimum, durationMaximum, voteAverageMinimum, releaseDateStart, releaseDateEnd);

            searcher.Search(query, collector);

            int startIndex = (startPage) * rowsPerPage;
            TopDocs hits = collector.GetTopDocs(startIndex, rowsPerPage);
            ScoreDoc[] scoreDocs = hits.ScoreDocs;

            List<FilmLuceneRecord> films = new();
            foreach (ScoreDoc? hit in scoreDocs)
            {
                Document foundDoc = searcher.Doc(hit.Doc);
                FilmLuceneRecord film = new()
                {
                    Id = foundDoc.Get("Id").ToString(),
                    Title = foundDoc.Get("Title").ToString(),
                    Overview = foundDoc.Get("Overview").ToString(),
                    Runtime = int.TryParse(foundDoc.Get("Runtime"), out int parsedRuntime) ? parsedRuntime : 0,
                    Tagline = foundDoc.Get("Tagline").ToString(),
                    Revenue = long.TryParse(foundDoc.Get("Revenue"), out long parsedRevenue) ? parsedRevenue : 0,
                    VoteAverage =  double.TryParse(foundDoc.Get("VoteAverage"), out double parsedVoteAverage) ? parsedVoteAverage : 0.0,
                    Score = hit.Score,
                    ReleaseDate = DateTime.TryParseExact(foundDoc.Get("ReleaseDate"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime releaseDate) ? releaseDate : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
                };
                films.Add(film);
            }

            SearchResultsViewModel searchResults = new()
            {
                RecordsCount = hits.TotalHits,
                Films = films.ToList()
            };

            return searchResults;
        }

        public IList<Lookup.LookupResult> AutoComplete(string input)
        {
            if (input == null)
            {
                return new List<Lookup.LookupResult>();
            }

            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string suggesterPath = Path.Combine(basePath, "suggester");
            using FSDirectory dir = FSDirectory.Open(suggesterPath);
            using AnalyzingInfixSuggester initSuggester = new(AppLuceneVersion, dir, new StandardAnalyzer(AppLuceneVersion));

            IList<Lookup.LookupResult> suggestedResults = initSuggester.DoLookup(input, 10, true, true);
            return suggestedResults;

        }

        public void PopulateSuggesterIndex()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            string suggesterPath = Path.Combine(basePath, "suggester");
            using FSDirectory dir = FSDirectory.Open(suggesterPath);
            using AnalyzingInfixSuggester initSuggester = new(AppLuceneVersion, dir, new StandardAnalyzer(AppLuceneVersion));

            // Get dictionary based on existing index to build suggester
            string indexPath = Path.Combine(basePath, "index");
            using FSDirectory indexdir = FSDirectory.Open(indexPath);
            using DirectoryReader reader = DirectoryReader.Open(indexdir);
            DocumentDictionary suggesterDict = new(reader, "Title", null);

            initSuggester.Build(suggesterDict);
            initSuggester.Commit();
        }

        private Query GetLuceneQuery(string searchString, int? durationMinimum, int? durationMaximum, double? voteAverageMinimum, DateTime? releaseDateStart, DateTime? releaseDateEnd)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                // If there's no search string, just return everything.
                return new MatchAllDocsQuery();
            }

            var pq = new MultiPhraseQuery();
            foreach (var word in searchString.Split(" ").Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                if (!EnglishAnalyzer.DefaultStopSet.Contains(word))
                {
                    pq.Add(new Term("CombinedText", word.ToLowerInvariant()));
                }
            }

            Query rq = NumericRangeQuery.NewInt32Range("Runtime", durationMinimum, durationMaximum, true, true);
            Query vaq = NumericRangeQuery.NewDoubleRange("VoteAverage", voteAverageMinimum, 10.0, true, true);
            if (!releaseDateStart.HasValue)
            {
                releaseDateStart = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            }

            if (!releaseDateEnd.HasValue)
            {
                releaseDateEnd = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            }
            string lowerDate = DateTools.DateToString(releaseDateStart.Value, DateResolution.DAY);
            string upperDate = DateTools.DateToString(releaseDateEnd.Value, DateResolution.DAY);
            Query dq = new TermRangeQuery(
                "ReleaseDate",
                new BytesRef(lowerDate),
                new BytesRef(upperDate),
                true,
                true
            );

            // Apply the filters.
            BooleanQuery bq = new()
            {
                { pq, Occur.MUST },
                { rq, Occur.MUST },
                { vaq,Occur.MUST },
                { dq, Occur.MUST }
            };

            return bq;
        }
    }
}

