using Lucene.Net.Search.Suggest;
using MarketingCodingAssignment.Models;
using MarketingCodingAssignment.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;

namespace MarketingCodingAssignment.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SearchEngine _searchEngine;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            _searchEngine = new SearchEngine();
        }

        public IActionResult Index()
        {
            ViewBag.InvalidDate = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public JsonResult Autocomplete(string searchString)
        {
            IList<Lookup.LookupResult> searchResults = _searchEngine.AutoComplete(searchString);
            return Json(new { searchResults });
        }

        [HttpGet]
        public JsonResult Search(string searchString, int start, int rows, int? durationMinimum, int? durationMaximum, double? voteAverageMinimum, DateTime? releaseDateStart, DateTime? releaseDateEnd)
        {
            SearchResultsViewModel searchResults = _searchEngine.Search(searchString, start, rows, durationMinimum, durationMaximum, voteAverageMinimum, releaseDateStart, releaseDateEnd);
            return Json(new {searchResults});
        }

        public ActionResult ReloadIndex()
        {
            DeleteIndex();
            PopulateIndex();
            return RedirectToAction("Index", "Home");
        }

        // Delete the contents of the lucene index 
        public void DeleteIndex()
        {
            _searchEngine.DeleteIndex();
            return;
        }

        // Read the data from the csv and feed it into the lucene index
        public void PopulateIndex()
        {
            _searchEngine.PopulateIndexFromCsv();
            _searchEngine.PopulateSuggesterIndex();
            return;
        }

    }
}

