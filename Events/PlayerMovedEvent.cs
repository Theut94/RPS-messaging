namespace Events;

public class PlayerMovedEvent
{
    public Guid GameId { get; set; }
    public string PlayerId { get; set; }
    public Move Move { get; set; }
    public Dictionary<string, object> Headers { get; } = new ();
}