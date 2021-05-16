using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using gRPC.net.demo.stockDetails;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace gRPC.Server.Services
{
    public class StockDataService : StockService.StockServiceBase
    {
        private readonly ILogger<StockDataService> _logger;

        private static readonly List<Stock> _stocks = new List<Stock> 
        {
            {new Stock {StockId = "FB", StockName = "Facebook"} },
            {new Stock {StockId = "AAPL", StockName = "Apple"} },
            {new Stock {StockId = "AMZN", StockName = "Amazon"} },
            {new Stock {StockId = "NFLX", StockName = "Netflix"} },
            {new Stock {StockId = "MSFT", StockName = "Microsoft"} },
            {new Stock {StockId = "TSLA", StockName = "Tesla"} },
            {new Stock {StockId = "GOOG", StockName = "Alphabet"} }
        }; 

        public StockDataService(ILogger<StockDataService> logger)
        {
            _logger = logger;
        }

        public override Task<StockListing> GetStockListings(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new StockListing { Stocks = { _stocks }  });
        }

        public override Task<StockPrice> GetStockPrice(Stock request, ServerCallContext context)
        {
            var rnd = new Random(100);
            return Task.FromResult(
                new StockPrice
                {
                    Stock = _stocks.FirstOrDefault(x => x.StockId == request.StockId),
                    DateTimeStamp = DateTime.UtcNow.ToTimestamp(),
                    Price = rnd.Next(100, 500).ToString()
                });
        }

        public override async Task GetStockPriceStream(Empty request, IServerStreamWriter<StockPrice> responseStream, ServerCallContext context)
        {            
            int i = 10;
            var rnd = new Random(100);
            while (!context.CancellationToken.IsCancellationRequested && i > 0)
            {
                _stocks.ForEach(async s =>
                {
                    var time = DateTime.UtcNow.ToTimestamp();                    
                    await responseStream.WriteAsync(new StockPrice 
                    { 
                        Stock = s,
                        DateTimeStamp = time,
                        Price = rnd.Next(100, 500).ToString()
                    });
                });

                await Task.Delay(500);
            }
        }

        public override async Task GetCompanyStockPriceStream(IAsyncStreamReader<Stock> requestStream, IServerStreamWriter<StockPrice> responseStream, ServerCallContext context)
        {
            // we'll use a channel here to handle in-process 'messages' concurrently being written to and read from the channel.
            var channel = Channel.CreateUnbounded<StockPrice>();

            // background task which uses async streams to write each stockPrice from the channel to the response steam.
            _ = Task.Run(async () =>
            {
                await foreach (var stockPrice in channel.Reader.ReadAllAsync())
                {
                    await responseStream.WriteAsync(stockPrice);
                }
            });

            // a list of tasks handling requests concurrently
            var getCompanyStockPriceStreamRequestTasks = new List<Task>();

            try
            {
                // async streams used to read and process each request from the stream as they are receieved
                await foreach (var request in requestStream.ReadAllAsync())
                {
                    _logger.LogInformation($"Getting stock Price for {request.StockName}({request.StockId})");
                    // start and add the request handling task
                    getCompanyStockPriceStreamRequestTasks.Add(GetStockPriceAsync(request));
                }

                _logger.LogInformation("Client finished streaming");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred");
            }

            // wait for all responses to be written to the channel 
            // from the concurrent tasks handling each request
            await Task.WhenAll(getCompanyStockPriceStreamRequestTasks);

            channel.Writer.TryComplete();

            //  wait for all responses to be read from the channel and streamed as responses
            await channel.Reader.Completion;

            _logger.LogInformation("Completed response streaming");

            // a local function which defines a task to handle a Company Stock Price request
            // it mimics 10 consecutive stock price, simulating a delay of 0.5s
            // multiple instances of this will run concurrently for each recieved request
            async Task GetStockPriceAsync(Stock stock)
            {
                var rnd = new Random(100);
                for (int i = 0; i < 10; i++)
                {
                    var time = DateTime.UtcNow.ToTimestamp();
                    await channel.Writer.WriteAsync(new StockPrice 
                    {
                        Stock = _stocks.FirstOrDefault(x => x.StockId == stock.StockId),
                        Price = rnd.Next(100, 500).ToString(),
                        DateTimeStamp = time
                    });

                    await Task.Delay(500);
                }
            }
        }
    }
}
