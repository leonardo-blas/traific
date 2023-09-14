# Multiplay SDK

The Multiplay SDK provides various functionality to setup a dedicated game server using Multiplay.

The Multiplay SDK depends on the Operate Core SDK.

To use the SDK you must initialize the Operate Core SDK.

```csharp
using Unity.Services.Core;
using Unity.Services.Multiplay;
```

```csharp
try
{
    await UnityServices.Initialize();
}
catch (Exception e)
{
    Debug.Log(e);
}
```

You should then be able to access the Multiplay SDK using a singleton interface:

```csharp
await MultiplayService.Instance.ReadyServerForPlayersAsync();
```

## Multiplay OneAction Authoring

The Multiplay SDK provides authoring tools to simplify your experience 
while developing Multiplay servers. 

In order to access the authoring tools you must be using Unity 2021.3.0f1 or newer. 
