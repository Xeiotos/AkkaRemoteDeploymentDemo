# AkkaRemoteDeploymentDemo

This is a reproduction of an issue that occurs when using Akka.NET remote deployment with Phobos

## Issue Description

For unknown reasons (maybe because the Akka.Remote.DaemonMsgCreate is wrapped in a Phobos.Tracing.SpanEnvelope?) remote 
deployment of an actor fails silently when using Phobos & when there is a current Activity.

The issue does not occur when there is no current activity (such as when manually setting `Activity.Current = null`).

For convencience, two endpoints are provided: 

POST deploy: Directly sends a message from the endpoint to the DeployerActor. Activity.Current is null.
<br>

Deployment succeeds, the HelloAndDieActor receives a message.

POST deploy-intermediate: Sends the same message through an intermediate actor, causing Activity.Current to contain a receive activity.
<br>

Deployment fails (or does not happen?)

## Notes

A NuGet.config must be created in the root of the project to restore the Phobos package.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
        <add key="Phobos" value="https://feed.sdkbin.com/auth/{YOUR-KEY}/v3/index.json" />
    </packageSources>
</configuration>
```

```json
    "remote": {
      "log-received-messages": true,
      "log-sent-messages": true
    },
```

Can be used to observe the effects more in-depth.