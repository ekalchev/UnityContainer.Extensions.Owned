using Unity.Builder;
using Unity.Extension;

namespace UnityOwnedT;

public class OwnedExtension : UnityContainerExtension
{
    protected override void Initialize()
    {
        Context.Strategies.Add(new OwnedBuildStrategy(), UnityBuildStage.PreCreation);
        Context.Strategies.Add(new DisposalTrackingStrategy(), UnityBuildStage.PostInitialization);
    }
}
