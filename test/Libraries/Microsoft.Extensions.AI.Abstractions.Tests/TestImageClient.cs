// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.AI;

public sealed class TestImageClient : IImageClient
{
    public TestImageClient()
    {
        GetServiceCallback = DefaultGetServiceCallback;
    }

    public IServiceProvider? Services { get; set; }

    public Func<ImageRequest, ImageOptions?, CancellationToken, Task<ImageResponse>>? GenerateImagesAsyncCallback { get; set; }

    public Func<ImageRequest, ImageOptions?, CancellationToken, IAsyncEnumerable<ImageResponseUpdate>>? GenerateImagesStreamingAsyncCallback { get; set; }

    public Func<Type, object?, object?> GetServiceCallback { get; set; }

    public bool DisposeInvoked { get; private set; }

    private object? DefaultGetServiceCallback(Type serviceType, object? serviceKey)
        => serviceType is not null && serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public Task<ImageResponse> GenerateImagesAsync(ImageRequest request, ImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        return GenerateImagesAsyncCallback?.Invoke(request, options, cancellationToken) ??
            Task.FromResult(new ImageResponse());
    }

    public async IAsyncEnumerable<ImageResponseUpdate> GenerateImagesStreamingAsync(
        ImageRequest request, 
        ImageOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (GenerateImagesStreamingAsyncCallback != null)
        {
            await foreach (var update in GenerateImagesStreamingAsyncCallback(request, options, cancellationToken))
            {
                yield return update;
            }
        }
        else
        {
            // Default implementation: return the full response as a single update
            var response = await GenerateImagesAsync(request, options, cancellationToken);
            yield return new ImageResponseUpdate(response.Contents);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return GetServiceCallback.Invoke(serviceType, serviceKey);
    }

    public void Dispose()
    {
        DisposeInvoked = true;
    }
}
