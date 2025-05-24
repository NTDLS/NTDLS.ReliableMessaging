# NTDLS.ReliableMessaging

📦 Be sure to check out the NuGet package: https://www.nuget.org/packages/NTDLS.ReliableMessaging

NTDLS.ReliableMessaging provides lightweight, simple, and high-performance TCP/IP based inter-process-communication / RPC functionality.

Once connected, the peers can send fire-and-forget style notifications or dispatch queries which require replies - all of which are handled either by events or convention.

## Testing Status
[![Regression Tests](https://github.com/NTDLS/NTDLS.ReliableMessaging/actions/workflows/Regression%20Tests.yml/badge.svg)](https://github.com/NTDLS/NTDLS.ReliableMessaging/actions/workflows/Regression%20Tests.yml)

## Use Cases
ReliableMessaging can be used to simply communicate between a backend service and a UI or service-to-service communication.

It has been successfully implemented as the communications backbone of instant-messaging services, file transfer applications, proxy services, tunneling services, message queuing servers, key-value servers and even as the communication protocol for a relational-database server.

## Asynchronous / Synchronous
By default, all queries and notifications are handled asynchronously, but that can be disabled via the configuration. The default configuration means that these messages can be received out of order. To ensure order you can use one of three methods:

1. Disable AsynchronousFrameProcessing, AsynchronousNotifications and/or AsynchronousQueryWaiting via the configuration that is passed to RmServer and/or RmClient.
2. Use Queries instead notifications. Queries require a reply from the server so allow the connected client to operation synchronously with the server – even when operating in asynchronous mode.
3. Use the built in RmSequenceBuffer to buffer out of order packets. This is used in conjunction with a custom notification class that communicated the “packet sequence”.

## Compression
Compress is added with a call to the client and server SetCompressionProvider() function with a reference to a compression provider. ReliableMessaging supples two built in compression providers: RmDeflateCompressionProvider and RmBrotliCompressionProvider(), but you can implement your own by inheriting from IRmCompressionProvider.

## Encryption
Encryption is added to the connection by a call to client and server SetCryptographyProvider() function with a reference to an encryption provider that inherits from IRmCryptographyProvider.
Server and the client can use a simple cryptography provider with a “hard coded” encryption key, meaning that they the server will expect each connecting client to encrypt the data with the same provider and key. However, once connected, the client and server can set the encryption provider for the connection. This allows the two peers to negotiate a key (such as Diffie-Hellman implementation or RSA) and use a custom provider to implement AES or some other encryption.

The server and client Query() function contains a special pre-flight delegate handler to allow you to initialize encryption after a query packet has been built but before it has been dispatched to the remote peer:
```csharp
client.Query(new ImGoingToInitializeEncryptionNowQuery(publicKey), ()=>
{
    //This is the pre-flight delegate where we would initialize the encryption provider for the client.
    client.SetCryptographyProvider(new MyCryptoProvider(publicKey));
});
```

## CRC (Cyclic redundancy check)
CRC is automatic and is applied and checked for each packet. If the CRC does not match, an exception is thrown and the packet is skipped. Since TCP/IP already implements CRC checks, this check is doubly redundant and is not ever expected to occur in real-world situations.

## Notification Message Sending and Receiving
Notifications are fire-and-forget messages that are communicated by calling the Notify() function on the server or client and passing a class that implements the IRmNotification interface.
The server and client can receive these messages in one of two ways:

### Events
Hooking the client or server OnNotificationReceived event
```csharp
OnNotificationReceived += (RmContext context, IRmNotification payload) =>
    {
        if(payload is MyNotification myNotification)
        {
        }
    }
```

### Convention
Creating a class that inherits from IRmMessageHandler and adding it to the client or server via a call to AddHandler(). The client and server can have as many handlers as you’d like, depending on how you want to separate your business logic.
The handler class would contain functions whose signatures match the signature of the notification types that you are sending. For example:
```csharp
class MessageHandlers : IRmMessageHandler
{
    public void SomeFunctionName(RmContext context, MyNotification notification)
    {
        Console.WriteLine($"Server received notification: {notification.Message}");
    }
}
```

In this case, when the client or server sends a MyNotification type, this handler will be called with the deserialized object. Also note that the notification message handlers also support generics, so if you have a class that use generics such as MyNotification<T> where T is another type, the signature just needs to match the same type that was sent via the call to Notify().

## Query Message Sending and Receiving
Queries messages that wait on a reply from the server and are communicated by calling the Query() function on the server or client and passing a class that implements the IRmQuery<IRmQueryReply> interface.
The server and client can receive these messages in one of two ways:

### Events
Hooking the client or server OnQueryReceived event
```csharp
OnQueryReceived += (RmContext context, IRmPayload payload) =>
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
```

### Convention
Creating a class that inherits from IRmMessageHandler and adding it to the client or server via a call to AddHandler(). The client and server can have as many handlers as you’d like, depending on how you want to separate your business logic.
The handler class would contain functions whose signatures match the signature of the query types that you are sending. For example:
```csharp
class MessageHandlers : IRmMessageHandler
{
    public MyQueryReply MyQueryReceived(RmContext context, MyQuery query)
    {
        Console.WriteLine($"Server received query: '{query.Message}'");

        return new MyQueryReply("This is query reply from the server.");
    }
}
```

In this case, when the client or server sends a MyQuery type, this handler will be called with the deserialized object. Unlike notifications, both the event or the convention based handlers should return a reply to the query where the type is denoted by the signature of the query type. (e.g. for IRmQuery<IRmQueryReply>, the reply should be of type IRmQueryReply).
Also note that the query message handlers also support generics, so if you have a class that use generics such as MyQuery <T> where T is another type, the signature just needs to match the same type that was sent via the call to Query().

## Performance
The throughput is regularly tested with each release of ReliableMessaging and notifications are suitable for multi-gigabit communication. Your miles will vary depending on whether you use compression, encryption, and the size of the messages that are being sent. Generally, larger messages have the highest throughput.

![image](https://github.com/user-attachments/assets/1a7c72f7-2f1b-4062-b8f4-8aa1c47dd0d8)

# Code Examples

## Server with convention based handler
Starts a server and adds a single message handler which is used to process messages that are sent by a client.

```csharp
var server = new RmServer();
server.AddHandler(new HandlerMethods());
server.Start(31254);

//server.Stop();
```

Handler class that is used to catch and process messages. Handlers like this can be added to the client and/or the server – and you can add as many of them as you want to separate business logic.

```csharp
internal class HandlerMethods : IRmMessageHandler
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

## Server with event based handler

Alternatively, you can use events to process messages. This is an example of a server using events instead of convention-based message handling. Note that you can add handlers to both the client and the server, but in these examples, we are only adding to the server for brevity.

Additionally, you can add a mix of event handlers and convention-based handlers. Messages are first matched to the convention handlers, and any unhandled messages are then routed to event hooks.

```csharp
var server = new RmServer();

server.OnNotificationReceived += Server_OnNotificationReceived;
server.OnQueryReceived += Server_OnQueryReceived;

// Handle the OnException event, otherwise the server will ignore any exceptions.
server.OnException += (context, ex, payload) =>
{
    Console.WriteLine($"Server exception: {ex.Message}");
};

server.Start(31254);

//server.Stop();

IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload query)
{
    if (query is MyQuery myQuery)
    {
        Console.WriteLine($"Server received query: '{myQuery.Message}'");
        return new MyQueryReply("This is the query reply from the server.");
    }
    throw new Exception("Query type was not handled.");
}

void Server_OnNotificationReceived(RmContext context, IRmNotification notification)
{
    if (notification is MyNotification myNotification)
    {
        Console.WriteLine($"Server received notification: {myNotification.Message}");
    }
    else
    {
        throw new Exception("Notification type was not handled.");
    }
}
```

## Client
An example clientthat connects to the server and sends a few notifications and a query.
```csharp
var client = new RmClient();

client.Connect("localhost", 31254);

client.Notify(new MyNotification("This is message 001 from the client."));
client.Notify(new MyNotification("This is message 002 from the client."));
client.Notify(new MyNotification("This is message 003 from the client."));

//Send a query to the server, wait on a reply.
client.Query(new MyQuery("This is the query from the client.")).ContinueWith(x =>
{
    //If we received a reply, print it to the console.
    if (x.IsCompletedSuccessfully && x.Result != null)
    {
        Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
    }
    else
    {
        Console.WriteLine($"Exception: '{x.Exception?.GetBaseException()?.Message}'");
    }
});

//client.Disconnect();
```

## Example supporting classes

Example message class that implements IRmNotification for fire-and-forget messages.

```csharp
public class MyNotification : IRmNotification
{
    public string Message { get; set; }

    public MyNotification(string message)
    {
        Message = message;
    }
}
```

Example message class that implements IRmQuery for query messages. These messages expect a reply of the given type, in this case the expected reply is in the type of MyQueryReply.
```csharp
public class MyQuery : IRmQuery<MyQueryReply>
{
    public string Message { get; set; }

    public MyQuery(string message)
    {
        Message = message;
    }
}
```

Classes that implement IRmQueryReply are replies in response to a query message.
```csharp
public class MyQueryReply : IRmQueryReply
{
    public string Message { get; set; }

    public MyQueryReply(string message)
    {
        Message = message;
    }
}
        
```

## License
[MIT](https://choosealicense.com/licenses/mit/)
