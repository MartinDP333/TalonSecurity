using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using Shared.Contracts;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IWafAuditParser, WafAuditParser>();
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "rabbitmq";
        var user = Environment.GetEnvironmentVariable("RabbitMQ__UserName") ?? "guest";
        var password = Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest";
        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(password);
        });
    });
});

builder.Services.AddHostedService<WafAuditReader>();
await builder.Build().RunAsync();

public interface IWafAuditParser
{
    WafThreatEvent? Parse(string line);
}

public sealed class WafAuditParser : IWafAuditParser
{
    private static readonly Regex RuleRegex = new("\\[id \\\"(?<id>\\d+)\\\\?\\\"\\] \\\\[msg \\\"(?<message>[^\\\"]+)", RegexOptions.Compiled);

    public WafThreatEvent? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith("{"))
            return null;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("transaction", out var transaction)) return null;
            if (!transaction.TryGetProperty("is_interrupted", out var interrupted) || !interrupted.GetBoolean()) return null;
            if (!transaction.TryGetProperty("request", out var request)) return null;

            var responseCode = transaction.GetProperty("response").GetProperty("http_code").GetInt32();
            if (responseCode < 400) return null;

            var uniqueId = transaction.GetProperty("unique_id").GetString() ?? Guid.NewGuid().ToString("N");
            var ip = transaction.GetProperty("client_ip").GetString() ?? "unknown";
            var path = request.GetProperty("uri").GetString() ?? "/";
            var raw = root.GetRawText();
            var message = root.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0
                ? messages[0].GetProperty("message").GetString() ?? "WAF rule matched"
                : "WAF rule matched";
            var ruleId = root.TryGetProperty("messages", out messages) && messages.GetArrayLength() > 0 && messages[0].TryGetProperty("details", out var details) && details.TryGetProperty("ruleId", out var rule)
                ? rule.GetString() ?? "unknown"
                : "unknown";

            return new WafThreatEvent
            {
                EventId = uniqueId,
                SourceIp = ip,
                RequestPath = path,
                AttackType = Categorize(ruleId, message),
                RuleId = ruleId,
                RuleMessage = message,
                RawPayload = raw,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (JsonException) { return null; }
    }

    private static string Categorize(string ruleId, string message)
    {
        if (ruleId.StartsWith("941") || message.Contains("cross-site", StringComparison.OrdinalIgnoreCase) || message.Contains("XSS", StringComparison.OrdinalIgnoreCase)) return "Cross-Site Scripting";
        if (ruleId.StartsWith("942") || message.Contains("SQL", StringComparison.OrdinalIgnoreCase)) return "SQL Injection";
        if (ruleId.StartsWith("932") || message.Contains("command", StringComparison.OrdinalIgnoreCase)) return "Command Injection";
        if (ruleId.StartsWith("920")) return "Protocol Violation";
        if (ruleId == "949110" || message.Contains("anomaly", StringComparison.OrdinalIgnoreCase)) return "WAF Anomaly Block";
        return "WAF Anomaly Block";
    }
}

public sealed class WafAuditReader : BackgroundService
{
    private readonly IWafAuditParser _parser;
    private readonly IPublishEndpoint _publisher;
    private readonly ILogger<WafAuditReader> _logger;
    private readonly string _path = Environment.GetEnvironmentVariable("WAF_AUDIT_LOG") ?? "/var/log/modsecurity/audit.log";

    public WafAuditReader(IWafAuditParser parser, IPublishEndpoint publisher, ILogger<WafAuditReader> logger) => (_parser, _publisher, _logger) = (parser, publisher, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!File.Exists(_path)) { await Task.Delay(1000, stoppingToken); continue; }
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (!stoppingToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(stoppingToken);
                if (line == null) { await Task.Delay(500, stoppingToken); continue; }
                var item = _parser.Parse(line);
                if (item == null) continue;
                await _publisher.Publish(item, stoppingToken);
                _logger.LogInformation("Published WAF event {EventId} as {AttackType}", item.EventId, item.AttackType);
            }
        }
    }
}
