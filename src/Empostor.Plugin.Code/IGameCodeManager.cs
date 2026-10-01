using System.Collections.Generic;
using Empostor.Api.Games;

namespace Empostor.Plugin.Code;

/// <summary>One room code in the pool, for the admin panel's entry list.</summary>
public sealed record PoolEntry(GameCode Code, bool InUse, string Source);

public interface IGameCodeManager
{
    int SixCharCodes { get; }

    int FourCharCodes { get; }

    string Path { get; }

    GameCode Get();

    void Release(GameCode code);

    /// <summary>Every tracked code (available + in use), sorted by code text.</summary>
    IReadOnlyList<PoolEntry> Entries { get; }

    /// <summary>
    ///     Adds a code to the available pool and appends it to <c>Boot.Codes/00-admin-added.txt</c>
    ///     so it survives a restart. Returns (true, confirmation) or (false, reason).
    /// </summary>
    (bool Ok, string Message) Add(string codeText);

    /// <summary>
    ///     Removes a code from the pool. Returns (true, message) — the message may carry a note
    ///     when the code comes from a hand-maintained word list file.
    /// </summary>
    (bool Ok, string Message) Remove(GameCode code);

    /// <summary>Re-reads <c>Boot.Codes/*.txt</c>. Codes currently in use are kept.</summary>
    int Reload();
}
