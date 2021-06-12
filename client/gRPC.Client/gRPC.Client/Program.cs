using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using gRPC.net.demo.stockDetails;
using System.Threading;
using Grpc.Core;
using Grpc.Health.V1;

namespace gRPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new StockService.StockServiceClient(channel);
            Metadata headers = await FetchAuthenticationToken(client);

            while (true)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine("******************************************");
                    Console.WriteLine("Press 1 to GetStockListings");
                    Console.WriteLine("Press 2 to GetStockPrice");
                    Console.WriteLine("Press 3 to GetStockPriceStream");
                    Console.WriteLine("Press 4 to GetStocksPrices");
                    Console.WriteLine("Press 5 to GetCompanyStockPriceStream");
                    Console.WriteLine("Press 6 to check service health");
                    Console.WriteLine("Press 7 to Exit from app");
                    Console.WriteLine("******************************************");
                    Console.ResetColor();
                    var input = Console.ReadLine();
                    switch (input)
                    {
                        case "1":
                            await GetStockListings(client, headers);
                            break;
                        case "2":
                            await GetStockPrice(client, headers);
                            break;
                        case "3":
                            await GetStockPriceStream(client, headers);
                            break;
                        case "4":
                            await GetStocksPrices(client, headers);
                            break;
                        case "5":
                            await GetCompanyStockPriceStream(client, headers);
                            break;
                        case "6":
                            await CheckServiceHealth(channel);
                            break;
                        case "7":
                            Environment.Exit(0);
                            break;
                        default:
                            break;
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
                {
                    //Assuming token expired, renewing..
                    Console.WriteLine("Token expired/rejected. Fetching latest...");
                    headers = await FetchAuthenticationToken(client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in app: " + ex);
                }
            }
        }

        private static async Task GetStockListings(StockService.StockServiceClient client, Metadata headers)
        {
            var result = await client.GetStockListingsAsync(new Empty(), headers: headers);
            foreach (var stock in result.Stocks)
            {
                Console.WriteLine($"{stock.StockId}\t{stock.StockName}");
            }
        }

        private static async Task GetStockPrice(StockService.StockServiceClient client, Metadata headers)
        {
            var result = await client.GetStockPriceAsync(new Stock { StockId = "FB" }, headers: headers);
            Console.WriteLine($"Stock Id: {result.Stock.StockId}\n Stock Name: {result.Stock.StockName}\n " +
                $"Stock Price: {result.Price}\n TimeStamp: {result.DateTimeStamp}");
        }

        private static async Task GetStockPriceStream(StockService.StockServiceClient client, Metadata headers)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            using var streamingCall = client.GetStockPriceStream(new Empty(), cancellationToken: cts.Token, headers: headers);
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

        private static async Task GetStocksPrices(StockService.StockServiceClient client, Metadata headers)
        {
            using var streamingCall = client.GetStocksPrices(headers: headers);

            //send requests through requst stream
            foreach (var stockId in new[] { "FB", "AAPL", "AMZN", "MSFT", "GOOG" })
            {
                Console.WriteLine($"Requesting details for {stockId}...");

                await streamingCall.RequestStream.WriteAsync(new Stock { StockId = stockId });

                //Mimicing delay in sending request
                await Task.Delay(1500);
            }

            Console.WriteLine("Completing request stream");
            await streamingCall.RequestStream.CompleteAsync();
            Console.WriteLine("Request stream completed");

            var response = await streamingCall;
            foreach (var stockPrice in response.StockPriceList)
            {
                Console.WriteLine(string.Format($"{stockPrice.Stock.StockId}\t {stockPrice.Stock.StockName}\t {stockPrice.Price}\t {stockPrice.DateTimeStamp}"));
            }
            Console.WriteLine();
        }
                
        private static async Task GetCompanyStockPriceStream(StockService.StockServiceClient client, Metadata headers)
        {
            //To get all stock Ids
            //var stocks = client.GetStockListings(new Empty()).Stocks; 
            using var streamingCall = client.GetCompanyStockPriceStream(headers: headers);

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

        private static async Task CheckServiceHealth(GrpcChannel channel)
        {
            var healthClient = new Health.HealthClient(channel);

            var health = await healthClient.CheckAsync(new HealthCheckRequest { Service = "StockDataService" });

            Console.WriteLine($"Health Status: {health.Status}");
        }

        private static async Task<Metadata> FetchAuthenticationToken(StockService.StockServiceClient client)
        {
            var authToken = await client.AuthenticateAsync(new ClientCred { ClientId = "clientId1", ClientSecret = "secret1" });
            var headers = new Metadata();
            headers.Add("Authorization", string.Format($"Bearer {authToken.BearerToken}"));
            return headers;
        }
    }
}
