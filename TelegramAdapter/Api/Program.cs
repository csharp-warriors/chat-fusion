using ChatFusion.TelegramAdapter.Api.Options;
using ChatFusion.TelegramAdapter.Api.Services;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<AdapterOptions>()
    .Bind(builder.Configuration.GetSection(AdapterOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Redis");
    var redisOptions = ConfigurationOptions.Parse(connectionString);
    redisOptions.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(redisOptions);
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaOptions = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<IProducer<string, string>>>();

    var config = new ProducerConfig
    {
        BootstrapServers = kafkaOptions.BootstrapServers,
        EnableIdempotence = true,
        MessageTimeoutMs = 10000,
        Acks = Acks.All
    };

    return new ProducerBuilder<string, string>(config).SetErrorHandler((_, error) => logger.LogError("Ошибка Kafka producer: {KafkaError}", error.Reason)).Build();
});

builder.Services.AddScoped<TelegramService>();

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();
app.MapOpenApi();

app.MapScalarApiReference(options => options.WithTitle("ChatFusion Telegram Adapter"));
app.UseAuthorization();
app.MapControllers();

app.Lifetime.ApplicationStopping.Register(() => app.Services.GetRequiredService<IProducer<string, string>>().Flush(TimeSpan.FromSeconds(5)));
app.Run();
