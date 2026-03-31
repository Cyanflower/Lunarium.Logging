using Lunarium.Logging;
using Lunarium.Logging.Config.Models;
using Lunarium.Logging.Models;

// ── Logger setup ──────────────────────────────────────────────────────────────
ILogger logger = new LoggerBuilder()
    .SetLoggerName("MyApp")
    .AddConsoleSink(isColor: true, FilterConfig: new FilterConfig { LogMinLevel = LogLevel.Debug })
    .Build();

// ── Section 1: Log levels ─────────────────────────────────────────────────────
Console.WriteLine("== Color Console Example ====================================================");
Console.WriteLine("── Log Levels ───────────────────────────────────────────────────────────────");
logger.Debug("Application initializing, environment: {Env}", "Production");
logger.Info("Server started on port {Port}", 8080);
logger.Warning("High memory usage: {UsageMB} MB", 1024);
logger.Error("Disk quota exceeded on volume {Volume}", "/var/log");

try { throw new Exception("Connection timeout"); }
catch (Exception ex) { logger.Error(ex, "Request failed for user {UserId}", 42); }

logger.Critical("Database unreachable, shutting down");
await Task.Delay(100);

// ── Section 2: Structured destructuring ──────────────────────────────────────
Console.WriteLine("── Structured Destructuring ─────────────────────────────────────────────────");
var order = new Order(42, 99.99m);
logger.Info("Order created: {@Order}", order);
logger.Info("Status: {$Status}", OrderStatus.Active);
logger.Info("Price: {Amount,10:F2} USD", 42.5);
await Task.Delay(100);

// ── Section 3: Context and scoping ───────────────────────────────────────────
Console.WriteLine("── Context and Scoping ──────────────────────────────────────────────────────");
ILogger orderLog = logger.ForContext("Order.Processor");
orderLog.Info("Processing order {Id}", 1001);

ILogger validatorLog = orderLog.ForContext("Validator");
validatorLog.Info("Validating order {Id}", 1001);
validatorLog.Warning("Order {Id} has unusual total: {Total}", 1001, 9999.99m);

await logger.DisposeAsync();

// ── Types ─────────────────────────────────────────────────────────────────────
record Order(int Id, decimal Total);
enum OrderStatus { Active, Pending, Cancelled }
