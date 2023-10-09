using System.Runtime.ExceptionServices;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThreeFourteen.Finnhub.Client;
using ThreeFourteen.Finnhub.Client.Model;

namespace cclauson
{
    public class BaseTableEntity : ITableEntity {
        public string? PartitionKey { get; set; }
        public string? RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    public class ValueEntity : BaseTableEntity {
        public decimal? value { get; set; }
        public DateTime? dateTime { get; set; }
    }

    public class TechnicalAnalysis
    {
        private readonly ILogger _logger;
        private readonly FinnhubClient finnhubClient;
        private readonly TableClient tableClient;

        public TechnicalAnalysis(ILoggerFactory loggerFactory)
        {
            try {
                _logger = loggerFactory.CreateLogger<TechnicalAnalysis>();
                var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
                bool isLocal = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
                string? finnhubClientKey;
                if (isLocal) {
                    // Based on:
                    // https://swharden.com/blog/2021-10-09-console-secrets/
                    // see for instructions on how to add client key value
                    finnhubClientKey = config["finnhub-client-key"];
                } else {
                    finnhubClientKey = Environment.GetEnvironmentVariable("FinnhubClientKey");
                }
                if (finnhubClientKey == null) {
                    throw new KeyNotFoundException("Couldn't find finnhub client key");
                }
                finnhubClient = new(finnhubClientKey);
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                _logger.LogInformation($"Connection string: {connectionString}");
                this.tableClient = new TableClient(connectionString, "values");
            } catch (Exception e) {
                _logger.LogInformation($"Exception: ${e}");
                ExceptionDispatchInfo.Capture(e);
            }
        }

        [Function("TechnicalAnalysis")]
        public Task Run([TimerTrigger("%TimerSchedule%")] MyInfo myTimer, ExecutionContext context)
        {
            return this.RunI(myTimer, context);
        }

        private async Task RunI(MyInfo myTimer, ExecutionContext context) {
            Quote quote = await this.finnhubClient.Stock.GetQuote("MSFT");
            decimal price = quote.Current;
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            _logger.LogInformation($"Current quote: {price}");
            this.tableClient.AddEntity<ValueEntity>(new ValueEntity() {
                PartitionKey = "TestPartitionKey",
                RowKey = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff"),
                value = price,
                dateTime = DateTime.UtcNow
            });
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
