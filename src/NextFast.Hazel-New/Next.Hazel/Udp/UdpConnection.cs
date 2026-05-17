using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Next.Hazel.Abstractions;
using Serilog;

namespace Next.Hazel.Udp;

/// <summary>
///     Represents a connection that uses the UDP protocol.
/// </summary>
/// <inheritdoc />
public abstract partial class UdpConnection : NetworkConnection
{
    private const int SioUdpConnectionReset = -1744830452;
    private static readonly ILogger Logger = Log.ForContext<UdpConnection>();

    public static readonly byte[] EmptyDisconnectBytes = new[] { (byte)UdpSendOption.Disconnect };

    private readonly ConnectionListener _listener;
    protected readonly ObjectPool<MessageReader> _readerPool;
    private readonly CancellationTokenSource _stoppingCts;
    private Task _executingTask;

    private bool _isDisposing;
    private bool _isFirst = true;

    protected UdpConnection(ConnectionListener listener, ObjectPool<MessageReader> readerPool)
    {
        _listener = listener;
        _readerPool = readerPool;
        _stoppingCts = new CancellationTokenSource();

        Pipeline = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }

    public override float AveragePingMs => _pingMs;

    internal Channel<byte[]> Pipeline { get; }

    public Task StartAsync()
    {
        // Store the task we're executing
        _executingTask = Task.Factory.StartNew(ReadAsync, TaskCreationOptions.LongRunning);

        // If the task is completed then return it, this will bubble cancellation and failure to the caller
        if (_executingTask.IsCompleted) return _executingTask;

        // Otherwise it's running
        return Task.CompletedTask;
    }

    public void Stop()
    {
        // Stop called without start
        if (_executingTask == null) return;

        // Signal cancellation to methods.
        _stoppingCts.Cancel();

        try
        {
            // Cancel reader.
            Pipeline.Writer.Complete();
        }
        catch (ChannelClosedException)
        {
            // Already done.
        }

        // Remove references.
        if (!_isDisposing) Dispose(true);
    }

    private async Task ReadAsync()
    {
        var reader = new MessageReader(_readerPool);

        while (!_stoppingCts.IsCancellationRequested)
        {
            var result = await Pipeline.Reader.ReadAsync(_stoppingCts.Token);

            try
            {
                reader.Update(result);

                await HandleReceive(reader, reader.Length);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception during ReadAsync");
                Dispose(true);
                break;
            }
        }
    }

    /// <summary>
    ///     Writes the given bytes to the connection.
    /// </summary>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="length"></param>
    protected abstract ValueTask WriteBytesToConnection(byte[] bytes, int length);

