# NTDLS.ReliableMessaging — In-Depth Code Review

**Project:** NTDLS.ReliableMessaging v3.4.5  
**Scope:** Full source code review (24 files, ~3000 lines)  
**Date:** 2026-01-13  

---

## Executive Summary

This is a TCP-based RPC library with framing, optional compression/encryption, and convention-based message routing. The architecture is sound but has **3 critical bugs**, **7 major issues**, and **15+ minor items**. The most severe findings are a race condition in client reconnection that can corrupt state, a query termination bug that silently swallows exceptions on disconnect, and a crash in `RmSequenceBuffer` on duplicate sequences.

---

## 🔴 CRITICAL BUGS

### #1 — Race Condition in Client Reconnection Can Cause Double-Connection or State Corruption

**File:** `RmClient.cs`, lines 528–560 (`ReconnectThreadProc`) and lines 229–255 (`Connect`)

**The problem:** There is no synchronization between the reconnect background thread and external callers of `Connect()` / `Disconnect()`. Multiple threads can simultaneously mutate `_tcpClient`, `_activeConnection`, `_reconnectHost`, `_reconnectIpAddress`, and `_reconnectPort`.

**Concrete scenario:**
1. Connection drops → `InvokeOnDisconnected` fires → sets `_isReconnecting = true` → starts `ReconnectThreadProc`
2. Meanwhile, user calls `client.Connect("host", 1234)` from their own thread
3. Both threads execute `Connect()` concurrently:
   - Both check `IsConnected` (which reads `_tcpClient?.Connected` — already null/false since disconnected)
   - Both create new `TcpClient` instances
   - Both assign to `_tcpClient` and `_activeConnection`
   - Both call `_activeConnection.RunAsync()` starting two data pump threads on the same logical connection
4. Result: Two threads reading from the same stream, interleaved frame parsing, corrupted state

**Also:** Inside `ReconnectThreadProc`, after a successful reconnect, the method returns — but `_isReconnecting` is only reset to `false` in the `finally` block. If the reconnect thread exits normally (success), the next disconnection will correctly start a new reconnect attempt. However, if during the reconnect loop `_explicitlyDisconnected` becomes `true` while another thread is also calling `Disconnect()`, there's a window where both set `_explicitlyDisconnected = true` and both call `_activeConnection?.Disconnect(true)` — which closes the stream twice. This isn't catastrophic (the second close is a no-op if already closed), but the lack of any lock means the entire reconnect dance is racy.

**Recommended fix:** Add a dedicated lock object for all connect/disconnect/reconnect operations:

```csharp
private readonly object _connectionLock = new();

public void Connect(string hostName, int port)
{
    lock (_connectionLock)
    {
        if (IsConnected) throw new Exception("Client is already connected.");
        // ... rest of Connect logic
    }
}

private void ReconnectThreadProc()
{
    try
    {
        while (!_explicitlyDisconnected)
        {
            Thread.Sleep(Configuration.ReconnectDelay);
            if (_explicitlyDisconnected) break;

            lock (_connectionLock)
            {
                if (_explicitlyDisconnected || IsConnected) break;
                // ... reconnect logic
            }
        }
    }
    finally { _isReconnecting = false; }
}
```

---

### #2 — `TerminateWaitingQueries` Sets Exceptions But Never Removes Entries — Memory Leak + Silent Swallowing

**File:** `Internal/StreamFraming/RmFraming.cs`, line 31–36

```csharp
internal static void TerminateWaitingQueries(RmContext context, Guid connectionId)
{
    foreach (var kvp in context.QueriesAwaitingReplies.ToArray())
    {
        if (kvp.Value.ConnectionId == connectionId)
        {
            kvp.Value.Tcs.TrySetException(new Exception("The connection was terminated."));
        }
    }
}
```

**The problem:** This iterates over a snapshot (`.ToArray()`) and sets exceptions on matching TCS objects, but **never removes the entries from `QueriesAwaitingReplies`**. 

**Consequences:**
1. **Memory leak:** Every time a connection drops, all pending queries remain in the dictionary forever. With repeated connect/disconnect cycles (or auto-reconnect scenarios), this grows unbounded.
2. **Silent exception swallowing:** When `WriteQueryFrame` catches an exception from the TCS task, it tries `TryRemove(frameBody.Id, out _)` in its `finally` block — but the entry is still there because `TerminateWaitingQueries` didn't remove it. The `TryRemove` succeeds, so the cleanup *eventually* happens... but only if the caller's `finally` runs. If the exception propagates before the `finally`, the entry stays forever.
3. **Double-set risk:** If `TerminateWaitingQueries` sets an exception on the TCS, and then the reply actually arrives (race between disconnect and network delivery), `ProcessFrame` calls `waitingQuery.Tcs.SetResult(reply)`. Since `TrySetException` already ran, `SetResult` will fail silently (TCS can only be completed once). The reply is lost.

