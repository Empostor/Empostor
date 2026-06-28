using System;

namespace Next.Hazel.UPnP;

public interface ILogger
{
    void WriteVerbose(string msg);
    void WriteError(string msg);
    void WriteInfo(string msg);
}

public class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    public void WriteVerbose(string msg)
    {
    }

    public void WriteError(string msg)
    {
    }

    public void WriteInfo(string msg)
    {
    }
}

public class ConsoleLogger : ILogger
{
    private readonly bool verbose;

    public ConsoleLogger(bool verbose)
    {
        this.verbose = verbose;
    }

    public void WriteVerbose(string msg)
    {
        if (verbose) Console.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [VRB] {msg}");
    }

    public void WriteError(string msg)
    {
        Console.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [ERR] {msg}");
    }

    public void WriteInfo(string msg)
    {
        Console.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [INF] {msg}");
    }
}