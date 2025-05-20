# NTDLS.ReliableMessaging

📦 Be sure to check out the NuGet package: https://www.nuget.org/packages/NTDLS.ReliableMessaging

NTDLS.ReliableMessaging provides incredibly lightweight, simple, and high-performance TCP/IP based inter-process-communication / RPC functionality. This includes a server which listens for incoming connections and a client which makes a connection to the server.

Once connected, the server and the client can send fire-and-forget style notifications or dispatch queries which require a reply.
All messages are handled by either convention or by events. Convention is achieved by adding a hander class with function signatures that match the message types which are being dispatched.

All messages are guaranteed to be received in their entirety and in the order in which they were dispatched.

CRC and compression are automatic and encryption is supported but optional.

## Examples of sending messages and receiving them by convention:
## Server:
  > Start a server and add a handler class.

```csharp
static void Main()
{
    var server = new RmServer();

    server.AddHandler(new HandlerMethods());

    server.Start(31254);

    Console.WriteLine("Press [enter] to shutdown.");
    Console.ReadLine();

    server.Stop();
}
```

## Client Example (Handlers by Convention):
  > Start a client, send a few notifications, a query and receive a query reply.

```csharp
static void Main()
{
    //Start a client and connect to the server.
    var client = new RmClient();

    client.Connect("localhost", 31254);

    client.Notify(new MyNotification("This is message 001 from the client."));
    client.Notify(new MyNotification("This is message 002 from the client."));
    client.Notify(new MyNotification("This is message 003 from the client."));

    //Send a query to the server, wait on a reply.
    client.Query(new MyQuery("This is the query from the client.")).ContinueWith(x =>
    {
        //If we recevied a reply, print it to the console.
        if (x.IsCompletedSuccessfully && x.Result != null)
        {
            Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
        }
        else
        {
            Console.WriteLine($"Exception: '{x.Exception?.GetBaseException()?.Message}'");
        }
    });

    Console.WriteLine("Press [enter] to shutdown.");
    Console.ReadLine();

    //Cleanup.
    client.Disconnect();
}
```

## Message handler Example:
  > Classes like this can be added to the server or the client to handle incomming notifications or queries.

```csharp
internal class HandlerMethods : IReliableMessagingMessageHandler
{
    public void MyNotificationReceived(RmContext context, MyNotification notification)
    {
        Console.WriteLine($"Server received notification: {notification.Message}");
    }

    public MyQueryReply MyQueryReceived(RmContext context, MyQuery query)
    {
        Console.WriteLine($"Server received query: '{query.Message}'");
        return new MyQueryReply("This is the query reply from the server.");
    }
}
```

## Examples of sending messages and receiving them by events.
```csharp
static void Main()
{
    var server = new RmServer();

    server.OnNotificationReceived += Server_OnNotificationReceived;
    server.OnQueryReceived += Server_OnQueryReceived;
    server.OnException += Server_OnException;

    server.Start(31254);

    Console.WriteLine("Press [enter] to shutdown.");
    Console.ReadLine();

    server.Stop();
}

private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload payload)
{
    if (payload is MyQuery myQuery)
    {
        Console.WriteLine($"Server received query: '{myQuery.Message}'");
        return new MyQueryReply("This is the query reply from the server.");
    }
    else
    {
        throw new Exception("Payload type was not handled.");
    }
}

private static void Server_OnNotificationReceived(RmContext context, IRmNotification payload)
{
    if (payload is MyNotification myNotification)
    {
        Console.WriteLine($"Server received notification: {myNotification.Message}");
    }
    else
    {
        throw new Exception("Payload type was not handled.");
    }
}
```

## Payloads:
  > Classes that implement IReliableMessagingNotification are fire-and-forget type messages.

```csharp
public class MyNotification : IReliableMessagingNotification
{
    public string Message { get; set; }

    public MyNotification(string message)
    {
        Message = message;
    }
}
```

> Classes that implement IReliableMessagingQuery are queries and they expect a reply, in this case the expected reply is in the type of MyQueryReply.
```csharp
public class MyQuery : IReliableMessagingQuery<MyQueryReply>
{
    public string Message { get; set; }

    public MyQuery(string message)
    {
        Message = message;
    }
}
```

> Classes that implement IReliableMessagingQueryReply are replies from a dispatched query.
```csharp
public class MyQueryReply : IReliableMessagingQueryReply
{
    public string Message { get; set; }

    public MyQueryReply(string message)
    {
        Message = message;
    }
}
```
