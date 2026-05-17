# Contributing to Empostor

Thank you for your interest in contributing! We welcome bug reports, feature suggestions, and pull requests.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An Among Us installation (for testing)
- Git

### Build

```bash
git clone https://github.com/your-org/Empostor.git
cd Empostor
dotnet build src/Impostor.sln
```

### Run locally

```bash
dotnet run --project src/Core/Impostor.Server
```

---

## Reporting Issues

- Search [existing issues](../../issues) before opening a new one.
- Include your Empostor version, .NET version, and steps to reproduce.
- For security vulnerabilities, **do not open a public issue** — contact us on Discord directly.

---

## Pull Requests

- **One feature / fix per PR** — keeps review focused and history clean.
- Branch from `master` and name your branch meaningfully (e.g. `fix/language-fallback`, `feat/report-dashboard`).
- Write clear commit messages.
- Ensure the project compiles without errors or StyleCop warnings before submitting.
- If your PR adds a user-facing feature, update or add the relevant doc in `docs/`.
- Do **not** commit `bin/`, `obj/`, or `packages.lock.json` files.

### PR checklist

- [ ] Code compiles and existing tests pass
- [ ] No `bin/` or `obj/` files committed
- [ ] StyleCop warnings resolved
- [ ] Relevant docs updated (if applicable)
- [ ] Single logical change per PR

---

## Code Style

- Follow the existing patterns in the codebase.
- The `.editorconfig` and `stylecop.json` files enforce formatting — let your IDE apply them.
- Keep variable names concise but descriptive; no abbreviations unless they are widely understood (`ctx`, `cfg`, `id`).
- Avoid unnecessary blank lines or trailing spaces.

---

## Writing a Plugin

Plugins implement `PluginBase` + `IPluginStartup` and reference `Impostor.Api`.

```csharp
[ImpostorPlugin("com.example.myplugin", "My Plugin", "Author", "1.0.0")]
public sealed class MyPlugin : PluginBase
{
    public override ValueTask EnableAsync()
    {
        // load config: var cfg = LoadConfig<MyConfig>();
        return default;
    }
}
```

See **[docs/Writing-a-plugin.md](docs/Writing-a-plugin.md)** for the full guide.

Custom commands can be registered by injecting `CommandService` and calling `Register(new MyCommand())`. The `ICommand` interface lives in `Impostor.Api.Commands` so plugins can use it without referencing `Impostor.Server`.

---

## Questions

Join us on [Discord](https://discord.gg/5fPmpxxnrc) — we're happy to help.
