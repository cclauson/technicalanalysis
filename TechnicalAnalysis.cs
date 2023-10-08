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
            _logger = loggerFactory.CreateLogger<TechnicalAnalysis>();
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            // Based on:
            // https://swharden.com/blog/2021-10-09-console-secrets/
            // see for instructions on how to add client key value
            var finnhubClientKey = config["finnhub-client-key"];
            if (finnhubClientKey == null) {
                throw new KeyNotFoundException("Couldn't find finnhub client key");
            }
            finnhubClient = new(finnhubClientKey);
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            this.tableClient = new TableClient(connectionString, "values");
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
