namespace GraphQL.AspNetCore3.WebSockets;

/// <summary>
/// Configuration options for a WebSocket connection.
/// </summary>
public class GraphQLWebSocketOptions
{
    /// <summary>
    /// The amount of time to wait for a GraphQL initialization packet before the connection is closed.
    /// The default is 10 seconds.
    /// </summary>
    public TimeSpan? ConnectionInitWaitTimeout { get; set; }

    /// <summary>
    /// The amount of time to wait between sending keep-alive packets.
    /// The default is disabled.
    /// <br/><br/>
    /// Keep in mind that the 'subscription-transport-ws' implementation typically
    /// disconnects clients if a keep-alive packet for more than 20 seconds, when
    /// keep-alive packets are enabled, so it is recommended to keep the keep-alive
    /// packets disabled or use a value less than 20 seconds.
    /// </summary>
    public TimeSpan? KeepAliveTimeout { get; set; }

    /// <summary>
    /// The amount of time to wait to attempt a graceful teardown of the WebSockets protocol.
    /// The default is 10 seconds.
    /// </summary>
    public TimeSpan? DisconnectionTimeout { get; set; }

    /// <summary>
    /// Disconnects a subscription from the client if the subscription source dispatches an
    /// <see cref="IObserver{T}.OnError(Exception)"/> event.
    /// </summary>
    public bool DisconnectAfterErrorEvent { get; set; } = true;

    /// <summary>
    /// Disconnects a subscription from the client there are any GraphQL errors during a subscription.
    /// </summary>
    public bool DisconnectAfterAnyError { get; set; }

    /// <summary>
    /// To help prevent backpressure from slower internet speeds, this will prevent the queue from expanding
    /// beyond the max length.
    /// The default is null (no limit). Value must be greater than 0.
    /// </summary>
    public int? MaxSendQueueThreshold { get; set; }
}
