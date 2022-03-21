# favicon-crawler

Multi-threaded command line program to extract favicons path based on CSV list of domains. Built with C#/.NET 6.

## Requirements

- VS2022 and .NET SDK

## Usage

Running more than 5 threads with default settings will on average complete 1000 domains under 5 minutes.

```
cd favicon-crawler
dotnet run --input favicon-finder-top-1k-domains.csv --threads 30 --timeout 2
```

Required parameters

- `input`: path for input CSV

Optional

- `retries`: how many times to retry a failed favicon lookup, default: 0
- `timeout`: seconds to wait before connection timeout, default: 3
- `threads`: number of threads to process URL queue, default: 10
- `output`: output CSV location

## Future features

- Docker version
