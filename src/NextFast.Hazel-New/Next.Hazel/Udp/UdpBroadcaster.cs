using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Next.Hazel.Udp;

///
public class UdpBroadcaster : IDisposable
{
    private readonly EndPoint endpoint;
    private readonly Action<string> logger;
    private byte[] data;
    private Socket socket;

    ///
    public UdpBroadcaster(int port, Action<string> logger = null)
    {
        this.logger = logger;
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.EnableBroadcast = true;
        socket.MulticastLoopback = false;
        endpoint = new IPEndPoint(IPAddress.Broadcast, port);
    }

    ///
    public void Dispose()
    {
        if (socket == null) return;
        try
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            socket.Dispose();
        }
        catch
        {
            // ignored
        }

        socket = null;
    }

    ///
    public void SetData(string dataValue)
    {
        var len = Encoding.UTF8.GetByteCount(dataValue);
        data = new byte[len + 2];
        data[0] = 4;
        data[1] = 2;

        Encoding.UTF8.GetBytes(dataValue, 0, dataValue.Length, data, 2);
    }

    ///
    public void Broadcast()
    {
        if (data == null) return;

        try
        {
            socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, endpoint, FinishSendTo, null);
        }
        catch (Exception e)
        {
            logger?.Invoke("BroadcastListener: " + e);
        }
    }

    private void FinishSendTo(IAsyncResult evt)
    {
        try
        {
            socket.EndSendTo(evt);
        }
        catch (Exception e)
        {
            logger?.Invoke("BroadcastListener: " + e);
        }
    }
}