using Akka.Actor;
using Akka.Hosting;

namespace Deployer;

public class IntermediateActor: ReceiveActor
{
    public IntermediateActor(ActorRegistry registry)
    {
        Receive<Common.DeployerActor.DeployActorCommand>(message =>
        {
            var deployer = registry.Get<Common.DeployerActor>();
            deployer.Forward(message);
        });
    }
}