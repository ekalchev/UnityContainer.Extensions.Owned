# UnityOwnedT

Autofac-style `Owned<T>` for [Unity Container](https://github.com/unitycontainer/unity) (v5.11.x).

`Owned<T>` gives you **deterministic disposal** of resolved services and their entire dependency graph. Each `Owned<T>` creates an isolated scope — when you dispose it, all transient and hierarchical dependencies created within that scope are disposed with it.

## Usage

### Register the extension

```csharp
using Unity;
using UnityOwnedT;

var container = new UnityContainer();
container.AddExtension(new OwnedExtension());
```

### Resolve `Owned<T>`

```csharp
container.RegisterType<IRepository, Repository>();

// Each resolve creates an isolated scope
var owned = container.Resolve<Owned<IRepository>>();
IRepository repo = owned.Value;

// Use the service...
repo.Save("some data");

// Dispose the owned scope — disposes repo and all its transient dependencies
owned.Dispose();
```

### Inject `Owned<T>` as a dependency

```csharp
public class MyService : IDisposable
{
    private readonly Owned<IRepository> _ownedRepo;

    public MyService(Owned<IRepository> ownedRepo)
    {
        _ownedRepo = ownedRepo;
    }

    public void DoWork()
    {
        _ownedRepo.Value.Save("data");
    }

    public void Dispose()
    {
        _ownedRepo.Dispose(); // Disposes the repository and its scope
    }
}
```

## Behavior

### Lifetime handling

| Registration lifetime | Behavior inside `Owned<T>` |
|---|---|
| Transient | New instance created and disposed with scope |
| Hierarchical (per-container) | New instance per scope, disposed with scope |
| Singleton (`ContainerControlled`) | Shared instance, **not** disposed by scope |
| `ExternallyControlled` | Instance resolved but **not** disposed by scope |

### Scope isolation

- Each `Owned<T>` gets its own child container as its scope
- Disposing `Owned<T>` disposes all transient/hierarchical services resolved within that scope
- Singletons are resolved from the parent and are never disposed by `Owned<T>`
- Nested `Owned<Owned<T>>` scopes are independent — disposing outer does not cascade to inner
- If resolution fails, the child container is disposed immediately (no leak)

### Child container support

`Owned<T>` works correctly when resolved from child containers. It preserves the registration hierarchy — services registered on child containers are visible to the owned scope.

## How it works

The extension registers two `BuilderStrategy` implementations into Unity's build pipeline:

1. **`OwnedBuildStrategy`** (PreCreation stage) — intercepts resolution of `Owned<T>`, creates a child container, resolves `T` from it, and wraps both in `Owned<T>`
2. **`DisposalTrackingStrategy`** (PostInitialization stage) — tracks `IDisposable` instances created within an owned scope so they are disposed when the scope is disposed

An `OwnedScopeMarker` registered on the child container is used to detect whether the current resolution is happening inside an owned scope, using Unity's `BuilderContext` policy lookup.

## Tests

The test suite (109 tests) validates behavior against Autofac's `Owned<T>` side-by-side to ensure identical semantics. Run with:

```
dotnet test
```

## License

MIT
