using Akka.Actor;

namespace Common;

public class HelloActor : ReceiveActor
{
    public HelloActor()
    {
        ReceiveAny(cmd =>
        {
            Console.WriteLine("Received command: " + cmd + " from " + Context.Sender.Path);
        });
    }
}