using System.Reflection;
using Unity.Builder;
using Unity.Lifetime;
using Unity.Strategies;

namespace Unity.Extensions.Owned;

public class OwnedBuildStrategy : BuilderStrategy
{
    private static readonly Type OwnedOpenGenericType = typeof(Owned<>);
    private static readonly Type LifetimeManagerType = typeof(LifetimeManager);
    private static readonly BindingFlags CtorFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    public override void PreBuildUp(ref BuilderContext context)
    {
        var type = context.Type;

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != OwnedOpenGenericType)
            return;

        var innerType = type.GetGenericArguments()[0];

        if (context.Get(innerType, context.Name, LifetimeManagerType) is ContainerControlledLifetimeManager)
        {
            var singleton = context.Container.Resolve(innerType, context.Name);
            context.Existing = CreateOwned(type, singleton, NoOpScope.Instance);
            context.BuildComplete = true;
            return;
        }

        var child = context.Container.CreateChildContainer();
        context.Lifetime.Remove(child);
        child.RegisterInstance(OwnedScopeMarker.Instance);

        try
        {
            var resolved = child.Resolve(innerType, context.Name, context.Overrides);
            context.Existing = CreateOwned(type, resolved, (IDisposable)child);
            context.BuildComplete = true;
        }
        catch
        {
            child.Dispose();
            throw;
        }
    }

    private static object CreateOwned(Type ownedType, object value, IDisposable scope) =>
        Activator.CreateInstance(ownedType, CtorFlags, null, [value, scope], null)!;

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }
}

internal sealed class OwnedScopeMarker
{
    public static readonly OwnedScopeMarker Instance = new();
}
