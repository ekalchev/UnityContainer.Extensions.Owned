using Unity.Builder;
using Unity.Lifetime;
using Unity.Strategies;

namespace Unity.Extensions.Owned;

internal class DisposalTrackingStrategy : BuilderStrategy
{
    private static readonly Type OwnedScopeMarkerType = typeof(OwnedScopeMarker);
    private static readonly Type LifetimeManagerType = typeof(LifetimeManager);
    private static readonly Type OwnedOpenGenericType = typeof(Owned<>);

    public override void PostBuildUp(ref BuilderContext context)
    {
        // IDisposable check is a single IL isinst — cheaper than context.Get dictionary lookup
        if (context.Existing is not IDisposable disposable)
            return;

        if (context.Get(OwnedScopeMarkerType, null, LifetimeManagerType) is null)
            return;

        var type = context.Type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == OwnedOpenGenericType)
            return;

        var lm = context.Get(context.RegistrationType, context.Name, LifetimeManagerType);
        if (lm is not ContainerControlledLifetimeManager and not ExternallyControlledLifetimeManager)
            context.Lifetime.Add(disposable);
    }
}
