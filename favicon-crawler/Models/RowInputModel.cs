using CsvHelper.Configuration.Attributes;
using System;

namespace FaviconFinder.Models
{
    public class RowInputModel
    {
        [Index(0)]
        public int Rank { get; set; }

        [Index(1)]
        public string Url { get; set; }
    }
}