    /// <inheritdoc />
    public override async ValueTask SendAsync(IMessageWriter msg)
    {
        if (_state != ConnectionState.Connected)
            throw new InvalidOperationException(
                "Could not send data as this Connection is not connected. Did you disconnect?");

        var buffer = new byte[msg.Length];
        Buffer.BlockCopy(msg.Buffer, 0, buffer, 0, msg.Length);

        switch (msg.SendOption)
        {
            case MessageType.Reliable:
                ResetKeepAliveTimer();

                AttachReliableID(buffer, 1);
                await WriteBytesToConnection(buffer, buffer.Length);
                Statistics.LogReliableSend(buffer.Length - 3, buffer.Length);
                break;

            default:
                await WriteBytesToConnection(buffer, buffer.Length);
                Statistics.LogUnreliableSend(buffer.Length - 1, buffer.Length);
                break;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <include file="DocInclude/common.xml" path="docs/item[@name='Connection_SendBytes_General']/*" />
    ///     <para>
    ///         Udp connections can currently send messages using <see cref="MessageType.Unreliable" /> and
    ///         <see cref="MessageType.Reliable" />. Fragmented messages are not currently supported and will default to
    ///         <see cref="MessageType.Unreliable" /> until implemented.
    ///     </para>
    /// </remarks>
    public override async ValueTask SendBytes(byte[] bytes, MessageType sendOption = MessageType.Unreliable)
    {
        //Add header information and send
        await HandleSend(bytes, (byte)sendOption);
    }

    /// <summary>
    ///     Handles the reliable/fragmented sending from this connection.
    /// </summary>
    /// <param name="data">The data being sent.</param>
    /// <param name="sendOption"></param>
    /// <param name="ackCallback">The callback to invoke when this packet is acknowledged.</param>
    /// <returns>The bytes that should actually be sent.</returns>
    protected async ValueTask HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
    {
        switch (sendOption)
        {
            case (byte)UdpSendOption.Ping:
            case (byte)MessageType.Reliable:
            case (byte)UdpSendOption.Hello:
                await ReliableSend(sendOption, data, ackCallback);
                break;

            //Treat all else as unreliable
            default:
                await UnreliableSend(sendOption, data);
                break;
        }
    }

    /// <summary>
    ///     Handles the receiving of data.
    /// </summary>
    /// <param name="message">The buffer containing the bytes received.</param>
    /// <param name="bytesReceived"></param>
    internal virtual async ValueTask HandleReceive(MessageReader message, int bytesReceived)
    {
        // Check if the first message received is the hello packet.
        if (_isFirst)
        {
            _isFirst = false;

            // Slice 4 bytes to get handshake data.
            if (_listener != null)
            {
                using var handshake = message.Copy(4);
                await _listener.InvokeNewConnection(handshake, this);
            }
        }

        switch (message.Buffer[0])
        {
            //Handle reliable receives
            case (byte)MessageType.Reliable:
                await ReliableMessageReceive(message, bytesReceived);
                break;

            //Handle acknowledgments
            case (byte)UdpSendOption.Acknowledgement:
                AcknowledgementMessageReceive(message.Buffer, bytesReceived);
                break;

            //We need to acknowledge hello and ping messages but dont want to invoke any events!
            case (byte)UdpSendOption.Ping:
            case (byte)UdpSendOption.Hello:
                await ProcessReliableReceive(message.Buffer, 1);
                Statistics.LogHelloReceive(bytesReceived);
                break;

            case (byte)UdpSendOption.Disconnect:
                message.Offset = 1;
                message.Position = 0;
                await DisconnectRemote("The remote sent a disconnect request", message);
                break;

            //Treat everything else as unreliable
            default:
                await InvokeDataReceived(MessageType.Unreliable, message, 1, bytesReceived);
                Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                break;
        }
    }

    /// <summary>
    ///     Sends bytes using the unreliable UDP protocol.
    /// </summary>
    /// <param name="sendOption">The SendOption to attach.</param>
    /// <param name="data">The data.</param>
    private ValueTask UnreliableSend(byte sendOption, byte[] data)
    {
        return UnreliableSend(sendOption, data, 0, data.Length);
    }

    /// <summary>
    ///     Sends bytes using the unreliable UDP protocol.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="sendOption">The SendOption to attach.</param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    private async ValueTask UnreliableSend(byte sendOption, byte[] data, int offset, int length)
    {
        var bytes = new byte[length + 1];

        //Add message type
        bytes[0] = sendOption;

        //Copy data into new array
        Buffer.BlockCopy(data, offset, bytes, bytes.Length - length, length);

        //Write to connection
        await WriteBytesToConnection(bytes, bytes.Length);

        Statistics.LogUnreliableSend(length, bytes.Length);
    }

    /// <summary>
    ///     Helper method to invoke the data received event.
    /// </summary>
    /// <param name="sendOption">The send option the message was received with.</param>
    /// <param name="buffer">The buffer received.</param>
    /// <param name="dataOffset">The offset of data in the buffer.</param>
    /// <param name="bytesReceived"></param>
    private ValueTask InvokeDataReceived(MessageType sendOption, MessageReader buffer, int dataOffset,
        int bytesReceived)
    {
        buffer.Offset = dataOffset;
        buffer.Length = bytesReceived - dataOffset;
        buffer.Position = 0;

        return InvokeDataReceived(buffer, sendOption);
    }

    /// <summary>
    ///     Sends a hello packet to the remote endpoint.
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="acknowledgeCallback">The callback to invoke when the hello packet is acknowledged.</param>
    protected ValueTask SendHello(byte[] bytes, Action acknowledgeCallback)
    {
        //First byte of handshake is version indicator so add data after
        byte[] actualBytes;
        if (bytes == null)
        {
            actualBytes = new byte[1];
        }
        else
        {
            actualBytes = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, actualBytes, 1, bytes.Length);
        }

        return HandleSend(actualBytes, (byte)UdpSendOption.Hello, acknowledgeCallback);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isDisposing = true;

            Stop();
            DisposeKeepAliveTimer();
            DisposeReliablePackets();
        }

        base.Dispose(disposing);
    }
}