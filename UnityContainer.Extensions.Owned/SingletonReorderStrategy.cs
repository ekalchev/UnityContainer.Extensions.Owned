using Unity.Builder;
using Unity.Lifetime;
using Unity.Strategies;

namespace UnityContainer.Extensions.Owned;

/// <summary>
/// Reorders singleton lifetime managers in Unity's <see cref="ILifetimeContainer"/> from
/// registration order to creation order. When a singleton is first created, this strategy
/// removes its <see cref="ContainerControlledLifetimeManager"/> from the lifetime container
/// and re-adds it at the end. Since Unity disposes in reverse order (LIFO), the final
/// disposal order becomes reverse creation order — which naturally respects the dependency
/// graph because dependencies are always created before their dependents.
/// </summary>
internal class SingletonReorderStrategy : BuilderStrategy
{
    private static readonly Type LifetimeManagerType = typeof(LifetimeManager);

    public override void PostBuildUp(ref BuilderContext context)
    {
        object? lm = context.Get(context.RegistrationType, context.Name, LifetimeManagerType);
        if (lm is not ContainerControlledLifetimeManager lifetimeManager)
        {
            return;
        }

        // Move the lifetime manager to the end of the disposal list.
        // This reorders from registration order to creation order.
        context.Lifetime.Remove(lifetimeManager);
        context.Lifetime.Add(lifetimeManager);
    }
}
