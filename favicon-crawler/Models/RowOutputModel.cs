using CsvHelper.Configuration.Attributes;

namespace FaviconFinder.Models
{
    public class RowOutputModel
    {
        [Name("rank")]
        public int Rank { get; set; }

        [Name("domain")]
        public string Domain { get; set; }

        [Name("favicon_url")]
        public string FaviconUrl { get; set; }
    }
}