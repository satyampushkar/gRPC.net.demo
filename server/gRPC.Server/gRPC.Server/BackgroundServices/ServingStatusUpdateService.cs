using Grpc.Health.V1;
using Grpc.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace gRPC.Server.BackgroundServices
{
    public class ServingStatusUpdateService : BackgroundService
    {
        private readonly HealthServiceImpl _healthService;
        private readonly HealthCheckService _healthCheckService;

        public ServingStatusUpdateService(HealthServiceImpl healthService, HealthCheckService healthCheckService)
        {
            _healthService = healthService;
            _healthCheckService = healthCheckService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var health = await _healthCheckService.CheckHealthAsync(stoppingToken);

                _healthService.SetStatus("StockDataService",
                    health.Status == HealthStatus.Healthy
                        ? HealthCheckResponse.Types.ServingStatus.Serving
                        : HealthCheckResponse.Types.ServingStatus.NotServing);

                _healthService.SetStatus(string.Empty,
                    health.Status == HealthStatus.Healthy
                        ? HealthCheckResponse.Types.ServingStatus.Serving
                        : HealthCheckResponse.Types.ServingStatus.NotServing);

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
