using Events;
using Helpers;
using OpenTelemetry.Context.Propagation;
using Serilog;

namespace CopyPlayerService;

public class CopyPlayer : IPlayer
{
    private const string PlayerId = "The Copy Cat";
    private readonly Queue<Move> _previousMoves = new Queue<Move>();

    public PlayerMovedEvent MakeMove(GameStartedEvent e)
    {

        var propagator = new TraceContextPropagator();
        var parentContext = propagator.Extract(default, e, (r, key) =>
        {
            return new List<string>(new[] { r.Headers.ContainsKey(key) ? r.Headers[key].ToString() : String.Empty }!);
        });
        using var activity = Helpers.Monitoring.ActivitySource.StartActivity(" Making move", System.Diagnostics.ActivityKind.Consumer, parentContext.ActivityContext);
        
        Move move = Move.Paper;
        if (_previousMoves.Count > 2)
        {
            move = _previousMoves.Dequeue();
        }
        Log.Logger.Debug("Player {PlayerId} has decided to perform the move {Move}", PlayerId, move);


        var moveEvent = new PlayerMovedEvent
        {
            GameId = e.GameId,
            PlayerId = PlayerId,
            Move = move
        };
        return moveEvent;
    }

    public void ReceiveResult(GameFinishedEvent e)
    {
        using var activity = Monitoring.ActivitySource.StartActivity();
        
        var otherMove = e.Moves.SingleOrDefault(m => m.Key != PlayerId).Value;
        _previousMoves.Enqueue(otherMove);
    }
}