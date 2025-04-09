using Akka.Actor;
using Akka.Event;

namespace Common;

public class HelloAndDieActor : ReceiveActor
{
    public HelloAndDieActor()
    {
        var logger = Context.GetLogger();
        
        Receive<SayHelloAndDie>(cmd =>
        {
            logger.Info("Received command: " + cmd + " from " + Context.Sender.Path);
            Context.Stop(Self);
        });
    }

    public record SayHelloAndDie();
}