## Capability: onnx-concurrency

Cap parallel ONNX inference in `MultilingualE5Embedder` and `CrossEncoderReranker` via `SemaphoreSlim`.

### Changes

**`MedAssist.AI/Embedding/MultilingualE5Embedder.cs`**

Add a private field:
```csharp
private readonly SemaphoreSlim _inferenceGate = new(Environment.ProcessorCount, Environment.ProcessorCount);
```

Wrap the `session.Run(...)` call inside the public embed method:
```csharp
await _inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
try
{
    // existing session.Run(...) call
}
finally
{
    _inferenceGate.Release();
}
```

If the current method is synchronous (returns `float[]` directly), convert it to `async Task<float[]>` and `await _inferenceGate.WaitAsync(ct)`.

**`MedAssist.AI/Reranker/CrossEncoderReranker.cs`**

Same pattern:
```csharp
private readonly SemaphoreSlim _inferenceGate = new(Environment.ProcessorCount, Environment.ProcessorCount);
```

Wrap `session.Run(...)`:
```csharp
await _inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
try
{
    // existing scoring loop
}
finally
{
    _inferenceGate.Release();
}
```

### Behaviour

- At most `Environment.ProcessorCount` ONNX inference calls execute simultaneously.
- Additional concurrent requests queue on the semaphore and are served FIFO as slots free up.
- Semaphore is disposed with the service (both classes are singletons whose lifetimes match the host).