**Recommended fix:** Remove entries as you terminate them:

```csharp
internal static void TerminateWaitingQueries(RmContext context, Guid connectionId)
{
    var ex = new Exception("The connection was terminated.");
    foreach (var kvp in context.QueriesAwaitingReplies.ToArray())
    {
        if (kvp.Value.ConnectionId == connectionId)
        {
            if (context.QueriesAwaitingReplies.TryRemove(kvp.Key, out var waiting))
            {
                waiting.Tcs.TrySetException(ex);
            }
        }
    }
}
```

---

### #3 — `RmSequenceBuffer.Process()` Crashes on Duplicate Sequences

**File:** `RmSequenceBuffer.cs`, lines 62–63 and 91–92

```csharp
//We received out-of-order packets. Store them in the buffer.
_buffer.Add(sequence, data);
```

**The problem:** `Dictionary.Add()` throws `ArgumentException` if the key already exists. If a packet with the same sequence number arrives twice (e.g., due to TCP-level retransmission at the application layer, or a bug in the sender), the entire processing thread crashes.

**Concrete scenario:** If the sender accidentally sends the same sequence number twice (or if a retry mechanism resends a packet that was actually received), `Process()` throws, the exception propagates up through the data pump thread, and the connection dies.

**Recommended fix:** Use `TryAdd` or overwrite existing entries:

```csharp
if (!_buffer.TryAdd(sequence, data))
{
    // Duplicate sequence — either ignore or log a warning
    return; 
}
```

Or use `_buffer[sequence] = data;` if overwriting is acceptable.

---

## 🟠 MAJOR ISSUES

### #4 — `lock(this)` Anti-Pattern in `RmFrameBuffer`

**File:** `Internal/StreamFraming/RmFrameBuffer.cs`, lines 80, 111, 163, 214

All four lock sites use `lock(this)`. While C# locks are reentrant (so the nested calls don't deadlock), `lock(this)` is an anti-pattern because:
- External code could obtain a reference to the `RmFrameBuffer` instance and lock it, causing unexpected deadlocks
- It exposes internal synchronization strategy as part of the public API surface

**Fix:** Use a private readonly lock object: `private readonly object _lock = new();`

### #5 — Provider Fields in `RmContext` Have No Thread Safety

**File:** `RmContext.cs`, lines 22–24

```csharp
private IRmSerializationProvider? _serializationProvider = null;
private IRmCompressionProvider? _compressionProvider = null;
private IRmCryptographyProvider? _cryptographyProvider = null;
```

`SetSerializationProvider`, `SetCompressionProvider`, and `SetCryptographyProvider` perform plain field assignments with no synchronization. Meanwhile, the data pump thread reads these fields concurrently via `Get*Provider()` during frame processing.

In practice, `IRmMessenger.SetSerializationProvider()` calls both `Configuration.SerializationProvider = provider` AND `_activeConnection?.Context.SetSerializationProvider(provider)` — two non-atomic operations across two objects. A frame being processed mid-transition could see a null or partially-updated provider.

