#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Next.Hazel.Abstractions;

namespace Next.Hazel.Udp;

internal sealed class UdpServerConnection : UdpConnection
{
    /// <summary>
    ///     Creates a UdpConnection for the virtual connection to the endpoint.
    /// </summary>
    /// <param name="listener">The listener that created this connection.</param>
    /// <param name="endPoint">The endpoint that we are connected to.</param>
    /// <param name="IPMode">The IPMode we are connected using.</param>
    /// <param name="readerPool"></param>
    internal UdpServerConnection(UdpConnectionListener listener, IPEndPoint endPoint, IPMode IPMode,
        ObjectPool<MessageReader> readerPool)
        : base(listener, readerPool)
    {
        Listener = listener;
        EndPoint = endPoint;
        this.IPMode = IPMode;

        State = ConnectionState.Connected;
        InitializeKeepAliveTimer();
    }

    /// <summary>
    ///     The connection listener that we use the socket of.
    /// </summary>
    /// <remarks>
    ///     Udp server connections utilize the same socket in the listener for sends/receives, this is the listener that
    ///     created this connection and is hence the listener this conenction sends and receives via.
    /// </remarks>
    public UdpConnectionListener Listener { get; }

    /// <inheritdoc />
    protected override ValueTask WriteBytesToConnection(byte[] bytes, int length)
    {
        return Listener.SendData(bytes, length, EndPoint);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     This will always throw a HazelException.
    /// </remarks>
    public override ValueTask ConnectAsync(byte[] bytes = null!, int timeout = 5000)
    {
        throw new InvalidOperationException(
            "Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
    }

    /// <summary>
    ///     Sends a disconnect message to the end point.
    /// </summary>
    protected override async ValueTask<bool> SendDisconnect(MessageWriter? data)
    {
        lock (this)
        {
            if (_state != ConnectionState.Connected) return false;
            _state = ConnectionState.NotConnected;
        }

        var bytes = EmptyDisconnectBytes;
        if (data is { Length: > 0 })
        {
            if (data.SendOption != MessageType.Unreliable)
                throw new ArgumentException("Disconnect messages can only be unreliable.");

            bytes = data.ToByteArray(true);
            bytes[0] = (byte)UdpSendOption.Disconnect;
        }

        try
        {
            await Listener.SendData(bytes, bytes.Length, EndPoint);
        }
        catch
        {
            // ignored
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        Listener.RemoveConnectionTo(EndPoint);

        if (disposing) _ = SendDisconnect(null);

        base.Dispose(disposing);
    }
}