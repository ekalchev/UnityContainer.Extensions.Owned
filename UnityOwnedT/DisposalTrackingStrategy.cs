using Unity.Builder;
using Unity.Lifetime;
using Unity.Strategies;

namespace UnityOwnedT;

internal class DisposalTrackingStrategy : BuilderStrategy
{
    public override void PostBuildUp(ref BuilderContext context)
    {
        if (!OwnedBuildStrategy.IsInsideOwnedScope.Value)
            return;

        if (context.Existing is IDisposable disposable)
        {
            var lm = context.Get(context.RegistrationType, context.Name, typeof(LifetimeManager));
            if (lm is not ContainerControlledLifetimeManager)
                context.Lifetime.Add(disposable);
        }
    }
}