**Fix:** Either make the fields `volatile` (simplest, since they're just references) or add proper locking around get/set pairs.

### #6 — `RmServer.Stop()` Can Hang Forever

**File:** `RmServer.cs`, lines 183–193

```csharp
public void Stop()
{
    _keepRunning = false;
    Exceptions.Ignore(() => _listener?.Stop());
    _listenerThreadProc?.Join();       // ← No timeout
    _activeConnections.Use((o) =>
    {
        o.ForEach(c => c.Disconnect(true));  // ← Disconnect(true) calls Thread.Join() with no timeout
        o.Clear();
    });
}
```

If a peer connection's data pump thread is blocked (e.g., stuck on `stream.Read()` that never returns, or deadlocked on `StreamWriteLock`), `Disconnect(true)` → `Thread.Join()` blocks indefinitely. `Stop()` then hangs forever.

**Fix:** Use `Thread.Join(timeout)` with a reasonable timeout (e.g., 5 seconds) and log a warning if the thread doesn't exit.

### #7 — Fire-and-Forget Tasks Swallow Unhandled Exceptions

**File:** `Internal/StreamFraming/RmFraming.cs`, lines 106, 393, 425

```csharp
Task.Run(() => stream.ProcessFrame(...));           // line 106
Task.Run(() => { processNotificationCallback(...); });  // lines 393, 425
```

Three `Task.Run()` calls are fire-and-forget. If an unhandled exception occurs inside these tasks (i.e., one that bypasses the inner try/catch), it becomes an unobserved task exception. In .NET, unobserved exceptions are logged but not propagated — meaning the error is silently swallowed from the perspective of the caller.

**Fix:** At minimum, attach a continuation: `.ContinueWith(t => /* log t.Exception */, TaskContinuationOptions.OnlyOnFaulted)`. Or better, wrap the body in a try/catch that logs.

### #8 — AES Key Stored by Reference — Caller Can Mutate It

**File:** `RmAesCryptographyProvider.cs`, line 28

```csharp
_aesKey = aesKey;
```

The constructor stores the caller's byte array directly without cloning. If the caller mutates the array after construction, subsequent encrypt/decrypt operations use the mutated key, producing garbage output or decryption failures.

**Fix:** Clone the key: `_aesKey = aesKey.ToArray();` or `Array.Copy(aesKey, _aesKey = new byte[aesKey.Length], aesKey.Length);`

### #9 — `RmQueryReplyException` Loses Stack Trace Entirely

**File:** `RmQueryReplyException.cs`, lines 35–44

```csharp
public RmQueryReplyException(Exception ex)
{
    Message = ex.Message;
    Source = ex.Source;
}

public Exception GetException()
{
    return new Exception(Message) { Source = Source };
}
```

The original exception's stack trace, inner exception, and HResult are all discarded. Only `Message` and `Source` survive the round-trip. On the receiving end, `GetException()` creates a brand-new `Exception` with a stack trace pointing to the *creation site*, not the original error location. Debugging remote errors becomes extremely difficult.

**Fix:** Serialize the full stack trace: store `ex.StackTrace`, `ex.InnerException?.ToString()`, and reconstruct with those fields. Consider using `Exception.ToJson()` / `FromJson()` or storing the full `ex.ToString()` output.

### #10 — `RmReflectionCache` Dictionaries Are Not Thread-Safe

**File:** `Internal/RmReflectionCache.cs`, lines 39–40

```csharp
private readonly Dictionary<string, CachedMethod> _handlerMethods = new();
private readonly Dictionary<Type, IRmMessageHandler> _handlerInstances = new();
```

These regular `Dictionary` instances are written to during `AddInstance()` (called during setup) and read from during `RouteToQueryHander()` / `RouteToNotificationHander()` (called from the data pump thread). If `AddInstance()` is called after the connection is established (which is possible — nothing prevents it), concurrent modification of a non-thread-safe dictionary causes undefined behavior (corruption, infinite loops, or `InvalidOperationException`).

Additionally, `GetOrCreatedCachedInstance()` can race with itself: two threads simultaneously finding a missing instance both call `Activator.CreateInstance` and both call `_handlerInstances.Add()`, with the second throwing `ArgumentException`.

**Fix:** Use `ConcurrentDictionary` or protect with a lock. For `GetOrCreatedCachedInstance`, use `ConcurrentDictionary.GetOrAdd()`.

---

## 🟡 MINOR / OPTIMIZATION ITEMS

### #11 — Hot-Path Allocation in `GetNextFrame`: Three Small Arrays Per Frame

**File:** `RmFrameBuffer.cs`, lines 119–121

```csharp
var frameDelimiterBytes = new byte[4];
var frameSizeBytes = new byte[4];
var expectedCRC16Bytes = new byte[2];
```

Every single frame parsed allocates three small arrays. Under high-throughput scenarios, this generates significant GC pressure. These could be stack-allocated via `stackalloc` or reused from a pool.

### #12 — `DeserializeToObject<T>` Wastes a Write Then Seek

**File:** `Internal/RmSerialization.cs`, lines 18–22

```csharp
using var stream = new MemoryStream();
stream.Write(arrBytes, 0, arrBytes.Length);
stream.Seek(0, SeekOrigin.Begin);
return Serializer.Deserialize<T>(stream);
```

protobuf-net's `Serializer.Deserialize<T>()` accepts a `byte[]` directly. No need to wrap in a `MemoryStream`, write, and seek. Just call `Serializer.Deserialize<T>(new MemoryStream(arrBytes, false))` or better yet, use the overload that takes a span/array directly if available.

### #13 — Missing `JsonSerializerOptions` Reuse in Default Serialization Provider

**File:** `RmJsonSerializationProvider.cs`

Every call to `JsonSerializer.Serialize()` and `JsonSerializer.Deserialize<T>()` uses default options, creating a new options object internally each time. For a hot-path serialization provider, a shared static `JsonSerializerOptions` instance should be reused.

### #14 — Stale `TcpClient.Connected` Check Before Writing

**File:** `Internal/StreamFraming/RmFraming.cs`, lines 55 and 73

```csharp
if (context.TcpClient.Connected)
{
    await stream.WriteAsync(buffer, cancellationToken);
}
```

`TcpClient.Connected` is a point-in-time snapshot that can become stale between the check and the actual write. If the peer disconnects in that window, the write silently does nothing (no bytes sent, no exception thrown). Data is dropped with no indication.

**Fix:** Remove the guard and let `WriteAsync` throw if the connection is dead, or check `CanWrite` instead (though that's also subject to TOCTOU).

### #15 — `RmContext.StreamWriteLock` SemaphoreSlim Is Never Disposed

**File:** `RmContext.cs`, line 28

```csharp
internal SemaphoreSlim StreamWriteLock { get; private set; } = new(1, 1);
```

`SemaphoreSlim` holds native resources. It's created per-context but never disposed. Each connection leaks a semaphore handle. Over many connections (especially with reconnect), this accumulates.

**Fix:** Implement `IDisposable` on `RmContext` or dispose the semaphore when the connection is torn down.

### #16 — Documentation Mismatch: Property Named "AsynchronousFrameProcessing" but Comment Says "MultiThreadedFrameProcessing"

**File:** `RmConfiguration.cs`, lines 13–17

The comments for `AsynchronousNotifications` and `AsynchronousQueryWaiting` reference "MultiThreadedFrameProcessing" which doesn't exist — the actual property is `AsynchronousFrameProcessing`. This is confusing for anyone reading the config.

### #17 — No Exponential Backoff for Reconnect Attempts

**File:** `RmClient.cs`, line 534

```csharp
Thread.Sleep(Configuration.ReconnectDelay);
```

Reconnect always uses a fixed delay. If the server is down for maintenance, this hammers the reconnect attempt every N seconds. Standard practice is exponential backoff with jitter.

### #18 — Duplicated `Connect` Logic Across Four Overloads

**File:** `RmClient.cs`, lines 229–312

Four `Connect()` overloads contain nearly identical logic (set reconnect fields, create TcpClient, create RmPeerConnection, call RunAsync). This violates DRY and makes future changes error-prone (a fix in one overload might be missed in another).

**Fix:** Extract a private `ConnectCore(TcpClient tcpClient)` method.

### #19 — Sync `Query<T>` Creates Redundant Async State Machine

**File:** `Internal/StreamFraming/RmFraming.cs`, sync `WriteQueryFrame` (line ~227)

The synchronous version calls `.WaitAsync(queryTimeout, cancellationToken).GetAwaiter().GetResult()`. This wraps the operation in an async state machine just to block on it synchronously. A simpler approach would be to use a `CancellationTokenSource` with timeout and wait on the TCS directly:

```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(queryTimeout);
replyPayload = queryAwaitingReply.Tcs.Task.Wait(cts.Token) 
    ? queryAwaitingReply.Tcs.Task.Result 
    : throw new Exception("Query timeout expired...");
```

### #20 — `SemaphoreSlim.Wait()` in `SafeWrite` Doesn't Respect Timeout Properly

**File:** `Internal/StreamFraming/RmFraming.cs`, line 70

```csharp
context.StreamWriteLock.Wait(cancellationToken);
```

If the cancellation token fires while waiting for the semaphore, `SemaphoreSlim.Wait(CancellationToken)` throws `OperationCanceledException`. But this exception isn't caught or handled — it propagates up through `WriteQueryFrame` / `WriteNotificationFrame` and gets wrapped in a generic exception. More importantly, if the semaphore is held by a thread that's stuck (e.g., blocking write to a dead socket), the caller waits indefinitely until cancelled.

### #21 — `GetNextFrame` Has a Subtle CRC Validation Bug

**File:** `RmFrameBuffer.cs`, lines 139–143

```csharp
if (RmCRC16.ComputeChecksum(FrameBuilder, RmConstants.GrossFrameHeaderSize, grossFrameSize - RmConstants.GrossFrameHeaderSize) != expectedCRC16)
{
    SkipFrame(context, onException);
    throw new Exception("Frame was corrupted (size discrepancy).");
}
```

The error message says "size discrepancy" but the actual check is a CRC mismatch. The message is misleading — a CRC failure could be caused by data corruption, not size issues. The message should say "CRC mismatch" or "frame integrity check failed."

### #22 — `SkipFrame` Linear Search for Next Delimiter Is O(n) and Fragile

**File:** `RmFrameBuffer.cs`, lines 163–187

When a frame is corrupted, `SkipFrame` scans byte-by-byte looking for the next valid delimiter. This is O(n) in the worst case (scanning the entire buffer). More concerning: if the corrupted data happens to contain a sequence of bytes that matches the delimiter value, `SkipFrame` will treat it as the start of a new frame — potentially desynchronizing the entire stream. A more robust approach would be to clear the buffer entirely or use a more sophisticated resync mechanism.

### #23 — `ReadAndProcessFrames` Returns `bool` But Inner Loop Ignores It

**File:** `Internal/RmPeerConnection.cs`, lines 51–56

```csharp
while (Context.Stream.ReadAndProcessFrames(...))
{
    //The famous do nothing loop!
}
```

The outer `while` loop continues as long as `ReadAndProcessFrames` returns `true`. When it returns `false` (stream closed), the inner loop exits and `Disconnect(false)` is called. This is correct but fragile — the "do nothing loop" comment acknowledges it's busy-waiting. A `Thread.Sleep(0)` or yield could reduce CPU usage, though `stream.Read()` inside `ReadStream` is blocking so this shouldn't spin.

### #24 — `_keepRunning` in `RmPeerConnection` Has No Memory Barrier

**File:** `Internal/RmPeerConnection.cs`, line 12

```csharp
private bool _keepRunning;
```

This field is written by the main thread (in `Disconnect`) and read by the data pump thread (in `DataPumpThreadProc`). Without `volatile` or a memory barrier, the JIT/compiler could cache the value in a register, causing the data pump thread to never see the update and run forever.

In practice, the `Stream.Close()` call in `Disconnect` usually forces the issue (by causing `stream.Read()` to throw), but relying on side effects for thread visibility is not guaranteed.

**Fix:** Make it `volatile` or use `Volatile.Read`/`Volatile.Write`.

### #25 — `_keepRunning` in `RmServer` Also Lacks Volatility

**File:** `RmServer.cs`, line 17

Same issue as #24. The listener thread reads `_keepRunning` in its while-loop, and `Stop()` writes it. Should be `volatile`.

### #26 — `RmCaching._slidingOneMinute` Shared Mutable State

**File:** `Internal/RmCaching.cs`, line 8

```csharp
internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };
```

This `MemoryCacheEntryOptions` instance is shared across all `SetOneMinute` and `GetOrCreateOneMinute` calls. `MemoryCacheEntryOptions` may maintain internal state (recording callbacks, expiration timestamps) that mutates when used. Sharing a single instance across multiple cache entries is unsafe.

**Fix:** Create a new `MemoryCacheEntryOptions` instance per call, or clone before use.

### #27 — `RmReflectionCache.GetOrCreatedCachedInstance` Can Throw on Concurrent Access

**File:** `Internal/RmReflectionCache.cs`, lines 157–169

If two threads simultaneously find a handler type not in `_handlerInstances`, both create a new instance and both call `_handlerInstances.Add()`. The second `Add` throws `ArgumentException` (duplicate key). This is a variant of issue #10.

### #28 — `ProcessFrame` Query Reply Routing Throws on Missing Waiting Query

**File:** `Internal/StreamFraming/RmFraming.cs`, lines 370–374

```csharp
if (!context.QueriesAwaitingReplies.TryGetValue(frameBody.Id, out var waitingQuery))
{
    throw new Exception($"No waiting query was found for the reply with id '{frameBody.Id}'. Possible query timeout.");
}
```

When a query times out on the sender side, the entry is removed from `QueriesAwaitingReplies` (via `TryRemove` in the `finally` block). If the reply then arrives late (network delay), this code throws. The exception is caught by the outer try/catch in `ProcessFrame` and routed to `onException`, but the reply is permanently lost.

This isn't necessarily a bug (late replies after timeout are inherently problematic), but the error handling could be more graceful — e.g., log a warning instead of treating it as an exception event.

### #29 — `RmFrameBuffer.FrameBuilder` Never Shrinks

**File:** `RmFrameBuffer.cs`, line 94

```csharp
Array.Resize(ref FrameBuilder, FrameBuilderLength + ReceiveBufferUsed);
```

Once `FrameBuilder` grows to accommodate a large message, it never shrinks back. For connections that occasionally receive large messages, the buffer stays large indefinitely, wasting memory.

### #30 — `SemaphoreSlim` Is Non-Reentrant — Same-Thread Recursion Risk

**File:** `RmContext.cs`, line 28

```csharp
internal SemaphoreSlim StreamWriteLock { get; private set; } = new(1, 1);
```

If a query handler callback (running on the data pump thread) attempts to send a reply or notification while holding the `StreamWriteLock` (which happens in `ProcessFrame` → `WriteReplyFrame`), and the data pump thread somehow already holds the lock (from a previous write), the thread blocks on itself. `SemaphoreSlim` is not reentrant.

In the current code flow, this doesn't happen because `ProcessFrame` is called from the read path (not the write path). But if someone adds a feature where the read path acquires the write lock and then calls back into code that also needs the write lock, it deadlocks. Worth documenting or switching to a `Mutex` or `Monitor` for reentrancy safety.

---

## Summary Table

| # | Severity | Category | File(s) | Impact |
|---|----------|----------|---------|--------|
| 1 | 🔴 Critical | Concurrency | RmClient.cs | Double-connection, state corruption |
| 2 | 🔴 Critical | Memory leak | RmFraming.cs | Unbounded dict growth, silent swallow |
| 3 | 🔴 Critical | Crash | RmSequenceBuffer.cs | ArgumentException on duplicates |
| 4 | 🟠 Major | Code quality | RmFrameBuffer.cs | lock(this) anti-pattern |
| 5 | 🟠 Major | Thread safety | RmContext.cs | Null ref / partial update race |
| 6 | 🟠 Major | Hang | RmServer.cs | Stop() blocks forever |
| 7 | 🟠 Major | Error handling | RmFraming.cs | Swallowed exceptions |
| 8 | 🟠 Major | Security | RmAesCryptographyProvider.cs | Key mutation vulnerability |
| 9 | 🟠 Major | Diagnostics | RmQueryReplyException.cs | Lost stack traces |
| 10 | 🟠 Major | Thread safety | RmReflectionCache.cs | Dict corruption under concurrency |
| 11 | 🟡 Minor | Performance | RmFrameBuffer.cs | GC pressure per frame |
| 12 | 🟡 Minor | Performance | RmSerialization.cs | Unnecessary MemoryStream |
| 13 | 🟡 Minor | Performance | RmJsonSerializationProvider.cs | Options allocation per call |
| 14 | 🟡 Minor | Reliability | RmFraming.cs | Silent data drop |
| 15 | 🟡 Minor | Resource leak | RmContext.cs | Undisposed SemaphoreSlim |
| 16 | 🟡 Minor | Documentation | RmConfiguration.cs | Wrong property name in comments |
| 17 | 🟡 Minor | Robustness | RmClient.cs | No exponential backoff |
| 18 | 🟡 Minor | Maintainability | RmClient.cs | Duplicated Connect logic |
| 19 | 🟡 Minor | Performance | RmFraming.cs | Unnecessary async state machine |
| 20 | 🟡 Minor | Error handling | RmFraming.cs | Poor cancellation handling |
| 21 | 🟡 Minor | Clarity | RmFrameBuffer.cs | Misleading error message |
| 22 | 🟡 Minor | Robustness | RmFrameBuffer.cs | Fragile frame resync |
| 23 | 🟡 Minor | Style | RmPeerConnection.cs | Busy-wait pattern |
| 24 | 🟡 Minor | Thread safety | RmPeerConnection.cs | Missing volatile |
| 25 | 🟡 Minor | Thread safety | RmServer.cs | Missing volatile |
| 26 | 🟡 Minor | Thread safety | RmCaching.cs | Shared mutable options |
| 27 | 🟡 Minor | Thread safety | RmReflectionCache.cs | Concurrent Add collision |
| 28 | 🟡 Minor | Error handling | RmFraming.cs | Harsh handling of late replies |
| 29 | 🟡 Minor | Memory | RmFrameBuffer.cs | Buffer never shrinks |
| 30 | 🟡 Minor | Robustness | RmContext.cs | Non-reentrant semaphore risk |