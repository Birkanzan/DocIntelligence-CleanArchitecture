using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DocIntelligence.Domain.Interfaces;
using DocIntelligence.Infrastructure.Options;

namespace DocIntelligence.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ tabanlı message broker implementasyonu.
/// Azure Service Bus kullanmak istersen sadece bu sınıfı değiştirmen yeterli.
/// </summary>
public class RabbitMqMessageBroker : IMessageBroker, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessageBroker> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public RabbitMqMessageBroker(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessageBroker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
                return;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("RabbitMQ bağlantısı kuruldu: {Host}:{Port}", _options.Host, _options.Port);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default) where T : class
    {
        await EnsureConnectedAsync(cancellationToken);

        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8"
        };

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogDebug("Mesaj gönderildi: Queue={Queue}, Type={Type}", queueName, typeof(T).Name);
    }

    public async Task SubscribeAsync<T>(
        string queueName,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) where T : class
    {
        await EnsureConnectedAsync(cancellationToken);

        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<T>(json);

                if (message is not null)
                    await handler(message, cancellationToken);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                _logger.LogDebug("Mesaj işlendi: Queue={Queue}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj işlenirken hata: Queue={Queue}", queueName);
                // Mesajı kuyruğa geri at (requeue=false → dead-letter kuyruğa gider)
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Kuyruk dinleniyor: {Queue}", queueName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
