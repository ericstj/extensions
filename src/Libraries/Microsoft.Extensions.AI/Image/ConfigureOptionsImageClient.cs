// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>Represents a delegating image client that configures an <see cref="ImageOptions"/> instance used by the remainder of the pipeline.</summary>
[Experimental("MEAI001")]
public sealed class ConfigureOptionsImageClient : DelegatingImageClient
{
    /// <summary>The callback delegate used to configure options.</summary>
    private readonly Action<ImageOptions> _configureOptions;

    /// <summary>Initializes a new instance of the <see cref="ConfigureOptionsImageClient"/> class with the specified <paramref name="configure"/> callback.</summary>
    /// <param name="innerClient">The inner client.</param>
    /// <param name="configure">
    /// The delegate to invoke to configure the <see cref="ImageOptions"/> instance. It is passed a clone of the caller-supplied <see cref="ImageOptions"/> instance
    /// (or a newly constructed instance if the caller-supplied instance is <see langword="null"/>).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="innerClient"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The <paramref name="configure"/> delegate is passed either a new instance of <see cref="ImageOptions"/> if
    /// the caller didn't supply an <see cref="ImageOptions"/> instance, or a clone (via <see cref="ImageOptions.Clone"/> of the caller-supplied
    /// instance if one was supplied.
    /// </remarks>
    public ConfigureOptionsImageClient(IImageClient innerClient, Action<ImageOptions> configure)
        : base(innerClient)
    {
        _configureOptions = Throw.IfNull(configure);
    }

    /// <inheritdoc/>
    public override async Task<ImageResponse> GenerateImagesAsync(
        ImageRequest request, ImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await base.GenerateImagesAsync(request, Configure(options), cancellationToken);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ImageResponseUpdate> GenerateImagesStreamingAsync(
        ImageRequest request, 
        ImageOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GenerateImagesStreamingAsync(request, Configure(options), cancellationToken))
        {
            yield return update;
        }
    }

    /// <summary>Creates and configures the <see cref="ImageOptions"/> to pass along to the inner client.</summary>
    private ImageOptions Configure(ImageOptions? options)
    {
        options = options?.Clone() ?? new();

        _configureOptions(options);

        return options;
    }
}
