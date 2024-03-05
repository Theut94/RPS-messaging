using EasyNetQ;
using Events;
using Helpers;
using Monolith;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Diagnostics;

public class Program
{
    private static Game game = new Game();

    public static async Task Main()
    {
        var connectionEstablished = false;

        using var bus = ConnectionHelper.GetRMQConnection();
        while (!connectionEstablished)
        {
            var subscriptionResult = bus.PubSub
                .SubscribeAsync<PlayerMovedEvent>("RPS", e =>
                {
                    var propagator = new TraceContextPropagator();
                    var parentContext = propagator.Extract(default, e, (r, key) =>
                    {
                        return new List<string>(new[] { r.Headers.ContainsKey(key) ? r.Headers[key].ToString() : String.Empty }!);
                    });
                    Baggage.Current = parentContext.Baggage;
                    var finishedEvent = game.ReceivePlayerEvent(e);

                     if (finishedEvent != null)
                     {
                         bus.PubSub.PublishAsync(finishedEvent);
                     }
                })
                .AsTask();

            await subscriptionResult.WaitAsync(CancellationToken.None);
            connectionEstablished = subscriptionResult.Status == TaskStatus.RanToCompletion;
            if (!connectionEstablished) Thread.Sleep(1000);
        }

        using (var activity = Helpers.Monitoring.ActivitySource.StartActivity())
        {
            var gameEvent = game.Start();
            var propagator = new TraceContextPropagator();
            var activityContext = activity?.Context ?? Activity.Current?.Context ?? default;
            var propagationContext = new PropagationContext(activityContext, Baggage.Current);
            propagator.Inject(propagationContext, gameEvent.Headers, (headers, key, value) => headers.Add(key, value));
            await bus.PubSub.PublishAsync(gameEvent);
        }

        while (true) Thread.Sleep(5000);
    }
}