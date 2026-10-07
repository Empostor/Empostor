using System;
using System.Threading;

namespace Empostor.Server.Localization;

internal sealed class LocalizationRegistration : IDisposable
{
    private Action? _unregister;
    public LocalizationRegistration(Action unregister) => _unregister = unregister;
    public void Dispose() => Interlocked.Exchange(ref _unregister, null)?.Invoke();
}
