using Autofac;
using NUnit.Framework;
using Unity;
using Unity.Resolution;

namespace UnityOwnedT.Tests;

[TestFixture]
public class OwnedBehaviorTests
{
    [SetUp]
    public void SetUp()
    {
        TrackedService.ResetCounter();
        Dependency.ResetCounter();
        ServiceWithDependency.ResetCounter();
        DeepRoot.ResetCounter();
        NonDisposableService.ResetCounter();
    }

    [Test]
    public void EachResolve_ProducesNewInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        using var afOwned1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        using var afOwned2 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var autofacDifferent = afOwned1.Value.Id != afOwned2.Value.Id;

        using var uOwned1 = unity.Resolve<Owned<ITrackedService>>();
        using var uOwned2 = unity.Resolve<Owned<ITrackedService>>();
        var unityDifferent = uOwned1.Value.Id != uOwned2.Value.Id;

        Assert.That(autofacDifferent, Is.True);
        Assert.That(unityDifferent, Is.EqualTo(autofacDifferent));
    }

    [Test]
    public void Dispose_DisposesInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;
        afOwned.Dispose();

        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void Dispose_DoesNotAffectOtherResolutions()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afRegular = (TrackedService)autofac.Resolve<ITrackedService>();
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        afOwned.Dispose();

        var uRegular = (TrackedService)unity.Resolve<ITrackedService>();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        uOwned.Dispose();

        Assert.That(afRegular.IsDisposed, Is.False);
        Assert.That(uRegular.IsDisposed, Is.EqualTo(afRegular.IsDisposed));
    }

    [Test]
    public void OwnedWithSingleton_ReturnsSameInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();

        var afSingleton = autofac.Resolve<ITrackedService>();
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSameInstance = afSingleton.Id == afOwned.Value.Id;

        var uSingleton = unity.Resolve<ITrackedService>();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uSameInstance = uSingleton.Id == uOwned.Value.Id;

        Assert.That(afSameInstance, Is.True);
        Assert.That(uSameInstance, Is.EqualTo(afSameInstance));

        afOwned.Dispose();
        uOwned.Dispose();
    }

    [Test]
    public void OwnedWithSingleton_DisposeDoesNotDisposeSingleton()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();

        var afSingleton = (TrackedService)autofac.Resolve<ITrackedService>();
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        afOwned.Dispose();

        var uSingleton = (TrackedService)unity.Resolve<ITrackedService>();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        uOwned.Dispose();

        Assert.That(afSingleton.IsDisposed, Is.False);
        Assert.That(uSingleton.IsDisposed, Is.EqualTo(afSingleton.IsDisposed));
    }

    [Test]
    public void Dispose_CascadesToDependencies()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<ServiceWithDependency>().As<IServiceWithDependency>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithDependency>>();
        var afService = (ServiceWithDependency)afOwned.Value;
        var afDep = (Dependency)afService.Dependency;
        afOwned.Dispose();

        var uOwned = unity.Resolve<Owned<IServiceWithDependency>>();
        var uService = (ServiceWithDependency)uOwned.Value;
        var uDep = (Dependency)uService.Dependency;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(afDep.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void Dispose_DoesNotCascadeToSingletonDependency()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>().SingleInstance();
        autofacBuilder.RegisterType<ServiceWithDependency>().As<IServiceWithDependency>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<IDependency, Dependency>();
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithDependency>>();
        var afService = (ServiceWithDependency)afOwned.Value;
        var afDep = (Dependency)afService.Dependency;
        afOwned.Dispose();

        var uOwned = unity.Resolve<Owned<IServiceWithDependency>>();
        var uService = (ServiceWithDependency)uOwned.Value;
        var uDep = (Dependency)uService.Dependency;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(afDep.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void MultipleOwned_HaveIndependentLifetimes()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOwned1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afOwned2 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSvc1 = (TrackedService)afOwned1.Value;
        var afSvc2 = (TrackedService)afOwned2.Value;
        afOwned1.Dispose();

        var uOwned1 = unity.Resolve<Owned<ITrackedService>>();
        var uOwned2 = unity.Resolve<Owned<ITrackedService>>();
        var uSvc1 = (TrackedService)uOwned1.Value;
        var uSvc2 = (TrackedService)uOwned2.Value;
        uOwned1.Dispose();

        Assert.That(afSvc1.IsDisposed, Is.True);
        Assert.That(afSvc2.IsDisposed, Is.False);
        Assert.That(uSvc1.IsDisposed, Is.EqualTo(afSvc1.IsDisposed));
        Assert.That(uSvc2.IsDisposed, Is.EqualTo(afSvc2.IsDisposed));

        afOwned2.Dispose();
        uOwned2.Dispose();

        Assert.That(afSvc2.IsDisposed, Is.True);
        Assert.That(uSvc2.IsDisposed, Is.EqualTo(afSvc2.IsDisposed));
    }

    [Test]
    public void FuncOwned_PassesParametersAndCreatesOwnedInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Connection>().As<IConnection>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IConnection, Connection>();
        unity.RegisterFactory<Func<string, int, Owned<IConnection>>>(c =>
            (object)new Func<string, int, Owned<IConnection>>((connStr, retries) => c.Resolve<Owned<IConnection>>(
                new DependencyOverride<string>(connStr),
                new DependencyOverride<int>(retries))));

        var afFactory = autofac.Resolve<Func<string, int, Autofac.Features.OwnedInstances.Owned<IConnection>>>();
        var afOwned = afFactory("server=localhost", 3);

        var uFactory = unity.Resolve<Func<string, int, Owned<IConnection>>>();
        var uOwned = uFactory("server=localhost", 3);

        Assert.That(afOwned.Value.ConnectionString, Is.EqualTo("server=localhost"));
        Assert.That(afOwned.Value.MaxRetries, Is.EqualTo(3));
        Assert.That(uOwned.Value.ConnectionString, Is.EqualTo(afOwned.Value.ConnectionString));
        Assert.That(uOwned.Value.MaxRetries, Is.EqualTo(afOwned.Value.MaxRetries));

        afOwned.Dispose();
        uOwned.Dispose();
    }

    [Test]
    public void FuncOwned_EachInvocationCreatesNewScope()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Connection>().As<IConnection>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IConnection, Connection>();
        unity.RegisterFactory<Func<string, int, Owned<IConnection>>>(c =>
            (object)new Func<string, int, Owned<IConnection>>((connStr, retries) => c.Resolve<Owned<IConnection>>(
                new DependencyOverride<string>(connStr),
                new DependencyOverride<int>(retries))));

        var afFactory = autofac.Resolve<Func<string, int, Autofac.Features.OwnedInstances.Owned<IConnection>>>();
        using var afOwned1 = afFactory("a", 1);
        using var afOwned2 = afFactory("b", 2);

        var uFactory = unity.Resolve<Func<string, int, Owned<IConnection>>>();
        using var uOwned1 = uFactory("a", 1);
        using var uOwned2 = uFactory("b", 2);

        var afDifferent = !ReferenceEquals(afOwned1.Value, afOwned2.Value);
        var uDifferent = !ReferenceEquals(uOwned1.Value, uOwned2.Value);

        Assert.That(afDifferent, Is.True);
        Assert.That(uDifferent, Is.EqualTo(afDifferent));
    }

    [Test]
    public void FuncOwned_DisposeDisposesInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Connection>().As<IConnection>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IConnection, Connection>();
        unity.RegisterFactory<Func<string, int, Owned<IConnection>>>(c =>
            (object)new Func<string, int, Owned<IConnection>>((connStr, retries) => c.Resolve<Owned<IConnection>>(
                new DependencyOverride<string>(connStr),
                new DependencyOverride<int>(retries))));

        var afFactory = autofac.Resolve<Func<string, int, Autofac.Features.OwnedInstances.Owned<IConnection>>>();
        var afOwned = afFactory("conn", 1);
        var afConn = (Connection)afOwned.Value;
        afOwned.Dispose();

        var uFactory = unity.Resolve<Func<string, int, Owned<IConnection>>>();
        var uOwned = uFactory("conn", 1);
        var uConn = (Connection)uOwned.Value;
        uOwned.Dispose();

        Assert.That(afConn.IsDisposed, Is.True);
        Assert.That(uConn.IsDisposed, Is.EqualTo(afConn.IsDisposed));
    }

    [Test]
    public void FuncOwned_IndependentLifetimes()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Connection>().As<IConnection>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IConnection, Connection>();
        unity.RegisterFactory<Func<string, int, Owned<IConnection>>>(c =>
            (object)new Func<string, int, Owned<IConnection>>((connStr, retries) => c.Resolve<Owned<IConnection>>(
                new DependencyOverride<string>(connStr),
                new DependencyOverride<int>(retries))));

        var afFactory = autofac.Resolve<Func<string, int, Autofac.Features.OwnedInstances.Owned<IConnection>>>();
        var afOwned1 = afFactory("a", 1);
        var afOwned2 = afFactory("b", 2);
        var afConn1 = (Connection)afOwned1.Value;
        var afConn2 = (Connection)afOwned2.Value;
        afOwned1.Dispose();

        var uFactory = unity.Resolve<Func<string, int, Owned<IConnection>>>();
        var uOwned1 = uFactory("a", 1);
        var uOwned2 = uFactory("b", 2);
        var uConn1 = (Connection)uOwned1.Value;
        var uConn2 = (Connection)uOwned2.Value;
        uOwned1.Dispose();

        Assert.That(afConn1.IsDisposed, Is.True);
        Assert.That(afConn2.IsDisposed, Is.False);
        Assert.That(uConn1.IsDisposed, Is.EqualTo(afConn1.IsDisposed));
        Assert.That(uConn2.IsDisposed, Is.EqualTo(afConn2.IsDisposed));

        afOwned2.Dispose();
        uOwned2.Dispose();

        Assert.That(afConn2.IsDisposed, Is.True);
        Assert.That(uConn2.IsDisposed, Is.EqualTo(afConn2.IsDisposed));
    }

    [Test]
    public void ConcreteType_WorksWithoutInterfaceMapping()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());

        // Autofac needs interface registration; Unity resolves concrete types directly.
        // Both should dispose the instance when Owned is disposed.
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;
        afOwned.Dispose();

        var uOwned = unity.Resolve<Owned<TrackedService>>();
        var uService = uOwned.Value;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void ChildContainer_OwnedResolvesAndDisposesCorrectly()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.AddExtension(new OwnedExtension());

        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;
        afOwned.Dispose();

        var uOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void ChildContainer_OwnedDoesNotAffectParent()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.AddExtension(new OwnedExtension());

        var afParentInstance = (TrackedService)autofac.Resolve<ITrackedService>();
        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        afOwned.Dispose();

        var uParentInstance = (TrackedService)unity.Resolve<ITrackedService>();
        var uOwned = uChild.Resolve<Owned<ITrackedService>>();
        uOwned.Dispose();

        Assert.That(afParentInstance.IsDisposed, Is.False);
        Assert.That(uParentInstance.IsDisposed, Is.EqualTo(afParentInstance.IsDisposed));
    }

    [Test]
    public void ChildContainer_DisposingChildDoesNotAffectParentOwned()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;

        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;

        // Create and dispose child scopes — should not affect parent-resolved Owned<T>
        var afChild = autofac.BeginLifetimeScope();
        afChild.Dispose();

        var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.Dispose();

        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void ChildContainer_OwnedFromChildAndParentAreIndependent()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.AddExtension(new OwnedExtension());

        var afParentOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afChildOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afParentSvc = (TrackedService)afParentOwned.Value;
        var afChildSvc = (TrackedService)afChildOwned.Value;

        var uParentOwned = unity.Resolve<Owned<ITrackedService>>();
        var uChildOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uParentSvc = (TrackedService)uParentOwned.Value;
        var uChildSvc = (TrackedService)uChildOwned.Value;

        // Dispose only the child-resolved Owned
        afChildOwned.Dispose();
        uChildOwned.Dispose();

        Assert.That(afChildSvc.IsDisposed, Is.True);
        Assert.That(afParentSvc.IsDisposed, Is.False);
        Assert.That(uChildSvc.IsDisposed, Is.EqualTo(afChildSvc.IsDisposed));
        Assert.That(uParentSvc.IsDisposed, Is.EqualTo(afParentSvc.IsDisposed));

        afParentOwned.Dispose();
        uParentOwned.Dispose();

        Assert.That(afParentSvc.IsDisposed, Is.True);
        Assert.That(uParentSvc.IsDisposed, Is.EqualTo(afParentSvc.IsDisposed));
    }

    [Test]
    public void ChildContainer_SingletonFromParent_OwnedReturnsSameInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.AddExtension(new OwnedExtension());

        var afSingleton = autofac.Resolve<ITrackedService>();
        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSame = afSingleton.Id == afOwned.Value.Id;

        var uSingleton = unity.Resolve<ITrackedService>();
        var uOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uSame = uSingleton.Id == uOwned.Value.Id;

        Assert.That(afSame, Is.True);
        Assert.That(uSame, Is.EqualTo(afSame));

        // Disposing should not affect the singleton
        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(((TrackedService)afSingleton).IsDisposed, Is.False);
        Assert.That(((TrackedService)uSingleton).IsDisposed,
            Is.EqualTo(((TrackedService)afSingleton).IsDisposed));
    }

    [Test]
    public void DoubleDispose_DoesNotThrow()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();

        Assert.DoesNotThrow(() => { afOwned.Dispose(); afOwned.Dispose(); });
        Assert.DoesNotThrow(() => { uOwned.Dispose(); uOwned.Dispose(); });
    }

    [Test]
    public void DoubleDispose_DisposesUnderlyingOnlyOnce()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<DisposalCounter>();

        var owned = unity.Resolve<Owned<DisposalCounter>>();
        var counter = owned.Value;

        owned.Dispose();
        owned.Dispose();

        // HierarchicalLifetimeManager should only dispose once
        Assert.That(counter.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void ValueProperty_ReturnsSameInstanceEveryTime()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        using var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSame = ReferenceEquals(afOwned.Value, afOwned.Value);

        using var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uSame = ReferenceEquals(uOwned.Value, uOwned.Value);

        Assert.That(afSame, Is.True);
        Assert.That(uSame, Is.EqualTo(afSame));
    }

    [Test]
    public void SingletonDependency_SurvivesOwnedDisposal()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<ServiceWithSingletonDependency>().As<IServiceWithSingletonDependency>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithSingletonDependency, ServiceWithSingletonDependency>();

        var afSingleton = (TrackedService)autofac.Resolve<ITrackedService>();
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithSingletonDependency>>();
        var afService = (ServiceWithSingletonDependency)afOwned.Value;
        var afTransientDep = (Dependency)afService.TransientDep;
        afOwned.Dispose();

        var uSingleton = (TrackedService)unity.Resolve<ITrackedService>();
        var uOwned = unity.Resolve<Owned<IServiceWithSingletonDependency>>();
        var uService = (ServiceWithSingletonDependency)uOwned.Value;
        var uTransientDep = (Dependency)uService.TransientDep;
        uOwned.Dispose();

        // Service and transient dependency should be disposed
        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        Assert.That(afTransientDep.IsDisposed, Is.True);
        Assert.That(uTransientDep.IsDisposed, Is.EqualTo(afTransientDep.IsDisposed));

        // Singleton dependency must survive
        Assert.That(afSingleton.IsDisposed, Is.False);
        Assert.That(uSingleton.IsDisposed, Is.EqualTo(afSingleton.IsDisposed));
    }

    [Test]
    public void ConcurrentResolves_ProduceIndependentInstances()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        const int count = 20;
        var afResults = new Autofac.Features.OwnedInstances.Owned<ITrackedService>[count];
        var uResults = new Owned<ITrackedService>[count];

        Parallel.For(0, count, i =>
        {
            afResults[i] = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        });

        Parallel.For(0, count, i =>
        {
            uResults[i] = unity.Resolve<Owned<ITrackedService>>();
        });

        var afIds = afResults.Select(o => o.Value.Id).Distinct().Count();
        var uIds = uResults.Select(o => o.Value.Id).Distinct().Count();

        // All instances should be unique
        Assert.That(afIds, Is.EqualTo(count));
        Assert.That(uIds, Is.EqualTo(count));

        // Dispose one, others unaffected
        var afFirst = (TrackedService)afResults[0].Value;
        var afSecond = (TrackedService)afResults[1].Value;
        afResults[0].Dispose();

        var uFirst = (TrackedService)uResults[0].Value;
        var uSecond = (TrackedService)uResults[1].Value;
        uResults[0].Dispose();

        Assert.That(afFirst.IsDisposed, Is.True);
        Assert.That(afSecond.IsDisposed, Is.False);
        Assert.That(uFirst.IsDisposed, Is.EqualTo(afFirst.IsDisposed));
        Assert.That(uSecond.IsDisposed, Is.EqualTo(afSecond.IsDisposed));

        foreach (var o in afResults.Skip(1)) o.Dispose();
        foreach (var o in uResults.Skip(1)) o.Dispose();
    }

    [Test]
    public void FuncOwned_ZeroParams_Works()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afFactory = autofac.Resolve<Func<Autofac.Features.OwnedInstances.Owned<ITrackedService>>>();
        var afOwned = afFactory();
        var afService = (TrackedService)afOwned.Value;
        afOwned.Dispose();

        var uFactory = unity.Resolve<Func<Owned<ITrackedService>>>();
        var uOwned = uFactory();
        var uService = (TrackedService)uOwned.Value;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void FuncOwned_ZeroParams_EachCallProducesNewInstance()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afFactory = autofac.Resolve<Func<Autofac.Features.OwnedInstances.Owned<ITrackedService>>>();
        using var afOwned1 = afFactory();
        using var afOwned2 = afFactory();
        var afDifferent = afOwned1.Value.Id != afOwned2.Value.Id;

        var uFactory = unity.Resolve<Func<Owned<ITrackedService>>>();
        using var uOwned1 = uFactory();
        using var uOwned2 = uFactory();
        var uDifferent = uOwned1.Value.Id != uOwned2.Value.Id;

        Assert.That(afDifferent, Is.True);
        Assert.That(uDifferent, Is.EqualTo(afDifferent));
    }

    [Test]
    public void FuncOwned_OneParam_Works()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Greeter>().As<IGreeter>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IGreeter, Greeter>();
        unity.RegisterFactory<Func<string, Owned<IGreeter>>>(c =>
            (object)new Func<string, Owned<IGreeter>>(name => c.Resolve<Owned<IGreeter>>(
                new DependencyOverride<string>(name))));

        var afFactory = autofac.Resolve<Func<string, Autofac.Features.OwnedInstances.Owned<IGreeter>>>();
        var afOwned = afFactory("Alice");
        var afGreeter = (Greeter)afOwned.Value;
        afOwned.Dispose();

        var uFactory = unity.Resolve<Func<string, Owned<IGreeter>>>();
        var uOwned = uFactory("Alice");
        var uGreeter = (Greeter)uOwned.Value;
        uOwned.Dispose();

        Assert.That(afGreeter.Name, Is.EqualTo("Alice"));
        Assert.That(uGreeter.Name, Is.EqualTo(afGreeter.Name));
        Assert.That(afGreeter.IsDisposed, Is.True);
        Assert.That(uGreeter.IsDisposed, Is.EqualTo(afGreeter.IsDisposed));
    }

    [Test]
    public void Dispose_CascadesToDeepDependencyChain()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<DeepRoot>().As<IDeepRoot>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IDeepRoot, DeepRoot>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IDeepRoot>>();
        var afRoot = (DeepRoot)afOwned.Value;
        var afDep = (Dependency)afRoot.Dependency;
        afOwned.Dispose();

        var uOwned = unity.Resolve<Owned<IDeepRoot>>();
        var uRoot = (DeepRoot)uOwned.Value;
        var uDep = (Dependency)uRoot.Dependency;
        uOwned.Dispose();

        Assert.That(afRoot.IsDisposed, Is.True);
        Assert.That(afDep.IsDisposed, Is.True);
        Assert.That(uRoot.IsDisposed, Is.EqualTo(afRoot.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void SingletonDependency_SharedAcrossMultipleOwnedScopes()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<ServiceWithSingletonDependency>().As<IServiceWithSingletonDependency>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithSingletonDependency, ServiceWithSingletonDependency>();

        var afOwned1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithSingletonDependency>>();
        var afOwned2 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithSingletonDependency>>();
        var afSingleton1 = (TrackedService)afOwned1.Value.SingletonDep;
        var afSingleton2 = (TrackedService)afOwned2.Value.SingletonDep;
        var afSame = ReferenceEquals(afSingleton1, afSingleton2);
        afOwned1.Dispose();

        var uOwned1 = unity.Resolve<Owned<IServiceWithSingletonDependency>>();
        var uOwned2 = unity.Resolve<Owned<IServiceWithSingletonDependency>>();
        var uSingleton1 = (TrackedService)uOwned1.Value.SingletonDep;
        var uSingleton2 = (TrackedService)uOwned2.Value.SingletonDep;
        var uSame = ReferenceEquals(uSingleton1, uSingleton2);
        uOwned1.Dispose();

        Assert.That(afSame, Is.True);
        Assert.That(uSame, Is.EqualTo(afSame));
        Assert.That(afSingleton1.IsDisposed, Is.False);
        Assert.That(uSingleton1.IsDisposed, Is.EqualTo(afSingleton1.IsDisposed));

        afOwned2.Dispose();
        uOwned2.Dispose();

        Assert.That(afSingleton2.IsDisposed, Is.False);
        Assert.That(uSingleton2.IsDisposed, Is.EqualTo(afSingleton2.IsDisposed));
    }

    [Test]
    public void Owned_NonDisposableValue_WorksWithoutError()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<NonDisposableService>().As<INonDisposableService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<INonDisposableService, NonDisposableService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<INonDisposableService>>();
        var afValue = afOwned.Value;

        var uOwned = unity.Resolve<Owned<INonDisposableService>>();
        var uValue = uOwned.Value;

        Assert.That(afValue, Is.Not.Null);
        Assert.That(uValue, Is.Not.Null);

        Assert.DoesNotThrow(() => afOwned.Dispose());
        Assert.DoesNotThrow(() => uOwned.Dispose());
    }

    [Test]
    public void Dispose_NonDisposableMiddle_StillDisposesLeafDependency()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<NonDisposableMiddle>().As<INonDisposableMiddle>();
        autofacBuilder.RegisterType<ServiceWithNonDisposableMiddle>().As<IServiceWithNonDisposableMiddle>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<INonDisposableMiddle, NonDisposableMiddle>();
        unity.RegisterType<IServiceWithNonDisposableMiddle, ServiceWithNonDisposableMiddle>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithNonDisposableMiddle>>();
        var afService = (ServiceWithNonDisposableMiddle)afOwned.Value;
        var afDep = (Dependency)afService.Middle.Dependency;
        afOwned.Dispose();

        var uOwned = unity.Resolve<Owned<IServiceWithNonDisposableMiddle>>();
        var uService = (ServiceWithNonDisposableMiddle)uOwned.Value;
        var uDep = (Dependency)uService.Middle.Dependency;
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(afDep.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void Singleton_StillResolvableAfterOwnedDisposal()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afBefore = afOwned.Value.Id;
        afOwned.Dispose();
        var afAfter = autofac.Resolve<ITrackedService>();

        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uBefore = uOwned.Value.Id;
        uOwned.Dispose();
        var uAfter = unity.Resolve<ITrackedService>();

        Assert.That(afAfter.Id, Is.EqualTo(afBefore));
        Assert.That(uAfter.Id, Is.EqualTo(uBefore));
        Assert.That(((TrackedService)afAfter).IsDisposed, Is.False);
        Assert.That(((TrackedService)uAfter).IsDisposed, Is.EqualTo(((TrackedService)afAfter).IsDisposed));
    }

    [Test]
    public void NamedRegistration_ResolvesCorrectly()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<NamedServiceA>().Keyed<INamedService>("a");
        autofacBuilder.RegisterType<NamedServiceB>().Keyed<INamedService>("b");
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<INamedService, NamedServiceA>("a");
        unity.RegisterType<INamedService, NamedServiceB>("b");

        var afA = autofac.ResolveKeyed<Autofac.Features.OwnedInstances.Owned<INamedService>>("a");
        var afB = autofac.ResolveKeyed<Autofac.Features.OwnedInstances.Owned<INamedService>>("b");

        var uA = unity.Resolve<Owned<INamedService>>("a");
        var uB = unity.Resolve<Owned<INamedService>>("b");

        Assert.That(afA.Value.Label, Is.EqualTo("A"));
        Assert.That(afB.Value.Label, Is.EqualTo("B"));
        Assert.That(uA.Value.Label, Is.EqualTo(afA.Value.Label));
        Assert.That(uB.Value.Label, Is.EqualTo(afB.Value.Label));

        var afServiceA = (NamedServiceA)afA.Value;
        var uServiceA = (NamedServiceA)uA.Value;
        afA.Dispose();
        uA.Dispose();

        Assert.That(afServiceA.IsDisposed, Is.True);
        Assert.That(uServiceA.IsDisposed, Is.EqualTo(afServiceA.IsDisposed));

        // B should be unaffected
        Assert.That(((NamedServiceB)afB.Value).IsDisposed, Is.False);
        Assert.That(((NamedServiceB)uB.Value).IsDisposed, Is.EqualTo(((NamedServiceB)afB.Value).IsDisposed));

        afB.Dispose();
        uB.Dispose();
    }

    [Test]
    public void TransientWithoutOwned_NotDisposedByContainer()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<ServiceWithDependency>().As<IServiceWithDependency>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        var afService1 = (TrackedService)autofac.Resolve<ITrackedService>();
        var afService2 = (TrackedService)autofac.Resolve<ITrackedService>();
        var afSvcWithDep = (ServiceWithDependency)autofac.Resolve<IServiceWithDependency>();
        var afDep = (Dependency)afSvcWithDep.Dependency;

        var uService1 = (TrackedService)unity.Resolve<ITrackedService>();
        var uService2 = (TrackedService)unity.Resolve<ITrackedService>();
        var uSvcWithDep = (ServiceWithDependency)unity.Resolve<IServiceWithDependency>();
        var uDep = (Dependency)uSvcWithDep.Dependency;

        // Transients resolved without Owned should not be disposed
        Assert.That(afService1.IsDisposed, Is.False);
        Assert.That(afService2.IsDisposed, Is.False);
        Assert.That(afSvcWithDep.IsDisposed, Is.False);
        Assert.That(afDep.IsDisposed, Is.False);

        Assert.That(uService1.IsDisposed, Is.EqualTo(afService1.IsDisposed));
        Assert.That(uService2.IsDisposed, Is.EqualTo(afService2.IsDisposed));
        Assert.That(uSvcWithDep.IsDisposed, Is.EqualTo(afSvcWithDep.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void TransientWithoutOwned_NotAffectedByOwnedDisposal()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        // Resolve transients directly (no Owned)
        var afDirect = (TrackedService)autofac.Resolve<ITrackedService>();
        var uDirect = (TrackedService)unity.Resolve<ITrackedService>();

        // Resolve and dispose an Owned
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        afOwned.Dispose();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        uOwned.Dispose();

        // Direct transients must be unaffected
        Assert.That(afDirect.IsDisposed, Is.False);
        Assert.That(uDirect.IsDisposed, Is.EqualTo(afDirect.IsDisposed));
    }
}
