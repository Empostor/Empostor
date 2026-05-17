using System;
using System.Net;

namespace Next.Hazel.Dtls;

public struct ConnectionId : IEquatable<ConnectionId>
{
    public IPEndPoint EndPoint;
    public int Serial;

    public static ConnectionId Create(IPEndPoint endPoint, int serial)
    {
        return new ConnectionId
        {
            EndPoint = endPoint,
            Serial = serial
        };
    }

    public bool Equals(ConnectionId other)
    {
        return Serial == other.Serial
               && EndPoint.Equals(other.EndPoint)
            ;
    }

    public override bool Equals(object obj)
    {
        if (obj is ConnectionId) return Equals((ConnectionId)obj);

        return false;
    }

    public override int GetHashCode()
    {
        return EndPoint.GetHashCode();
    }

    public static bool operator ==(ConnectionId left, ConnectionId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ConnectionId left, ConnectionId right)
    {
        return !(left == right);
    }
}