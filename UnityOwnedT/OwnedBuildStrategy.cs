using System.Reflection;
using Unity;
using Unity.Builder;
using Unity.Lifetime;
using Unity.Strategies;

namespace UnityOwnedT;

public class OwnedBuildStrategy : BuilderStrategy
{
    internal static readonly AsyncLocal<bool> IsInsideOwnedScope = new();

    public override void PreBuildUp(ref BuilderContext context)
    {
        var type = context.Type;

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Owned<>))
            return;

        var innerType = type.GetGenericArguments()[0];

        if (context.Get(innerType, context.Name, typeof(LifetimeManager)) is ContainerControlledLifetimeManager)
        {
            var singleton = context.Container.Resolve(innerType, context.Name);
            var owned = CreateOwned(type, singleton, NoOpScope.Instance);
            context.Existing = owned;
            context.BuildComplete = true;
            return;
        }

        var child = context.Container.CreateChildContainer();
        context.Lifetime.Remove(child);
        var previous = IsInsideOwnedScope.Value;
        IsInsideOwnedScope.Value = true;

        try
        {
            var resolved = child.Resolve(innerType, context.Name, context.Overrides);
            var owned = CreateOwned(type, resolved, (IDisposable)child);
            context.Existing = owned;
            context.BuildComplete = true;
        }
        catch
        {
            child.Dispose();
            throw;
        }
        finally
        {
            IsInsideOwnedScope.Value = previous;
        }
    }

    private static object CreateOwned(Type ownedType, object value, IDisposable scope) =>
        Activator.CreateInstance(
            ownedType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { value, scope },
            null)!;

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }
}
