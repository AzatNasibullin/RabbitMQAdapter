namespace RabbitMQAdapter.Models { 
public class Consumer
{
    public int Id { get; set; }
    public string ProfileName { get; set; }
    public string ProfilePassword { get; set; }
    public string CallbackUrl { get; set; }
    public string QueueName { get; set; }
    public string ExchangeName { get; set; }
    public string ConnectionInfo { get; set; }

    }
}