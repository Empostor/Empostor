using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Serilog;

namespace Next.Hazel.Udp;

/// <summary>
///     Listens for new UDP connections and creates UdpConnections for them.
/// </summary>
/// <inheritdoc />
public class UdpConnectionListener : NetworkConnectionListener
{
    public delegate bool AcceptConnectionCheck(IPEndPoint endPoint, byte[] input, out byte[] response);

    private static readonly ILogger Logger = Log.ForContext<UdpConnectionListener>();
    private readonly ConcurrentDictionary<EndPoint, UdpServerConnection> _allConnections;
    private readonly UdpConnectionRateLimit _connectionRateLimit;
    protected readonly ObjectPool<MessageReader> _readerPool;
    private readonly Timer _reliablePacketTimer;

    private readonly UdpClient _socket;
    private readonly CancellationTokenSource _stoppingCts;
    private Task _executingTask;

    /// <summary>
    ///     A callback for early connection rejection.
    ///     * Return false to reject connection.
    ///     * A null response is ok, we just won't send anything.
    /// </summary>
    public AcceptConnectionCheck AcceptConnection;

    #nullable enable
    /// <summary>
    ///     Creates a new UdpConnectionListener for the given <see cref="IPAddress" />, port and <see cref="IPMode" />.
    /// </summary>
    /// <param name="endPoint">The endpoint to listen on.</param>
    /// <param name="readerPool"></param>
    /// <param name="ipMode"></param>
    /// <param name="Setup"></param>
    public UdpConnectionListener(IPEndPoint endPoint, ObjectPool<MessageReader> readerPool, IPMode ipMode = IPMode.IPv4, Action<UdpClient, UdpConnectionRateLimit>? Setup = null)
    {
        EndPoint = endPoint;
        IPMode = ipMode;

        _readerPool = readerPool;
        _socket = new UdpClient(endPoint);

        try
        {
            _socket.DontFragment = false;
        }
        catch (SocketException)
        {
        }

        _reliablePacketTimer = new Timer(ManageReliablePackets, null, 100, Timeout.Infinite);

        _allConnections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();

        _stoppingCts = new CancellationTokenSource();
        _stoppingCts.Token.Register(() => { _socket.Dispose(); });

        _connectionRateLimit = new UdpConnectionRateLimit();
        Setup?.Invoke(_socket, _connectionRateLimit);
    }

    private async void ManageReliablePackets(object? state)
    {
        foreach (var kvp in _allConnections)
        {
            var sock = kvp.Value;
            await sock.ManageReliablePackets();
        }

        try
        {
            _reliablePacketTimer.Change(100, Timeout.Infinite);
        }
        catch
        {
            // ignored
        }
    }

    /// <inheritdoc />
    public override Task StartAsync()
    {
        // Store the task we're executing
        _executingTask = Task.Factory.StartNew(ListenAsync, TaskCreationOptions.LongRunning);

        // If the task is completed then return it, this will bubble cancellation and failure to the caller
        return _executingTask.IsCompleted ? _executingTask :
            // Otherwise it's running
            Task.CompletedTask;
    }

    private async Task StopAsync()
    {
        // Stop called without start
        if (_executingTask == null) return;

        try
        {
            // Signal cancellation to the executing method
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            // Wait until the task completes or the timeout triggers
            await Task.WhenAny(_executingTask, Task.Delay(TimeSpan.FromSeconds(5)));
        }
    }

    /// <summary>
    ///     Instructs the listener to begin listening.
    /// </summary>
    private async Task ListenAsync()
    {
        try
        {
            while (!_stoppingCts.IsCancellationRequested)
            {
                UdpReceiveResult data;

                try
                {
                    data = await _socket.ReceiveAsync();

                    if (data.Buffer.Length == 0)
                    {
                        Logger.Fatal("Hazel read 0 bytes from UDP server socket.");
                        continue;
                    }
                }
                catch (SocketException)
                {
                    // Client no longer reachable, pretend it didn't happen
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    // Socket was disposed, don't care.
                    return;
                }

                await ProcessData(data);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Listen loop error");
        }
    }

    protected virtual async ValueTask ProcessData(UdpReceiveResult data)
    {
        // Get client from active clients
        if (!_allConnections.TryGetValue(data.RemoteEndPoint, out var client))
        {
            // Check for malformed connection attempts
            if (data.Buffer[0] != (byte)UdpSendOption.Hello) return;

            // Check rateLimit.
            if (!_connectionRateLimit.IsAllowed(data.RemoteEndPoint.Address))
            {
                Logger.Warning("Ratelimited connection attempt from {0}.", data.RemoteEndPoint);
                return;
            }

            // Create new client
            client = new UdpServerConnection(this, data.RemoteEndPoint, IPMode, _readerPool);

            // Store the client
            if (!_allConnections.TryAdd(data.RemoteEndPoint, client))
                throw new HazelException("Failed to add a connection. This should never happen.");

            // Activate the reader loop of the client
            await client.StartAsync();
        }

        // Write to client.
        await client.Pipeline.Writer.WriteAsync(data.Buffer);
    }

    /// <summary>
    ///     Sends data from the listener socket.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="length"></param>
    /// <param name="endPoint">The endpoint to send to.</param>
    internal virtual async ValueTask SendData(byte[] bytes, int length, IPEndPoint endPoint)
    {
        if (length > bytes.Length) return;

        try
        {
            await _socket.SendAsync(bytes, length, endPoint);
        }
        catch (SocketException e)
        {
            Logger.Error(e, "Could not send data as a SocketException occurred");
        }
        catch (ObjectDisposedException)
        {
            //Keep alive timer probably ran, ignore
        }
    }

    /// <summary>
    ///     Removes a virtual connection from the list.
    /// </summary>
    /// <param name="endPoint">The endpoint of the virtual connection.</param>
    internal void RemoveConnectionTo(EndPoint endPoint)
    {
        _allConnections.TryRemove(endPoint, out var conn);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        foreach (var kvp in _allConnections) kvp.Value.Dispose();

        await StopAsync();

        await _reliablePacketTimer.DisposeAsync();

        _connectionRateLimit.Dispose();

        await base.DisposeAsync();
    }
}