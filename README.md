# favicon-crawler

Multi-threaded command line program to extract favicons path based on CSV list of domains. Built with C#/.NET 6.

## Requirements

- VS2022 and .NET SDK

## Usage

Running more than 5 threads with default settings will on average complete 1000 domains under 5 minutes depending on timeout.

```
cd favicon-crawler
dotnet run --input favicon-finder-top-1k-domains.csv --threads 30 --timeout 2
```

Required parameters

- `input`: path for input CSV

Optional

- `retries`: How many times to retry a failed favicon lookup, default: 0
- `timeout`: Seconds to wait before connection timeout, some sites will require longer timeout. Default: 3
- `threads`: Number of threads to process URL queue, default: 10
- `output`: Output CSV location

## Future features

- Docker version
