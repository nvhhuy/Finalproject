namespace Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "Messaging:RabbitMq";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    /// <summary>Default exchange for publishes when <see cref="MessagePublishContext.TopicOrExchange"/> is empty.</summary>
    public string DefaultExchange { get; set; } = "finalproject.events";
}
