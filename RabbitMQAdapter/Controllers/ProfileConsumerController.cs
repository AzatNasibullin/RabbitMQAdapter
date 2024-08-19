using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQAdapter.Models;
using RabbitMQ.Client;
using System.Text.Json;
using System.Text;
using System.Threading.Channels;

[ApiController]
[Route("[controller]")]
public class ProfileConsumerController : ControllerBase 
{
    private readonly AppDbContext _context;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    
    public ProfileConsumerController(AppDbContext context)
    {
        _context = context; // Инициализация контекста базы данных

        // Установка соединения RabbitMQ
        var factory = new ConnectionFactory() { HostName = "localhost" }; // Настройте при необходимости
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    [HttpPost("SetProfile")]
    public async Task<IActionResult> SetProfile([FromBody] Profile profile)
    {
        if (profile == null)
        {
            return BadRequest("Profile cannot be null.");
        }
        // Проверка существования профиля с таким же именем
        var existingProfile = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Name == profile.Name);

        if (existingProfile != null)
        {
            return Conflict("Profile with this name already exists.");
        }

        _context.Profiles.Add(profile);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(SetProfile), new { id = profile.Id }, profile);
    }

    [HttpPost("SetConsumer")]
    public async Task<IActionResult> SetConsumer([FromBody] Consumer consumer)
    {
        if (consumer == null)
        {
            return BadRequest("Consumer cannot be null.");
        }
        // Проверка, существует ли профиль с указанным именем и паролем доступа
        var existingProfile = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Name == consumer.ProfileName && p.AccessPassword == consumer.ProfilePassword);

        if (existingProfile == null)
        {
            return NotFound("Profile not found or incorrect data entered.");
        }

        _context.Consumers.Add(consumer);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(SetConsumer), new { id = consumer.Id }, consumer);
    }

    [HttpPost("SendMessage")]
    public IActionResult SendMessage([FromBody] Message message)
    {
        if (message == null)
        {
            return BadRequest("Message cannot be null.");
        }
        // Проверка, существует ли профиль с указанным именем и паролем доступа
        var profileExists = _context.Profiles.Any(p => p.Name == message.ProfileName && p.AccessPassword == message.ProfilePassword); 
        if (!profileExists)
        {
            return NotFound("Profile not found or incorrect data entered.");
        }

        // Отправка сообщения в указанную очередь
        SendMessageToQueue(message.Queue, message.Msg , message.Exchange);

        return Ok("Message sent to the queue.");
    }

    private void SendMessageToQueue(string queue, string msg, string exchange)
    {
        // Сериализация сообщения
        var messageBody = JsonSerializer.Serialize(msg);
        var body = Encoding.UTF8.GetBytes(messageBody);

        // Проверим, что очередь существует
        _channel.QueueDeclare(queue: queue,
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        // Отправка сообщения в указанную очередь
        _channel.BasicPublish(exchange: exchange,
                             routingKey: queue,
                             basicProperties: null,
                             body: body);
    }
}