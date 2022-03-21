using CsvHelper;
using CsvHelper.Configuration;
using FaviconFinder.Common;
using FaviconFinder.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;

namespace FaviconFinder.Services
{
    public interface IFaviconCrawlerService
    {
        // main method to execute the service
        Task<bool> Run();
    }

    public class FaviconCrawlerService : IFaviconCrawlerService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<FaviconCrawlerService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;

        private readonly bool _printAllLogs = false;
        private readonly int _retryMax = 0;
        private readonly int _threads = 10;
        private readonly int _timeoutSeconds = 3;

        private ConcurrentBag<RowOutputModel> _results;
        private BlockingCollection<DomainProcessingModel> _queue;

        public FaviconCrawlerService(IHttpClientFactory httpClientFactory, ILogger<FaviconCrawlerService> logger, IConfiguration config)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            _retryMax = string.IsNullOrEmpty(config["retries"]) ? _retryMax : int.Parse(config["retries"]);
            _timeoutSeconds = string.IsNullOrEmpty(config["timeout"]) ? _timeoutSeconds : int.Parse(config["timeout"]);
            _threads = string.IsNullOrEmpty(config["threads"]) ? _threads : int.Parse(config["threads"]);

            // https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.blockingcollection-1?redirectedfrom=MSDN&view=net-5.0
            _results = new ConcurrentBag<RowOutputModel>();

            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

            // add headers to make our crawler appear like a real web browser or else connection may be rejected
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.HeaderUserAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept", Constants.HeaderAccept);
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", Constants.HeaderLanguage);

            _queue = new BlockingCollection<DomainProcessingModel>();
        }

        public Task<bool> Run()
        {
            // Console.Read();
            _logger.LogInformation($"Reading input CSV...");

            // Read input file and initialize queue
            _queue = ReadInputFileIntoQueue(_config["input"]);

            _logger.LogInformation($"{_queue.Count} domains found in input CSV.");
            _logger.LogInformation($"Processed CSV, will now crawl domains with {_threads} worker threads.");

            // track time elapsed
            DateTime startTime = DateTime.Now;

            // Try to parse a single website
            //DebugSingle();

            var background = Task.Factory.StartNew(InitProcess);

            // tell consumers that there will be no more additions
            _queue.CompleteAdding();

            background.Wait();

            _logger.LogInformation("Finished!");
            _logger.LogInformation($"Time elapsed: {DateTime.Now - startTime}");
            int failedLookups = _results.Count(x => x.FaviconUrl == "FAILED");
            _logger.LogInformation($"Processed {_results.Count} domains with {failedLookups} failures.");

            // write to output CSV file
            PrintOutputToFile();

            _logger.LogInformation("Done! Press any key to exit");
            Console.Read();

            return Task.FromResult(true);
        }

        private BlockingCollection<DomainProcessingModel> ReadInputFileIntoQueue(string inputPath)
        {
            var queue = new BlockingCollection<DomainProcessingModel>();

            var csvReaderConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false
            };

            using (var reader = new StreamReader(Directory.GetCurrentDirectory() + "/" + inputPath))
            using (var csv = new CsvReader(reader, csvReaderConfig))
            {
                var records = csv.GetRecords<RowInputModel>();

                foreach (var record in records)
                {
                    queue.Add(new DomainProcessingModel()
                    {
                        DomainRow = record,
                        Retries = _retryMax
                    });
                }
            }

            return queue;
        }

        private void InitProcess()
        {
            var actions = Enumerable.Repeat<Action>(QueueWorker, _threads);
            Parallel.Invoke(actions.ToArray());
        }

        private void PrintOutputToFile()
        {
            var outputPath = string.IsNullOrEmpty(_config["output"]) ? "/output.csv" : _config["output"];

            using (var writer = new StreamWriter(Directory.GetCurrentDirectory() + outputPath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(_results.OrderBy(x => x.Rank));
            }
        }

        // only for debugging
        private void DebugSingle()
        {
            var httpClient = _httpClientFactory.CreateClient();

            httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.HeaderUserAgent);
            httpClient.DefaultRequestHeaders.Add("Accept", Constants.HeaderAccept);

            var web = new HtmlWeb();
            web.PreRequest = delegate (HttpWebRequest webRequest)
            {
                webRequest.Timeout = _timeoutSeconds * 1000;
                webRequest.UserAgent = Constants.HeaderUserAgent;
                //webRequest.Accept = Constants.HeaderLanguage;
                return true;
            };

            var domain = new DomainProcessingModel()
            {
                Retries = 0,
                DomainRow = new RowInputModel()
                {
                    Rank = 0,
                    Url = "nseindia.com"
                }
            };

            ProcessDomain(domain, web, true);
        }

        private void QueueWorker()
        {
            HtmlWeb web = new HtmlWeb();
            web.PreRequest = delegate (HttpWebRequest webRequest)
            {
                webRequest.Timeout = _timeoutSeconds * 1000;
                webRequest.UserAgent = Constants.HeaderUserAgent;
                return true;
            };

            foreach (DomainProcessingModel domain in _queue.GetConsumingEnumerable())
            {
                ProcessDomain(domain, web);
            }
        }

        private void ProcessDomain(DomainProcessingModel domain, HtmlWeb web, bool checkDomOnly = false)
        {
            string faviconUrl = null;

            while (domain.Retries >= 0 && faviconUrl == null)
            {
                faviconUrl = GetFaviconUrl(domain.DomainRow.Url, web, checkDomOnly);
                domain.Retries--;
            }

            // failed to retrieve favicon
            if (faviconUrl == null)
            {
                _logger.LogError("Failed to get favicon for {DomainUrl}", domain.DomainRow.Url);
            }

            var rowResult = new RowOutputModel()
            {
                Rank = domain.DomainRow.Rank,
                Domain = domain.DomainRow.Url,
                FaviconUrl = faviconUrl == null ? "FAILED" : faviconUrl
            };

            _results.Add(rowResult);
        }

        private string GetFaviconUrl(string url, HtmlWeb web, bool checkDomOnly)
        {
            foreach (string prefix in Constants.UrlPrefixes)
            {
                // try optimistically accessing favicon.ico at the root path
                string rootPath = $"{prefix}{url}/favicon.ico";

                if (!checkDomOnly)
                {
                    try
                    {
                        var response = _httpClient.Send(new HttpRequestMessage(HttpMethod.Get, rootPath));
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            return rootPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_printAllLogs)
                        {
                            _logger.LogInformation("Failed to connect to {UrlPath}: {Message}", rootPath, ex.Message);
                        }
                    }
                }

                rootPath = $"{prefix}{url}";

                // scan DOM for a <link> tag with the favicon resource URL
                try
                {
                    
                    HtmlDocument doc = web.Load(rootPath);

                    foreach (var pattern in Constants.FaviconPatterns)
                    {
                        var node = doc.DocumentNode.SelectSingleNode(pattern);

                        if (node != null && !string.IsNullOrEmpty(node.Attributes["href"].Value))
                        {
                            string href = node.Attributes["href"].Value;
                            string faviconUrl = href.Contains("http") || href.Contains("www.") || href.Contains(".com")
                                ? href : string.Format("{0}{1}{2}", prefix, url, href);
                            return faviconUrl;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_printAllLogs)
                    {
                        _logger.LogInformation("Failed to connect to {UrlPath}: {Message}", rootPath, ex.Message);
                    }
                }
            }

            return null;
        }
    }
}