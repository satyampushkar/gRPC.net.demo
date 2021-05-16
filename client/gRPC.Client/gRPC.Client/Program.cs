using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using gRPC.net.demo.stockDetails;
using System.Threading;
using Grpc.Core;

namespace gRPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new StockService.StockServiceClient(channel);

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("******************************************");
                Console.WriteLine("Press 1 for GetStockListings");
                Console.WriteLine("Press 2 for GetStockPrice");
                Console.WriteLine("Press 3 for GetStockPriceStream");
                Console.WriteLine("Press 4 for GetCompanyStockPriceStream");
                Console.WriteLine("Press 5 for Exit from app");
                Console.WriteLine("******************************************");
                Console.ResetColor();
                var input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        GetStockListings(client);
                        break;
                    case "2":
                        GetStockPrice(client);
                        break;
                    case "3":
                        await GetStockPriceStream(client);
                        break;
                    case "4":
                        await GetCompanyStockPriceStream(client);
                        break;
                    case "5":
                        Environment.Exit(0);
                        break;
                    default:
                        break;
                }
            }
        }

        private static async Task GetCompanyStockPriceStream(StockService.StockServiceClient client)
        {
            //To get all stock Ids
            //var stocks = client.GetStockListings(new Empty()).Stocks; 
            using var streamingCall = client.GetCompanyStockPriceStream();

            // background task which uses async streams to read each stockPrice from the response steam.
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var stockPrice in streamingCall.ResponseStream.ReadAllAsync())
                    {
                        Console.WriteLine(string.Format($"{stockPrice.Stock.StockId}\t {stockPrice.Stock.StockName}\t {stockPrice.Price}\t {stockPrice.DateTimeStamp}"));
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    Console.WriteLine("Stream cancelled.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading response: " + ex);
                }
            });

            //send requests through requst stream
            foreach (var stockId in new[] { "FB", "AAPL", "AMZN", "MSFT", "GOOG" })
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Requesting details for {stockId}...");
                Console.ResetColor();

                await streamingCall.RequestStream.WriteAsync(new Stock { StockId = stockId });

                //Mimicing delay in sending request
                await Task.Delay(1500);
            }

            Console.WriteLine("Completing request stream");
            await streamingCall.RequestStream.CompleteAsync();
            Console.WriteLine("Request stream completed");
        }

        private static async Task GetStockPriceStream(StockService.StockServiceClient client)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            using var streamingCall = client.GetStockPriceStream(new Empty(), cancellationToken: cts.Token);
            try
            {
                Console.WriteLine(string.Format($"Stock Id\t Stock Name\t Stock Price\t TimeStamp"));
                await foreach (var stockPrice in streamingCall.ResponseStream.ReadAllAsync(cancellationToken: cts.Token))
                {
                    Console.WriteLine(string.Format($"{stockPrice.Stock.StockId}\t {stockPrice.Stock.StockName}\t {stockPrice.Price}\t {stockPrice.DateTimeStamp}"));
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                Console.WriteLine("Stream cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading response: " + ex);
            }
        }

        private static void GetStockPrice(StockService.StockServiceClient client)
        {
            var result = client.GetStockPrice(new Stock { StockId = "FB" });
            Console.WriteLine($"Stock Id: {result.Stock.StockId}\n Stock Name: {result.Stock.StockName}\n " +
                $"Stock Price: {result.Price}\n TimeStamp: {result.DateTimeStamp}");
        }

        private static StockListing GetStockListings(StockService.StockServiceClient client)
        {
            var result = client.GetStockListings(new Empty());
            foreach (var stock in result.Stocks)
            {
                Console.WriteLine($"{stock.StockId}\t{stock.StockName}");
            }

            return result;
        }
    }
}
