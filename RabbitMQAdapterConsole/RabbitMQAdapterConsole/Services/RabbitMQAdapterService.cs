using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQAdapterConsole.Data;
using Serilog;

namespace RabbitMQAdapterConsole.Services
{
    public class RabbitMqListener : BackgroundService
    {
        private readonly ILogger<RabbitMqListener> _logger;
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        // Используем ConcurrentDictionary для потокобезопасности
        private readonly ConcurrentDictionary<string, (IConnection connection, IModel channel)> _activeListeners = new ConcurrentDictionary<string, (IConnection, IModel)>();
        private Timer _queueUpdateTimer;
        
        public RabbitMqListener(ILogger<RabbitMqListener> logger, AppDbContext dbContext, IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _httpClient = new HttpClient();
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Начальная загрузка слушателей
            await LoadListenersAsync(stoppingToken); // Асинхронно загружаем слушателей

            // Запускаем таймер для периодического обновления очередей
            while (!stoppingToken.IsCancellationRequested)
            {
                // Обновляем слушатели
                await UpdateListenersAsync(null); // Вызываем ваш асинхронный метод

                // Ждем минуту
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task LoadListenersAsync(CancellationToken token)
        {
            // Создаем новый контекст для загрузки очередей
            var optionsBuilder = CreateDbContextOptions();

            using (var dbContext = new AppDbContext(optionsBuilder, _configuration))
            {
                var queues = await dbContext.Consumers.ToListAsync(token); // Асинхронно загружаем очереди

                foreach (var queue in queues)
                {
                    if (!_activeListeners.ContainsKey(queue.QueueName))
                    {
                        ListenToQueue(queue.QueueName, queue.CallbackUrl, token);
                    }
                }
            }
        }

        private async Task UpdateListenersAsync(object state)
        {
            // Создаем новый контекст для обновления очередей
            var optionsBuilder = CreateDbContextOptions();

            using (var dbContext = new AppDbContext(optionsBuilder, _configuration))
            {
                var queues = await dbContext.Consumers.ToListAsync(CancellationToken.None);  // Передайте CancellationToken, если необходимо

                foreach (var queue in queues)
                {
                    if (!_activeListeners.ContainsKey(queue.QueueName))
                    {
                        ListenToQueue(queue.QueueName, queue.CallbackUrl, CancellationToken.None);
                    }
                }

                var queuesToRemove = _activeListeners.Keys
                    .Where(q => !queues.Any(dbQueue => dbQueue.QueueName == q)).ToList();

                foreach (var queueToRemove in queuesToRemove)
                {
                    StopListeningToQueue(queueToRemove);
                }
            }
        }

        private DbContextOptions<AppDbContext> CreateDbContextOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer("Data Source = WIN-Q595KQ6H928\\SQLEXPRESS; Initial Catalog = RabbitMQAdapterDB; Trusted_Connection=True; TrustServerCertificate=True");
            return optionsBuilder.Options;
        }

        // Остальные методы остаются без изменений

        private void StopListeningToQueue(string queueName)
        {
            if (_activeListeners.TryRemove(queueName, out var listener))
            {
                // Закрываем соединение и канал
                listener.channel.Close();
                listener.connection.Close();
                _logger.LogInformation($"Stopped listening to queue: {queueName}");
            }
        }

        private void ListenToQueue(string queueName, string destinationUrl, CancellationToken token)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" }; // Укажите адрес вашего RabbitMQ сервера
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            // Объявляем очередь, если она ещё не создана
            channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                string isDeliveredSuccessfully = await SendMessageAsync(message, destinationUrl);

                if (isDeliveredSuccessfully == "")
                {
                    channel.BasicAck(ea.DeliveryTag, false); // Подтверждаем получение и обработку сообщения
                }
                else
                {
                    var properties = channel.CreateBasicProperties();
                    properties.Headers = new Dictionary<string, object>
{
{ "ErrorMessage", Encoding.UTF8.GetBytes(isDeliveredSuccessfully) }
};

                    // Переотправляем сообщение в ту же очередь в случае ошибки
                    channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);
                    channel.BasicNack(ea.DeliveryTag, false, true); // Отклоняем оригинальное сообщение без возврата
                }
            };

            // Начинаем прослушивание очереди
            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            // Добавляем слушателя в список активных
            _activeListeners.TryAdd(queueName, (connection, channel));
        }

        private async Task<string> SendMessageAsync(string message, string destinationUrl)
        {
            try
            {
                var content = new StringContent(message, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(destinationUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully sent message to {destinationUrl}");
                    return ""; // Успех
                }
                else
                {
                    _logger.LogError($"Error sending message to {destinationUrl}: {response.ReasonPhrase}");
                    return $"Error sending message to {destinationUrl}: {response.ReasonPhrase}"; // Ошибка
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception sending message to {destinationUrl}: {ex.Message}");
                return $"Exception sending message to {destinationUrl}: {ex.Message}"; // Ошибка
            }
        }
    }
}