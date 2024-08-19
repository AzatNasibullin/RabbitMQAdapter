namespace RabbitMQAdapter.Models { 
public class Message
{
    public string ProfileName { get; set; }
    public string ProfilePassword { get; set; }
    public string Queue { get; set; }
    public string Exchange { get; set; }
    public string Msg { get; set; }
}
}