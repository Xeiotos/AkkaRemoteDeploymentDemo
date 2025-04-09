using Akka.Actor;
using Akka.Cluster;
using Akka.Event;

namespace Common;

public class RoundRobinDeployer : ReceiveActor
{
	private readonly ILoggingAdapter _logger;
	private readonly List<Address> _clusterNodes = new();
	private int _nodeIndex;

	public RoundRobinDeployer()
	{
		_logger = Context.GetLogger();

		var cluster = Cluster.Get(Context.System);

		cluster.Subscribe(Self, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
			typeof(ClusterEvent.MemberUp), typeof(ClusterEvent.MemberRemoved));

		Receive<DeployActorCommand>(msg =>
		{
			if (_clusterNodes.Count == 0)
			{
				_logger.Error("RoundRobinDeployer: No nodes available for deployment");
				Sender.Tell(new Status.Failure(new InvalidOperationException("No nodes available")));
				return;
			}

			_logger.Info("RoundRobinDeployer: Deploying new actor '{ActorName}' to node with index {NodeIndex} and address '{Address}'", msg.ActorName, _nodeIndex, Address.Parse("akka.tcp://deployer@localhost:8081"));

			var deploy = Deploy.None.WithScope(new RemoteScope(Address.Parse("akka.tcp://deployer@localhost:8081")));
			var actorProps = msg.Props.WithDeploy(deploy);

			// For now, we only use this actor to deploy pipelines. Pipelines should not be parented to a singleton
			// In AKKA.NET, a singleton can and will get moved between nodes, which causes any children to be killed.
			// https://getakka.net/articles/clustering/cluster-singleton.html#potential-problems-to-be-aware-of
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
		_logger.Info("RoundRobinDeployer: Updating cluster members, cause: {Cause}", message.GetType().Name);

		var previousNodeCount = _clusterNodes.Count;

		var cluster = Cluster.Get(Context.System);
		_clusterNodes.Clear();
		_clusterNodes.AddRange(cluster.State.Members
			.Where(m => m.Status is MemberStatus.Up or MemberStatus.Joining)
			.Select(m => m.Address));

		_logger.Info("RoundRobinDeployer: Updated cluster with {NodeCount} nodes (previous: {PreviousNodeCount})", _clusterNodes.Count, previousNodeCount);
	}

	private void Initialize(ClusterEvent.CurrentClusterState state)
	{
		_logger.Info("RoundRobinDeployer: Initializing cluster");

		_clusterNodes.Clear();
		_clusterNodes.AddRange(state.Members
			.Where(m => m.Status == MemberStatus.Up)
			.Select(m => m.Address));

		_logger.Info("RoundRobinDeployer: Initialized cluster with {NodeCount} nodes", _clusterNodes.Count);
	}

	protected override void PreStart()
	{
		var cluster = Cluster.Get(Context.System);
		Initialize(cluster.State);
		base.PreStart();
	}

	public record DeployActorCommand(Props Props, string ActorName, object? InitialMessage);
}
