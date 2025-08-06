// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>A delegating image client that logs image operations to an <see cref="ILogger"/>.</summary>
/// <remarks>
/// <para>
/// The provided implementation of <see cref="IImageClient"/> is thread-safe for concurrent use so long as the
/// <see cref="ILogger"/> employed is also thread-safe for concurrent use.
/// </para>
/// <para>
/// When the employed <see cref="ILogger"/> enables <see cref="Logging.LogLevel.Trace"/>, the contents of
/// requests and options are logged. These requests and options may contain sensitive application data.
/// <see cref="Logging.LogLevel.Trace"/> is disabled by default and should never be enabled in a production environment.
/// Requests and options are not logged at other logging levels.
/// </para>
/// </remarks>
[Experimental("MEAI001")]
public partial class LoggingImageClient : DelegatingImageClient
{
    /// <summary>An <see cref="ILogger"/> instance used for all logging.</summary>
    private readonly ILogger _logger;

    /// <summary>The <see cref="JsonSerializerOptions"/> to use for serialization of state written to the logger.</summary>
    private JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>Initializes a new instance of the <see cref="LoggingImageClient"/> class.</summary>
    /// <param name="innerClient">The underlying <see cref="IImageClient"/>.</param>
    /// <param name="logger">An <see cref="ILogger"/> instance that will be used for all logging.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerClient"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    public LoggingImageClient(IImageClient innerClient, ILogger logger)
        : base(innerClient)
    {
        _logger = Throw.IfNull(logger);
        _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;
    }

    /// <summary>Gets or sets JSON serialization options to use when serializing logging data.</summary>
    /// <exception cref="ArgumentNullException">The value being set is <see langword="null"/>.</exception>
    public JsonSerializerOptions JsonSerializerOptions
    {
        get => _jsonSerializerOptions;
        set => _jsonSerializerOptions = Throw.IfNull(value);
    }

    /// <inheritdoc/>
    public override async Task<ImageResponse> GenerateImagesAsync(
        ImageRequest request, ImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogInvokedSensitive(nameof(GenerateImagesAsync), AsJson(request), AsJson(options), AsJson(this.GetService<ImageClientMetadata>()));
            }
            else
            {
                LogInvoked(nameof(GenerateImagesAsync));
            }
        }

        try
        {
            var response = await base.GenerateImagesAsync(request, options, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    LogCompletedSensitive(nameof(GenerateImagesAsync), AsJson(response));
                }
                else
                {
                    LogCompleted(nameof(GenerateImagesAsync));
                }
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            LogInvocationCanceled(nameof(GenerateImagesAsync));
            throw;
        }
        catch (Exception ex)
        {
            LogInvocationFailed(nameof(GenerateImagesAsync), ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ImageResponseUpdate> GenerateImagesStreamingAsync(
        ImageRequest request, 
        ImageOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogInvokedSensitive(nameof(GenerateImagesStreamingAsync), AsJson(request), AsJson(options), AsJson(this.GetService<ImageClientMetadata>()));
            }
            else
            {
                LogInvoked(nameof(GenerateImagesStreamingAsync));
            }
        }

        IAsyncEnumerable<ImageResponseUpdate> enumerable;
        try
        {
            enumerable = base.GenerateImagesStreamingAsync(request, options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInvocationFailed(nameof(GenerateImagesStreamingAsync), ex);
            throw;
        }

        await foreach (var update in enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogStreamingUpdateSensitive(nameof(GenerateImagesStreamingAsync), AsJson(update));
            }

            yield return update;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogCompleted(nameof(GenerateImagesStreamingAsync));
        }
    }

    private string AsJson<T>(T value) => LoggingHelpers.AsJson(value, _jsonSerializerOptions);

    [LoggerMessage(LogLevel.Debug, "{MethodName} invoked.")]
    private partial void LogInvoked(string methodName);

    [LoggerMessage(LogLevel.Trace, "{MethodName} invoked: Request: {ImageRequest}. Options: {ImageOptions}. Metadata: {ImageClientMetadata}.")]
    private partial void LogInvokedSensitive(string methodName, string imageRequest, string imageOptions, string imageClientMetadata);

    [LoggerMessage(LogLevel.Debug, "{MethodName} completed.")]
    private partial void LogCompleted(string methodName);

    [LoggerMessage(LogLevel.Trace, "{MethodName} completed: {ImageResponse}.")]
    private partial void LogCompletedSensitive(string methodName, string imageResponse);

    [LoggerMessage(LogLevel.Trace, "{MethodName} streaming update: {ImageResponseUpdate}.")]
    private partial void LogStreamingUpdateSensitive(string methodName, string imageResponseUpdate);

    [LoggerMessage(LogLevel.Debug, "{MethodName} canceled.")]
    private partial void LogInvocationCanceled(string methodName);

    [LoggerMessage(LogLevel.Error, "{MethodName} failed.")]
    private partial void LogInvocationFailed(string methodName, Exception error);
}
