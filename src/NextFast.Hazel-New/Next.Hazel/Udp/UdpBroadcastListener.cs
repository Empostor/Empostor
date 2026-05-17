using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Next.Hazel.Udp;

public class BroadcastPacket
{
    public string Data;
    public DateTime ReceiveTime;
    public IPEndPoint Sender;

    public BroadcastPacket(string data, IPEndPoint sender)
    {
        Data = data;
        Sender = sender;
        ReceiveTime = DateTime.Now;
    }

    public string GetAddress()
    {
        return Sender.Address.ToString();
    }
}

public class UdpBroadcastListener : IDisposable
{
    private readonly byte[] buffer = new byte[1024];
    private readonly Action<string> logger;

    private readonly List<BroadcastPacket> packets = new();
    private Socket socket;

    ///
    public UdpBroadcastListener(int port, Action<string> logger = null)
    {
        this.logger = logger;
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.EnableBroadcast = true;
        socket.MulticastLoopback = false;
        EndPoint endpoint1 = new IPEndPoint(IPAddress.Any, port);
        socket.Bind(endpoint1);
    }

    public bool Running { get; private set; }

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
        catch (Exception e)
        {
            logger.Invoke(e.ToString());
        }

        socket = null;
    }

    ///
    public void StartListen()
    {
        if (Running) return;
        Running = true;

        try
        {
            EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);
            socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref endpt, HandleData, null);
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception e)
        {
            logger?.Invoke("BroadcastListener: " + e);
            Dispose();
        }
    }

    private void HandleData(IAsyncResult result)
    {
        Running = false;

        int numBytes;
        EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            numBytes = socket.EndReceiveFrom(result, ref endpt);
        }
        catch (NullReferenceException)
        {
            // Already disposed
            return;
        }
        catch (Exception e)
        {
            logger?.Invoke("BroadcastListener: " + e);
            Dispose();
            return;
        }

        if (numBytes < 3
            || buffer[0] != 4 || buffer[1] != 2)
        {
            StartListen();
            return;
        }

        var ipEnd = (IPEndPoint)endpt;
        var data = Encoding.UTF8.GetString(buffer, 2, numBytes - 2);
        var dataHash = data.GetHashCode();

        lock (packets)
        {
            for (var i = 0; i < packets.Count; ++i)
            {
                var pkt = packets[i];
                if (pkt == null || pkt.Data == null)
                {
                    packets.RemoveAt(i);
                    i--;
                    continue;
                }

                if (pkt.Data.GetHashCode() != dataHash
                    || !pkt.Sender.Equals(ipEnd)) continue;
                packets[i].ReceiveTime = DateTime.Now;
                break;
            }

            packets.Add(new BroadcastPacket(data, ipEnd));
        }

        StartListen();
    }

    ///
    public BroadcastPacket[] GetPackets()
    {
        lock (packets)
        {
            var output = packets.ToArray();
            packets.Clear();
            return output;
        }
    }
}