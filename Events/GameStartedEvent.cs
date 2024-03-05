namespace Events;

public class GameStartedEvent
{
    public Guid GameId { get; set; }
    public Dictionary<string, object> Headers { get; } = new();
}