using EasyNetQ;
using Events;
using Helpers;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Diagnostics;

namespace CopyPlayerService;

public static class Program
{
    private static readonly IPlayer Player = new CopyPlayer();
    
    public static async Task Main()
    {
        var connectionEstablished = false;

        while (!connectionEstablished)
        {
            var bus = ConnectionHelper.GetRMQConnection();
            var subscriptionResult = bus.PubSub.SubscribeAsync<GameStartedEvent>("copy", e =>
            {
               
                    var propagator = new TraceContextPropagator();
                    var parentContext = propagator.Extract(default, e, (r, key) =>
                    {
                        return new List<string>(new[] { r.Headers.ContainsKey(key) ? r.Headers[key].ToString() : String.Empty }!);
                    });
                    Baggage.Current = parentContext.Baggage;
                    var moveEvent = Player.MakeMove(e);
               using (var activity = Helpers.Monitoring.ActivitySource.StartActivity("Received task", ActivityKind.Consumer, parentContext.ActivityContext))
                {
                        var activityContext = activity?.Context ?? Activity.Current?.Context ?? default;
                    var propagationContext = new PropagationContext(activityContext, Baggage.Current);
                    propagator.Inject(propagationContext, moveEvent.Headers, (headers, key, value) => headers.Add(key, value));

                    bus.PubSub.PublishAsync(moveEvent);
                }
            }).AsTask();

            await subscriptionResult.WaitAsync(CancellationToken.None);
            connectionEstablished = subscriptionResult.Status == TaskStatus.RanToCompletion;
            if(!connectionEstablished) Thread.Sleep(1000);
        }

        while (true) Thread.Sleep(5000);
    }
}