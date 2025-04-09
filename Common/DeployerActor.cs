using System.Diagnostics;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;

namespace Common;

public class DeployerActor : ReceiveActor
{
	private readonly ILoggingAdapter _logger;
	private readonly List<Address> _clusterNodes = new();

	public DeployerActor()
	{
		_logger = Context.GetLogger();
		
		var cluster = Cluster.Get(Context.System);

		cluster.Subscribe(Self, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
			typeof(ClusterEvent.MemberUp), typeof(ClusterEvent.MemberRemoved));

		Receive<DeployActorCommand>(msg =>
		{
			if (_clusterNodes.Count == 0)
			{
				_logger.Error("Deployer: No nodes available for deployment");
				Sender.Tell(new Status.Failure(new InvalidOperationException("No nodes available")));
				return;
			}

			var targetAddress = Address.Parse("akka.tcp://deployer@localhost:8081");
			_logger.Info("Deployer: Deploying new actor '{ActorName}' to node at address '{Address}'", msg.ActorName, targetAddress);

			var deploy = Deploy.None.WithScope(new RemoteScope(targetAddress));
			var actorProps = msg.Props.WithDeploy(deploy);

			var activity = Activity.Current;
			
			var newActorRef = Context.System.ActorOf(actorProps, msg.ActorName);

			if (msg.InitialMessage is not null)
			{
				newActorRef.Tell(msg.InitialMessage, ActorRefs.NoSender);
			}
		});

		Receive<ClusterEvent.MemberUp>(UpdateClusterMembers);
		Receive<ClusterEvent.MemberRemoved>(UpdateClusterMembers);
	}

	private void UpdateClusterMembers(object message)
	{
		_logger.Info("Deployer: Updating cluster members, cause: {Cause}", message.GetType().Name);

		var previousNodeCount = _clusterNodes.Count;

		var cluster = Cluster.Get(Context.System);
		_clusterNodes.Clear();
		_clusterNodes.AddRange(cluster.State.Members
			.Where(m => m.Status is MemberStatus.Up or MemberStatus.Joining)
			.Select(m => m.Address));

		_logger.Info("Deployer: Updated cluster with {NodeCount} nodes (previous: {PreviousNodeCount})", _clusterNodes.Count, previousNodeCount);
	}

	private void Initialize(ClusterEvent.CurrentClusterState state)
	{
		_logger.Info("Deployer: Initializing cluster");

		_clusterNodes.Clear();
		_clusterNodes.AddRange(state.Members
			.Where(m => m.Status == MemberStatus.Up)
			.Select(m => m.Address));

		_logger.Info("Deployer: Initialized cluster with {NodeCount} nodes", _clusterNodes.Count);
	}

	protected override void PreStart()
	{
		var cluster = Cluster.Get(Context.System);
		Initialize(cluster.State);
		base.PreStart();
	}

	public record DeployActorCommand(Props Props, string ActorName, object? InitialMessage);
}
