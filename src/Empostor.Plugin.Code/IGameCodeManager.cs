using Empostor.Api.Games;

namespace Empostor.Plugin.Code;

public interface IGameCodeManager
{
    int SixCharCodes { get; }

    int FourCharCodes { get; }

    string Path { get; }

    GameCode Get();

    void Release(GameCode code);
}
