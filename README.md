# NTDLS.ReliableMessaging

📦 Be sure to check out the NuGet pacakge: https://www.nuget.org/packages/NTDLS.ReliableMessaging

NTDLS.ReliableMessaging provides incredibly lightweight, reliable and high-performance TCP/IP based inter-process-communication functionality. This includes a server which listens for incoming connections and a client which makes a connection to the server.

Once connected the server and the client can send fire-and-forget style notifications or dispatch queries which require a reply.

All messages are guaranteed to be received in their entirety and in the order in which they were dispatched.


**Example of server and client sneding notifications and a query:**

```
//Class used to send a notification.
internal class MyNotification : IFrameNotification
{
    public string Message { get; set; }

    public MyNotification(string message)
    {
        Message = message;
    }
}

//Class used to send a query (which expects a response).
internal class MyQuery : IFrameQuery
{
    public string Message { get; set; }

    public MyQuery(string message)
    {
        Message = message;
    }
}

//Class used to reply to a query.
internal class MyQueryReply : IFrameQueryReply
{
    public string Message { get; set; }

    public MyQueryReply(string message)
    {
        Message = message;
    }
}

static void Main()
{
    //Start a server and add a "query received" and "notification received" event handler.
    var server = new MessageServer();
    server.OnQueryReceived += Server_OnQueryReceived;
    server.OnNotificationReceived += Server_OnNotificationReceived;
    server.Start(45784);

    //Start a client and connect to the server.
    var client = new MessageClient();
    client.Connect("localhost", 45784);

    client.Notify(new MyNotification("This is message 001 from the client."));
    client.Notify(new MyNotification("This is message 002 from the client."));
    client.Notify(new MyNotification("This is message 003 from the client."));

    //Send a query to the server, specify which type of reply we expect.
    client.Query<MyQueryReply>(new MyQuery("This is the query from the client.")).ContinueWith(x =>
    {
        //If we recevied a reply, print it to the console.
        if (x.IsCompletedSuccessfully && x.Result != null)
        {
            Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
        }
    });

    Console.WriteLine("Press [enter] to shutdown.");
    Console.ReadLine();

    //Cleanup.
    client.Disconnect();
    server.Stop();
}

private static void Server_OnNotificationReceived(MessageServer server, Guid connectionId, IFrameNotification payload)
{
    if (payload is MyNotification notification)
    {
        Console.WriteLine($"Server received notification: {notification.Message}");
    }
    else
    {
        throw new NotImplementedException();
    }
}

private static IFrameQueryReply Server_OnQueryReceived(MessageServer server, Guid connectionId, IFrameQuery payload)
{
    if (payload is MyQuery query)
    {
        Console.WriteLine($"Server received query: '{query.Message}'");

        //Return with a class that implements IFrameQueryReply to reply to the client.
        return new MyQueryReply("This is the query reply from the server.");
    }
    else
    {
        throw new NotImplementedException();
    }
}
```
