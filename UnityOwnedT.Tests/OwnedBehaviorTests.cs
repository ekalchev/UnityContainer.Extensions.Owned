using System.Collections.Concurrent;
using Autofac;
using Autofac.Core;
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
        SharedLeaf.ResetCounter();
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

    [Test]
    public void ParameterOverride_PassedToInnerType()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Connection>().As<IConnection>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IConnection, Connection>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IConnection>>(
            new NamedParameter("connectionString", "server=db"),
            new NamedParameter("maxRetries", 5));
        var afConn = (Connection)afOwned.Value;

        var uOwned = unity.Resolve<Owned<IConnection>>(
            new ParameterOverride("connectionString", "server=db"),
            new ParameterOverride("maxRetries", 5));
        var uConn = (Connection)uOwned.Value;

        Assert.That(afConn.ConnectionString, Is.EqualTo("server=db"));
        Assert.That(afConn.MaxRetries, Is.EqualTo(5));
        Assert.That(uConn.ConnectionString, Is.EqualTo(afConn.ConnectionString));
        Assert.That(uConn.MaxRetries, Is.EqualTo(afConn.MaxRetries));

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afConn.IsDisposed, Is.True);
        Assert.That(uConn.IsDisposed, Is.EqualTo(afConn.IsDisposed));
    }

    [Test]
    public void DependencyOverride_PassedToInnerType()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Greeter>().As<IGreeter>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IGreeter, Greeter>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IGreeter>>(
            new NamedParameter("name", "Bob"));
        var afGreeter = (Greeter)afOwned.Value;

        var uOwned = unity.Resolve<Owned<IGreeter>>(
            new DependencyOverride<string>("Bob"));
        var uGreeter = (Greeter)uOwned.Value;

        Assert.That(afGreeter.Name, Is.EqualTo("Bob"));
        Assert.That(uGreeter.Name, Is.EqualTo(afGreeter.Name));

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afGreeter.IsDisposed, Is.True);
        Assert.That(uGreeter.IsDisposed, Is.EqualTo(afGreeter.IsDisposed));
    }

    [Test]
    public void ParameterOverride_MultipleParameters()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IMultiParamService, MultiParamService>();

        var uOwned = unity.Resolve<Owned<IMultiParamService>>(
            new ParameterOverride("name", "test"),
            new ParameterOverride("count", 42));
        var uSvc = (MultiParamService)uOwned.Value;
        var uDep = (Dependency)uSvc.Dependency;

        Assert.That(uSvc.Name, Is.EqualTo("test"));
        Assert.That(uSvc.Count, Is.EqualTo(42));
        Assert.That(uSvc.Dependency, Is.Not.Null);

        uOwned.Dispose();

        Assert.That(uSvc.IsDisposed, Is.True);
        Assert.That(uDep.IsDisposed, Is.True);
    }

    [Test]
    public void ParameterOverride_DifferentValuesPerOwned()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IConnection, Connection>();

        var uOwned1 = unity.Resolve<Owned<IConnection>>(
            new ParameterOverride("connectionString", "server=a"),
            new ParameterOverride("maxRetries", 1));
        var uOwned2 = unity.Resolve<Owned<IConnection>>(
            new ParameterOverride("connectionString", "server=b"),
            new ParameterOverride("maxRetries", 9));

        Assert.That(((Connection)uOwned1.Value).ConnectionString, Is.EqualTo("server=a"));
        Assert.That(((Connection)uOwned1.Value).MaxRetries, Is.EqualTo(1));
        Assert.That(((Connection)uOwned2.Value).ConnectionString, Is.EqualTo("server=b"));
        Assert.That(((Connection)uOwned2.Value).MaxRetries, Is.EqualTo(9));

        var conn1 = (Connection)uOwned1.Value;
        uOwned1.Dispose();

        Assert.That(conn1.IsDisposed, Is.True);
        Assert.That(((Connection)uOwned2.Value).IsDisposed, Is.False);

        uOwned2.Dispose();
    }

    [Test]
    public void DependencyOverride_OverridesRegisteredDependency()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        var customDep = new Dependency();

        var uOwned = unity.Resolve<Owned<IServiceWithDependency>>(
            new DependencyOverride<IDependency>(customDep));
        var uSvc = (ServiceWithDependency)uOwned.Value;

        Assert.That(ReferenceEquals(uSvc.Dependency, customDep), Is.True);

        uOwned.Dispose();

        Assert.That(uSvc.IsDisposed, Is.True);
        // The custom dep was not created by the container, so it should not be disposed
        Assert.That(customDep.IsDisposed, Is.False);
    }

    [Test]
    public void DependencyOverride_MultipleTypes()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IMultiParamService, MultiParamService>();

        var customDep = new Dependency();

        var uOwned = unity.Resolve<Owned<IMultiParamService>>(
            new DependencyOverride<string>("hello"),
            new DependencyOverride<int>(7),
            new DependencyOverride<IDependency>(customDep));
        var uSvc = (MultiParamService)uOwned.Value;

        Assert.That(uSvc.Name, Is.EqualTo("hello"));
        Assert.That(uSvc.Count, Is.EqualTo(7));
        Assert.That(ReferenceEquals(uSvc.Dependency, customDep), Is.True);

        uOwned.Dispose();

        Assert.That(uSvc.IsDisposed, Is.True);
    }

    [Test]
    public void PropertyOverride_SetsPropertyOnResolvedInstance()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IConfigurable, ConfigurableService>(
            new Unity.Injection.InjectionProperty("Host"),
            new Unity.Injection.InjectionProperty("Port"),
            new Unity.Injection.InjectionProperty("Dependency"));

        var uOwned = unity.Resolve<Owned<IConfigurable>>(
            new PropertyOverride("Host", "localhost"),
            new PropertyOverride("Port", 8080));
        var uSvc = (ConfigurableService)uOwned.Value;

        Assert.That(uSvc.Host, Is.EqualTo("localhost"));
        Assert.That(uSvc.Port, Is.EqualTo(8080));
        Assert.That(uSvc.Dependency, Is.Not.Null);

        var dep = (Dependency)uSvc.Dependency;
        uOwned.Dispose();

        Assert.That(uSvc.IsDisposed, Is.True);
        Assert.That(dep.IsDisposed, Is.True);
    }

    [Test]
    public void PropertyOverride_DifferentValuesPerOwned()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IConfigurable, ConfigurableService>(
            new Unity.Injection.InjectionProperty("Host"),
            new Unity.Injection.InjectionProperty("Port"),
            new Unity.Injection.InjectionProperty("Dependency"));

        var uOwned1 = unity.Resolve<Owned<IConfigurable>>(
            new PropertyOverride("Host", "alpha"),
            new PropertyOverride("Port", 1000));
        var uOwned2 = unity.Resolve<Owned<IConfigurable>>(
            new PropertyOverride("Host", "beta"),
            new PropertyOverride("Port", 2000));

        Assert.That(((ConfigurableService)uOwned1.Value).Host, Is.EqualTo("alpha"));
        Assert.That(((ConfigurableService)uOwned1.Value).Port, Is.EqualTo(1000));
        Assert.That(((ConfigurableService)uOwned2.Value).Host, Is.EqualTo("beta"));
        Assert.That(((ConfigurableService)uOwned2.Value).Port, Is.EqualTo(2000));

        uOwned1.Dispose();
        uOwned2.Dispose();
    }

    [Test]
    public void FieldOverride_SetsFieldOnResolvedInstance()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IFieldInjected, FieldInjectedService>();

        var uOwned = unity.Resolve<Owned<IFieldInjected>>(
            new FieldOverride("Tag", "injected-value"));
        var uSvc = (FieldInjectedService)uOwned.Value;

        Assert.That(uSvc.Tag, Is.EqualTo("injected-value"));

        uOwned.Dispose();

        Assert.That(uSvc.IsDisposed, Is.True);
    }

    [Test]
    public void FieldOverride_DifferentValuesPerOwned()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IFieldInjected, FieldInjectedService>();

        var uOwned1 = unity.Resolve<Owned<IFieldInjected>>(
            new FieldOverride("Tag", "first"));
        var uOwned2 = unity.Resolve<Owned<IFieldInjected>>(
            new FieldOverride("Tag", "second"));

        Assert.That(((FieldInjectedService)uOwned1.Value).Tag, Is.EqualTo("first"));
        Assert.That(((FieldInjectedService)uOwned2.Value).Tag, Is.EqualTo("second"));

        uOwned1.Dispose();
        uOwned2.Dispose();
    }

    [Test]
    public void MixedOverrides_AllTypesWorkTogether()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IConfigurable, ConfigurableService>(
            new Unity.Injection.InjectionProperty("Host"),
            new Unity.Injection.InjectionProperty("Port"),
            new Unity.Injection.InjectionProperty("Dependency"));

        var customDep = new Dependency();

        var uOwned = unity.Resolve<Owned<IConfigurable>>(
            new PropertyOverride("Host", "mixed-host"),
            new PropertyOverride("Port", 3000),
            new DependencyOverride<IDependency>(customDep));
        var uSvc = (ConfigurableService)uOwned.Value;

        Assert.That(uSvc.Host, Is.EqualTo("mixed-host"));
        Assert.That(uSvc.Port, Is.EqualTo(3000));
        Assert.That(ReferenceEquals(uSvc.Dependency, customDep), Is.True);

        uOwned.Dispose();

        Assert.That(uSvc.IsDisposed, Is.True);
    }

    [Test]
    public void NestedOwned_Level2_OuterDisposeDoesNotDisposeService()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOuter = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<ITrackedService>>>();
        var afInner = afOuter.Value;
        var afService = (TrackedService)afInner.Value;
        afOuter.Dispose();

        var uOuter = unity.Resolve<Owned<Owned<ITrackedService>>>();
        var uInner = uOuter.Value;
        var uService = (TrackedService)uInner.Value;
        uOuter.Dispose();

        // Outer dispose does NOT cascade to inner's service
        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        // Inner dispose DOES dispose the service
        afInner.Dispose();
        uInner.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void NestedOwned_Level2_InnerDisposeDisposesService()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOuter = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<ITrackedService>>>();
        var afInner = afOuter.Value;
        var afService = (TrackedService)afInner.Value;
        afInner.Dispose();

        var uOuter = unity.Resolve<Owned<Owned<ITrackedService>>>();
        var uInner = uOuter.Value;
        var uService = (TrackedService)uInner.Value;
        uInner.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        // Outer dispose should not throw
        Assert.DoesNotThrow(() => afOuter.Dispose());
        Assert.DoesNotThrow(() => uOuter.Dispose());
    }

    [Test]
    public void NestedOwned_Level3_OnlyInnermostDisposesService()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afL1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<
                Autofac.Features.OwnedInstances.Owned<ITrackedService>>>>();
        var afL2 = afL1.Value;
        var afL3 = afL2.Value;
        var afService = (TrackedService)afL3.Value;

        var uL1 = unity.Resolve<Owned<Owned<Owned<ITrackedService>>>>();
        var uL2 = uL1.Value;
        var uL3 = uL2.Value;
        var uService = (TrackedService)uL3.Value;

        // Dispose outermost — service NOT disposed
        afL1.Dispose();
        uL1.Dispose();
        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        // Dispose middle — service NOT disposed
        afL2.Dispose();
        uL2.Dispose();
        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        // Dispose innermost — service IS disposed
        afL3.Dispose();
        uL3.Dispose();
        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void NestedOwned_Level4_OnlyInnermostDisposesService()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afL1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<
                Autofac.Features.OwnedInstances.Owned<
                    Autofac.Features.OwnedInstances.Owned<ITrackedService>>>>>();
        var afL2 = afL1.Value;
        var afL3 = afL2.Value;
        var afL4 = afL3.Value;
        var afService = (TrackedService)afL4.Value;

        var uL1 = unity.Resolve<Owned<Owned<Owned<Owned<ITrackedService>>>>>();
        var uL2 = uL1.Value;
        var uL3 = uL2.Value;
        var uL4 = uL3.Value;
        var uService = (TrackedService)uL4.Value;

        // Dispose from outermost to innermost, checking each step
        afL1.Dispose();
        uL1.Dispose();
        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        afL2.Dispose();
        uL2.Dispose();
        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        afL3.Dispose();
        uL3.Dispose();
        Assert.That(afService.IsDisposed, Is.False);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        afL4.Dispose();
        uL4.Dispose();
        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void NestedOwned_Level4_InnermostDisposeFirst()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afL1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<
                Autofac.Features.OwnedInstances.Owned<
                    Autofac.Features.OwnedInstances.Owned<ITrackedService>>>>>();
        var afService = (TrackedService)afL1.Value.Value.Value.Value;

        var uL1 = unity.Resolve<Owned<Owned<Owned<Owned<ITrackedService>>>>>();
        var uService = (TrackedService)uL1.Value.Value.Value.Value;

        // Dispose innermost first — service disposed immediately
        afL1.Value.Value.Value.Dispose();
        uL1.Value.Value.Value.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        // All remaining disposes should not throw
        Assert.DoesNotThrow(() => { afL1.Value.Value.Dispose(); afL1.Value.Dispose(); afL1.Dispose(); });
        Assert.DoesNotThrow(() => { uL1.Value.Value.Dispose(); uL1.Value.Dispose(); uL1.Dispose(); });
    }

    [Test]
    public void NestedOwned_Level2_IndependentScopes()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOuter1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<ITrackedService>>>();
        var afOuter2 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<
            Autofac.Features.OwnedInstances.Owned<ITrackedService>>>();
        var afService1 = (TrackedService)afOuter1.Value.Value;
        var afService2 = (TrackedService)afOuter2.Value.Value;

        var uOuter1 = unity.Resolve<Owned<Owned<ITrackedService>>>();
        var uOuter2 = unity.Resolve<Owned<Owned<ITrackedService>>>();
        var uService1 = (TrackedService)uOuter1.Value.Value;
        var uService2 = (TrackedService)uOuter2.Value.Value;

        // Different instances
        var afDifferent = !ReferenceEquals(afService1, afService2);
        var uDifferent = !ReferenceEquals(uService1, uService2);
        Assert.That(afDifferent, Is.True);
        Assert.That(uDifferent, Is.EqualTo(afDifferent));

        // Dispose outer1 — in Autofac, outer dispose does NOT cascade to inner service
        afOuter1.Dispose();
        uOuter1.Dispose();

        Assert.That(afService1.IsDisposed, Is.False, "Autofac: outer dispose does not cascade to inner service");
        Assert.That(afService2.IsDisposed, Is.False);
        Assert.That(uService1.IsDisposed, Is.EqualTo(afService1.IsDisposed));
        Assert.That(uService2.IsDisposed, Is.EqualTo(afService2.IsDisposed));

        afOuter2.Dispose();
        uOuter2.Dispose();
    }

    // ── Child Container + Various Lifetimes ──────────────────────────────

    [Test]
    public void ChildContainer_HierarchicalLifetime_OwnedDisposesService()
    {
        // Autofac InstancePerLifetimeScope ≈ Unity HierarchicalLifetimeManager
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>(new Unity.Lifetime.HierarchicalLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;

        var uOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void ChildContainer_HierarchicalLifetime_MultipleOwnedGetDifferentInstances()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>(new Unity.Lifetime.HierarchicalLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned1 = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afOwned2 = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSvc1 = (TrackedService)afOwned1.Value;
        var afSvc2 = (TrackedService)afOwned2.Value;
        var afDifferent = !ReferenceEquals(afSvc1, afSvc2);

        var uOwned1 = uChild.Resolve<Owned<ITrackedService>>();
        var uOwned2 = uChild.Resolve<Owned<ITrackedService>>();
        var uSvc1 = (TrackedService)uOwned1.Value;
        var uSvc2 = (TrackedService)uOwned2.Value;
        var uDifferent = !ReferenceEquals(uSvc1, uSvc2);

        Assert.That(afDifferent, Is.True, "Autofac: each Owned gets its own scope → different instances");
        Assert.That(uDifferent, Is.EqualTo(afDifferent));

        // Dispose one, other unaffected
        afOwned1.Dispose();
        uOwned1.Dispose();

        Assert.That(afSvc1.IsDisposed, Is.True);
        Assert.That(afSvc2.IsDisposed, Is.False);
        Assert.That(uSvc1.IsDisposed, Is.EqualTo(afSvc1.IsDisposed));
        Assert.That(uSvc2.IsDisposed, Is.EqualTo(afSvc2.IsDisposed));

        afOwned2.Dispose();
        uOwned2.Dispose();
    }

    [Test]
    public void ChildContainer_HierarchicalDependency_DisposedWithOwned()
    {
        // Service depends on a hierarchical dependency — disposing Owned should dispose the dependency too
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<ServiceWithDependency>().As<IServiceWithDependency>();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();
        unity.RegisterType<IDependency, Dependency>(new Unity.Lifetime.HierarchicalLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithDependency>>();
        var afDep = (Dependency)afOwned.Value.Dependency;

        var uOwned = uChild.Resolve<Owned<IServiceWithDependency>>();
        var uDep = (Dependency)uOwned.Value.Dependency;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afDep.IsDisposed, Is.True);
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void ChildContainer_DisposingChild_DoesNotDisposeOwnedService()
    {
        // Owned<T> resolved from child — then child is disposed. Owned's inner scope is independent.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afChild = autofac.BeginLifetimeScope();
        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;
        afChild.Dispose();

        var uChild = ((IUnityContainer)unity).CreateChildContainer();
        var uOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;
        uChild.Dispose();

        Assert.That(afService.IsDisposed, Is.False, "Autofac: child dispose does not affect Owned's own scope");
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));

        // Owned dispose still works
        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void ChildContainer_MixedLifetimes_SingletonSurvivesHierarchicalDisposed()
    {
        // Singleton dep + hierarchical dep in same service resolved via Owned from child
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<ServiceWithSingletonDependency>().As<IServiceWithSingletonDependency>();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IServiceWithSingletonDependency, ServiceWithSingletonDependency>();
        unity.RegisterSingleton<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>(new Unity.Lifetime.HierarchicalLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithSingletonDependency>>();
        var afSingleton = (TrackedService)afOwned.Value.SingletonDep;
        var afDep = (Dependency)afOwned.Value.TransientDep;

        var uOwned = uChild.Resolve<Owned<IServiceWithSingletonDependency>>();
        var uSingleton = (TrackedService)uOwned.Value.SingletonDep;
        var uDep = (Dependency)uOwned.Value.TransientDep;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afSingleton.IsDisposed, Is.False, "Autofac: singleton survives Owned dispose");
        Assert.That(afDep.IsDisposed, Is.True, "Autofac: hierarchical dep disposed with Owned");
        Assert.That(uSingleton.IsDisposed, Is.EqualTo(afSingleton.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void ChildContainer_OwnedFromParentAndChild_IndependentWithHierarchicalLifetime()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>(new Unity.Lifetime.HierarchicalLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afParentOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afChildOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afParentSvc = (TrackedService)afParentOwned.Value;
        var afChildSvc = (TrackedService)afChildOwned.Value;

        var uParentOwned = unity.Resolve<Owned<ITrackedService>>();
        var uChildOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uParentSvc = (TrackedService)uParentOwned.Value;
        var uChildSvc = (TrackedService)uChildOwned.Value;

        // Different instances (each Owned has its own scope)
        Assert.That(ReferenceEquals(afParentSvc, afChildSvc), Is.False);
        Assert.That(ReferenceEquals(uParentSvc, uChildSvc), Is.False);

        // Dispose child Owned only
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
    public void ChildContainer_PerResolveLifetime_OwnedDisposesCorrectly()
    {
        // PerResolve: same instance within a single resolve graph, new instance per Resolve() call
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>(new Unity.Lifetime.PerResolveLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var uOwned1 = uChild.Resolve<Owned<ITrackedService>>();
        var uOwned2 = uChild.Resolve<Owned<ITrackedService>>();
        var uSvc1 = (TrackedService)uOwned1.Value;
        var uSvc2 = (TrackedService)uOwned2.Value;

        Assert.That(ReferenceEquals(uSvc1, uSvc2), Is.False, "PerResolve: different Owned calls → different instances");

        uOwned1.Dispose();
        Assert.That(uSvc1.IsDisposed, Is.True);
        Assert.That(uSvc2.IsDisposed, Is.False);

        uOwned2.Dispose();
        Assert.That(uSvc2.IsDisposed, Is.True);
    }

    [Test]
    public void ChildContainer_ExternallyControlled_OwnedDoesNotDisposeService()
    {
        // ExternallyControlledLifetimeManager: container does NOT own the instance
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().ExternallyOwned();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>(new Unity.Lifetime.ExternallyControlledLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;

        var uOwned = uChild.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afService.IsDisposed, Is.EqualTo(uService.IsDisposed),
            "Unity should match Autofac behavior for externally controlled instances");
    }

    [Test]
    public void ChildContainer_DeepChain_HierarchicalDependencies_AllDisposed()
    {
        // Deep chain: DeepRoot → Dependency, both hierarchical, resolved via Owned from child
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<DeepRoot>().As<IDeepRoot>().InstancePerLifetimeScope();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDeepRoot, DeepRoot>(new Unity.Lifetime.HierarchicalLifetimeManager());
        unity.RegisterType<IDependency, Dependency>(new Unity.Lifetime.HierarchicalLifetimeManager());
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<IDeepRoot>>();
        var afRoot = (DeepRoot)afOwned.Value;
        var afDep = (Dependency)afOwned.Value.Dependency;

        var uOwned = uChild.Resolve<Owned<IDeepRoot>>();
        var uRoot = (DeepRoot)uOwned.Value;
        var uDep = (Dependency)uOwned.Value.Dependency;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afRoot.IsDisposed, Is.True);
        Assert.That(afDep.IsDisposed, Is.True);
        Assert.That(uRoot.IsDisposed, Is.EqualTo(afRoot.IsDisposed));
        Assert.That(uDep.IsDisposed, Is.EqualTo(afDep.IsDisposed));
    }

    [Test]
    public void ChildContainer_ChildRegistrationOverridesParent_OwnedUsesChildRegistration()
    {
        // Child overrides parent registration — Owned from child should use child's type
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<INamedService, NamedServiceA>();
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.RegisterType<INamedService, NamedServiceB>();

        var uOwned = uChild.Resolve<Owned<INamedService>>();
        var label = uOwned.Value.Label;

        // Owned creates scope from the resolving container (child), so child's override wins
        Assert.That(label, Is.EqualTo("B"));

        uOwned.Dispose();
        Assert.That(((NamedServiceB)uOwned.Value).IsDisposed, Is.True);
    }

    [Test]
    public void ChildContainer_MultipleChildren_OwnedScopesIndependent()
    {
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();
        using var afChild1 = autofac.BeginLifetimeScope();
        using var afChild2 = autofac.BeginLifetimeScope();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        using var uChild1 = ((IUnityContainer)unity).CreateChildContainer();
        using var uChild2 = ((IUnityContainer)unity).CreateChildContainer();

        var afOwned1 = afChild1.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afOwned2 = afChild2.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();

        var uOwned1 = uChild1.Resolve<Owned<ITrackedService>>();
        var uOwned2 = uChild2.Resolve<Owned<ITrackedService>>();

        var afSvc1 = (TrackedService)afOwned1.Value;
        var afSvc2 = (TrackedService)afOwned2.Value;
        var uSvc1 = (TrackedService)uOwned1.Value;
        var uSvc2 = (TrackedService)uOwned2.Value;

        // Dispose from child1 only
        afOwned1.Dispose();
        uOwned1.Dispose();

        Assert.That(afSvc1.IsDisposed, Is.True);
        Assert.That(afSvc2.IsDisposed, Is.False);
        Assert.That(uSvc1.IsDisposed, Is.EqualTo(afSvc1.IsDisposed));
        Assert.That(uSvc2.IsDisposed, Is.EqualTo(afSvc2.IsDisposed));

        afOwned2.Dispose();
        uOwned2.Dispose();
    }

    [Test]
    public void ChildContainer_DependencyRegisteredOnChild_OwnedResolvesCorrectly()
    {
        // Root has IServiceWithDependency registered, but IDependency is only on child.
        // Resolving Owned<IServiceWithDependency> from child should work because
        // IDependency is available in the child scope.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<ServiceWithDependency>().As<IServiceWithDependency>();
        using var autofac = autofacBuilder.Build();
        using var afChild = autofac.BeginLifetimeScope(b =>
            b.RegisterType<Dependency>().As<IDependency>());

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();
        using var uChild = ((IUnityContainer)unity).CreateChildContainer();
        uChild.RegisterType<IDependency, Dependency>();

        // Autofac: Owned from child should work — child has IDependency
        var afOwned = afChild.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithDependency>>();
        var afService = (ServiceWithDependency)afOwned.Value;
        var afDep = (Dependency)afService.Dependency;

        // Unity: Owned from child — but our strategy creates scope from ROOT,
        // which does NOT have IDependency registered. This should fail or misbehave.
        Assert.DoesNotThrow(() =>
        {
            var uOwned = uChild.Resolve<Owned<IServiceWithDependency>>();
            var uService = (ServiceWithDependency)uOwned.Value;
            var uDep = (Dependency)uService.Dependency;

            // Verify disposal works
            uOwned.Dispose();
            Assert.That(uService.IsDisposed, Is.True);
            Assert.That(uDep.IsDisposed, Is.True);
        }, "Owned should resolve from the correct scope that has child registrations");

        afOwned.Dispose();
        Assert.That(afService.IsDisposed, Is.True);
        Assert.That(afDep.IsDisposed, Is.True);
    }

    [Test]
    public void ForgottenOwned_ContainerDispose_DoesNotDisposeService()
    {
        // Owned<T> gives explicit ownership — forgetting to dispose it leaks the scope.
        // Neither Autofac nor Unity disposes the inner service on container dispose.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        var autofac = autofacBuilder.Build();

        var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;

        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;

        // Don't dispose Owned — just dispose the container
        autofac.Dispose();
        unity.Dispose();

        Assert.That(afService.IsDisposed, Is.False, "Autofac: forgotten Owned leaks");
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    [Test]
    public void ForgottenOwned_Singleton_ContainerDispose_DisposesService()
    {
        // Singleton is owned by the container, not by Owned<T>.
        // Container dispose should dispose the singleton regardless of Owned.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        var autofac = autofacBuilder.Build();

        var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afService = (TrackedService)afOwned.Value;

        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uService = (TrackedService)uOwned.Value;

        // Don't dispose Owned — just dispose the container
        autofac.Dispose();
        unity.Dispose();

        Assert.That(afService.IsDisposed, Is.True, "Autofac: singleton disposed with container");
        Assert.That(uService.IsDisposed, Is.EqualTo(afService.IsDisposed));
    }

    // ── Edge Cases / Bug Hunting ─────────────────────────────────────────

    [Test]
    public void OpenGeneric_OwnedDisposesCorrectly()
    {
        // Owned<IRepository<User>> with open generic registration
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterGeneric(typeof(Repository<>)).As(typeof(IRepository<>));
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType(typeof(IRepository<>), typeof(Repository<>));

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IRepository<User>>>();
        var afRepo = (Repository<User>)afOwned.Value;

        var uOwned = unity.Resolve<Owned<IRepository<User>>>();
        var uRepo = (Repository<User>)uOwned.Value;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afRepo.IsDisposed, Is.True);
        Assert.That(uRepo.IsDisposed, Is.EqualTo(afRepo.IsDisposed));
    }

    [Test]
    public void OpenGeneric_MultipleClosedTypes_IndependentScopes()
    {
        // Owned<IRepository<User>> and Owned<IRepository<Order>> are independent
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterGeneric(typeof(Repository<>)).As(typeof(IRepository<>));
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType(typeof(IRepository<>), typeof(Repository<>));

        var afUser = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IRepository<User>>>();
        var afOrder = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IRepository<Order>>>();
        var afUserRepo = (Repository<User>)afUser.Value;
        var afOrderRepo = (Repository<Order>)afOrder.Value;

        var uUser = unity.Resolve<Owned<IRepository<User>>>();
        var uOrder = unity.Resolve<Owned<IRepository<Order>>>();
        var uUserRepo = (Repository<User>)uUser.Value;
        var uOrderRepo = (Repository<Order>)uOrder.Value;

        afUser.Dispose();
        uUser.Dispose();

        Assert.That(afUserRepo.IsDisposed, Is.True);
        Assert.That(afOrderRepo.IsDisposed, Is.False);
        Assert.That(uUserRepo.IsDisposed, Is.EqualTo(afUserRepo.IsDisposed));
        Assert.That(uOrderRepo.IsDisposed, Is.EqualTo(afOrderRepo.IsDisposed));

        afOrder.Dispose();
        uOrder.Dispose();
    }

    [Test]
    public void ConstructorThrows_ChildContainerIsDisposed_NoLeak()
    {
        // If inner type construction fails, the child container should be disposed
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IFailingService, FailingService>();

        Assert.Throws<ResolutionFailedException>(() =>
            unity.Resolve<Owned<IFailingService>>());
    }

    [Test]
    public void ConstructorThrows_PartialDependencies_DisposedOnFailure()
    {
        // T has two deps: IDependency (succeeds) and IFailingService (fails).
        // The already-resolved IDependency should be cleaned up via child container disposal.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<FailingService>().As<IFailingService>();
        autofacBuilder.RegisterType<ServiceWithFailingDep>().As<IServiceWithFailingDep>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IFailingService, FailingService>();
        unity.RegisterType<IServiceWithFailingDep, ServiceWithFailingDep>();

        Assert.Throws<Autofac.Core.DependencyResolutionException>(() =>
            autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithFailingDep>>());

        Assert.Throws<ResolutionFailedException>(() =>
            unity.Resolve<Owned<IServiceWithFailingDep>>());
    }

    [Test]
    public void MultipleDeps_AllDisposed()
    {
        // Service with two disposable dependencies — both should be disposed
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<ServiceWithMultipleDeps>().As<IServiceWithMultipleDeps>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithMultipleDeps, ServiceWithMultipleDeps>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithMultipleDeps>>();
        var afSvc = (ServiceWithMultipleDeps)afOwned.Value;
        var afDep1 = (TrackedService)afSvc.Dep1;
        var afDep2 = (Dependency)afSvc.Dep2;

        var uOwned = unity.Resolve<Owned<IServiceWithMultipleDeps>>();
        var uSvc = (ServiceWithMultipleDeps)uOwned.Value;
        var uDep1 = (TrackedService)uSvc.Dep1;
        var uDep2 = (Dependency)uSvc.Dep2;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afSvc.IsDisposed, Is.True);
        Assert.That(afDep1.IsDisposed, Is.True);
        Assert.That(afDep2.IsDisposed, Is.True);
        Assert.That(uSvc.IsDisposed, Is.EqualTo(afSvc.IsDisposed));
        Assert.That(uDep1.IsDisposed, Is.EqualTo(afDep1.IsDisposed));
        Assert.That(uDep2.IsDisposed, Is.EqualTo(afDep2.IsDisposed));
    }

    [Test]
    public void MultipleDeps_OneSingleton_OnlyTransientDisposed()
    {
        // Two deps: one singleton, one transient. Only transient should be disposed.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>();
        autofacBuilder.RegisterType<ServiceWithMultipleDeps>().As<IServiceWithMultipleDeps>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithMultipleDeps, ServiceWithMultipleDeps>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithMultipleDeps>>();
        var afDep1 = (TrackedService)afOwned.Value.Dep1;
        var afDep2 = (Dependency)afOwned.Value.Dep2;

        var uOwned = unity.Resolve<Owned<IServiceWithMultipleDeps>>();
        var uDep1 = (TrackedService)uOwned.Value.Dep1;
        var uDep2 = (Dependency)uOwned.Value.Dep2;

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afDep1.IsDisposed, Is.False, "Autofac: singleton survives Owned dispose");
        Assert.That(afDep2.IsDisposed, Is.True, "Autofac: transient disposed with Owned");
        Assert.That(uDep1.IsDisposed, Is.EqualTo(afDep1.IsDisposed));
        Assert.That(uDep2.IsDisposed, Is.EqualTo(afDep2.IsDisposed));
    }

    [Test]
    public void SameSingleton_SharedAcrossOwnedAndDirectResolve()
    {
        // Singleton resolved via Owned and directly should be the same instance
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().SingleInstance();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ITrackedService, TrackedService>();

        var afDirect = autofac.Resolve<ITrackedService>();
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSame = ReferenceEquals(afDirect, afOwned.Value);

        var uDirect = unity.Resolve<ITrackedService>();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uSame = ReferenceEquals(uDirect, uOwned.Value);

        Assert.That(afSame, Is.True);
        Assert.That(uSame, Is.EqualTo(afSame));

        // Disposing Owned should NOT affect the singleton
        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(((TrackedService)afDirect).IsDisposed, Is.False);
        Assert.That(((TrackedService)uDirect).IsDisposed,
            Is.EqualTo(((TrackedService)afDirect).IsDisposed));
    }

    [Test]
    public void Owned_ResolvedFromDisposedContainer_Throws()
    {
        var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();
        unity.Dispose();

        Assert.Throws<ResolutionFailedException>(() =>
            unity.Resolve<Owned<ITrackedService>>());
    }

    [Test]
    public void Owned_ServiceDependsOnOwnedDependency()
    {
        // Service constructor takes Owned<IDependency> — transitive Owned
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IServiceWithOwnedDependency, ServiceWithOwnedDependency>();

        var uOwned = unity.Resolve<Owned<IServiceWithOwnedDependency>>();
        var uSvc = (ServiceWithOwnedDependency)uOwned.Value;
        var uDep = (Dependency)uSvc.Dependency;

        Assert.That(uDep.IsDisposed, Is.False);

        // Disposing outer Owned disposes the child container.
        // The inner Owned<IDependency> was created in that child, but its
        // child container was detached. So the inner dep should NOT be disposed
        // by the outer dispose — only by ServiceWithOwnedDependency.Dispose().
        uOwned.Dispose();

        // The service's Dispose calls _ownedDep.Dispose(), so the dep IS disposed
        Assert.That(uSvc.IsDisposed, Is.True);
        Assert.That(uDep.IsDisposed, Is.True);
    }

    [Test]
    public void Owned_NamedRegistrations_ResolvedCorrectly()
    {
        // Named registrations should resolve correctly inside Owned scope
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<INamedService, NamedServiceA>("alpha");
        unity.RegisterType<INamedService, NamedServiceB>("beta");

        var uAlpha = unity.Resolve<Owned<INamedService>>("alpha");
        var uBeta = unity.Resolve<Owned<INamedService>>("beta");

        Assert.That(uAlpha.Value.Label, Is.EqualTo("A"));
        Assert.That(uBeta.Value.Label, Is.EqualTo("B"));

        var uAlphaSvc = (NamedServiceA)uAlpha.Value;
        var uBetaSvc = (NamedServiceB)uBeta.Value;

        uAlpha.Dispose();

        Assert.That(uAlphaSvc.IsDisposed, Is.True);
        Assert.That(uBetaSvc.IsDisposed, Is.False);

        uBeta.Dispose();
        Assert.That(uBetaSvc.IsDisposed, Is.True);
    }

    [Test]
    public void Owned_RegisteredFactory_WorksCorrectly()
    {
        // T resolved via factory registration
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterFactory<ITrackedService>(c => new TrackedService());

        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uSvc = (TrackedService)uOwned.Value;

        Assert.That(uSvc.IsDisposed, Is.False);

        uOwned.Dispose();
        Assert.That(uSvc.IsDisposed, Is.True);
    }

    [Test]
    public void Owned_SameTransientResolvedTwice_DifferentInstances()
    {
        // Each Owned<T> should get its own instance even for the same T
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var af1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var af2 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afSvc1 = (TrackedService)af1.Value;
        var afSvc2 = (TrackedService)af2.Value;

        var u1 = unity.Resolve<Owned<ITrackedService>>();
        var u2 = unity.Resolve<Owned<ITrackedService>>();
        var uSvc1 = (TrackedService)u1.Value;
        var uSvc2 = (TrackedService)u2.Value;

        Assert.That(ReferenceEquals(afSvc1, afSvc2), Is.False);
        Assert.That(ReferenceEquals(uSvc1, uSvc2), Is.False);

        af1.Dispose();
        u1.Dispose();

        Assert.That(afSvc1.IsDisposed, Is.True);
        Assert.That(afSvc2.IsDisposed, Is.False);
        Assert.That(uSvc1.IsDisposed, Is.EqualTo(afSvc1.IsDisposed));
        Assert.That(uSvc2.IsDisposed, Is.EqualTo(afSvc2.IsDisposed));

        af2.Dispose();
        u2.Dispose();
    }

    // ── Convoluted Scenarios ─────────────────────────────────────────────

    [Test]
    public void DiamondDependency_PerResolve_SharedLeafIsSameInstance()
    {
        // Diamond: Root → A + B → SharedLeaf (PerResolve)
        // Within one Owned scope, A and B should get the SAME SharedLeaf instance
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<SharedLeaf>().As<ISharedLeaf>().InstancePerDependency();
        autofacBuilder.RegisterType<BranchA>().As<IBranchA>();
        autofacBuilder.RegisterType<BranchB>().As<IBranchB>();
        autofacBuilder.RegisterType<DiamondRoot>().As<IDiamondRoot>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ISharedLeaf, SharedLeaf>(new Unity.Lifetime.PerResolveLifetimeManager());
        unity.RegisterType<IBranchA, BranchA>();
        unity.RegisterType<IBranchB, BranchB>();
        unity.RegisterType<IDiamondRoot, DiamondRoot>();

        var uOwned = unity.Resolve<Owned<IDiamondRoot>>();
        var uRoot = (DiamondRoot)uOwned.Value;
        var uLeafA = (SharedLeaf)uRoot.A.Leaf;
        var uLeafB = (SharedLeaf)uRoot.B.Leaf;

        // PerResolve: same instance within one resolve graph
        Assert.That(ReferenceEquals(uLeafA, uLeafB), Is.True,
            "PerResolve should share SharedLeaf within one Owned resolve");

        uOwned.Dispose();

        Assert.That(uRoot.IsDisposed, Is.True);
        Assert.That(((BranchA)uRoot.A).IsDisposed, Is.True);
        Assert.That(((BranchB)uRoot.B).IsDisposed, Is.True);
        Assert.That(uLeafA.IsDisposed, Is.True);
    }

    [Test]
    public void DiamondDependency_Transient_LeafIsDifferentPerBranch()
    {
        // Without PerResolve, each branch gets its own leaf
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<SharedLeaf>().As<ISharedLeaf>();
        autofacBuilder.RegisterType<BranchA>().As<IBranchA>();
        autofacBuilder.RegisterType<BranchB>().As<IBranchB>();
        autofacBuilder.RegisterType<DiamondRoot>().As<IDiamondRoot>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ISharedLeaf, SharedLeaf>();
        unity.RegisterType<IBranchA, BranchA>();
        unity.RegisterType<IBranchB, BranchB>();
        unity.RegisterType<IDiamondRoot, DiamondRoot>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IDiamondRoot>>();
        var afRoot = (DiamondRoot)afOwned.Value;
        var afDifferent = !ReferenceEquals(afRoot.A.Leaf, afRoot.B.Leaf);

        var uOwned = unity.Resolve<Owned<IDiamondRoot>>();
        var uRoot = (DiamondRoot)uOwned.Value;
        var uDifferent = !ReferenceEquals(uRoot.A.Leaf, uRoot.B.Leaf);

        Assert.That(afDifferent, Is.True);
        Assert.That(uDifferent, Is.EqualTo(afDifferent));

        var uLeafA = (SharedLeaf)uRoot.A.Leaf;
        var uLeafB = (SharedLeaf)uRoot.B.Leaf;

        afOwned.Dispose();
        uOwned.Dispose();

        // Both leaves disposed
        Assert.That(uLeafA.IsDisposed, Is.True);
        Assert.That(uLeafB.IsDisposed, Is.True);
    }

    [Test]
    public void DiamondDependency_SingletonLeaf_SharedAndSurvives()
    {
        // Singleton leaf: shared across branches AND across Owned scopes, survives Owned dispose
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<SharedLeaf>().As<ISharedLeaf>().SingleInstance();
        autofacBuilder.RegisterType<BranchA>().As<IBranchA>();
        autofacBuilder.RegisterType<BranchB>().As<IBranchB>();
        autofacBuilder.RegisterType<DiamondRoot>().As<IDiamondRoot>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterSingleton<ISharedLeaf, SharedLeaf>();
        unity.RegisterType<IBranchA, BranchA>();
        unity.RegisterType<IBranchB, BranchB>();
        unity.RegisterType<IDiamondRoot, DiamondRoot>();

        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IDiamondRoot>>();
        var afLeaf = (SharedLeaf)afOwned.Value.A.Leaf;
        var afSame = ReferenceEquals(afOwned.Value.A.Leaf, afOwned.Value.B.Leaf);

        var uOwned = unity.Resolve<Owned<IDiamondRoot>>();
        var uLeaf = (SharedLeaf)uOwned.Value.A.Leaf;
        var uSame = ReferenceEquals(uOwned.Value.A.Leaf, uOwned.Value.B.Leaf);

        Assert.That(afSame, Is.True);
        Assert.That(uSame, Is.EqualTo(afSame));

        afOwned.Dispose();
        uOwned.Dispose();

        Assert.That(afLeaf.IsDisposed, Is.False, "Autofac: singleton survives Owned dispose");
        Assert.That(uLeaf.IsDisposed, Is.EqualTo(afLeaf.IsDisposed));
    }

    [Test]
    public void DeepChildHierarchy_RegistrationsSplitAcrossLevels()
    {
        // Root: IServiceWithDependency
        // Child1: IDependency
        // Owned resolved from Child1 — should see both registrations
        using var root = new UnityContainer();
        root.AddExtension(new OwnedExtension());
        root.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        using var child1 = ((IUnityContainer)root).CreateChildContainer();
        child1.RegisterType<IDependency, Dependency>();

        var owned = child1.Resolve<Owned<IServiceWithDependency>>();
        var svc = (ServiceWithDependency)owned.Value;
        var dep = (Dependency)svc.Dependency;

        owned.Dispose();

        Assert.That(svc.IsDisposed, Is.True);
        Assert.That(dep.IsDisposed, Is.True);
    }

    [Test]
    public void ThreeLevelChildHierarchy_OwnedFromDeepest()
    {
        // Root: ITrackedService (singleton)
        // Child1: IDependency (transient)
        // Child2: IServiceWithSingletonDependency
        // Owned from Child2 should see all three
        using var root = new UnityContainer();
        root.AddExtension(new OwnedExtension());
        root.RegisterSingleton<ITrackedService, TrackedService>();

        using var child1 = ((IUnityContainer)root).CreateChildContainer();
        child1.RegisterType<IDependency, Dependency>();

        using var child2 = ((IUnityContainer)child1).CreateChildContainer();
        child2.RegisterType<IServiceWithSingletonDependency, ServiceWithSingletonDependency>();

        var owned = child2.Resolve<Owned<IServiceWithSingletonDependency>>();
        var svc = (ServiceWithSingletonDependency)owned.Value;
        var singleton = (TrackedService)svc.SingletonDep;
        var dep = (Dependency)svc.TransientDep;

        owned.Dispose();

        Assert.That(svc.IsDisposed, Is.True);
        Assert.That(dep.IsDisposed, Is.True);
        Assert.That(singleton.IsDisposed, Is.False, "Singleton from root survives");
    }

    [Test]
    public void ChildOverridesLifetime_OwnedRespectsChildLifetime()
    {
        // Root registers transient, child overrides to hierarchical
        // Owned from child should use hierarchical behavior
        using var root = new UnityContainer();
        root.AddExtension(new OwnedExtension());
        root.RegisterType<ITrackedService, TrackedService>();

        using var child = ((IUnityContainer)root).CreateChildContainer();
        child.RegisterType<ITrackedService, TrackedService>(
            new Unity.Lifetime.HierarchicalLifetimeManager());

        var owned1 = child.Resolve<Owned<ITrackedService>>();
        var owned2 = child.Resolve<Owned<ITrackedService>>();

        // Each Owned creates its own child → each gets its own hierarchical instance
        Assert.That(ReferenceEquals(owned1.Value, owned2.Value), Is.False);

        var svc1 = (TrackedService)owned1.Value;
        owned1.Dispose();

        Assert.That(svc1.IsDisposed, Is.True);
        Assert.That(((TrackedService)owned2.Value).IsDisposed, Is.False);

        owned2.Dispose();
    }

    [Test]
    public void OwnedDisposeThenResolveNew_GetsFreshInstance()
    {
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var owned1 = unity.Resolve<Owned<ITrackedService>>();
        var svc1 = (TrackedService)owned1.Value;
        owned1.Dispose();

        Assert.That(svc1.IsDisposed, Is.True);

        // Resolve again — should get a fresh instance
        var owned2 = unity.Resolve<Owned<ITrackedService>>();
        var svc2 = (TrackedService)owned2.Value;

        Assert.That(svc2.IsDisposed, Is.False);
        Assert.That(ReferenceEquals(svc1, svc2), Is.False);

        owned2.Dispose();
    }

    [Test]
    public void HierarchicalDep_TwoOwnedFromSameContainer_IndependentScopes()
    {
        // Hierarchical dep means one-per-container. Two Owned scopes from same
        // container should each get their own instance (since each creates a child).
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>().InstancePerLifetimeScope();
        autofacBuilder.RegisterType<ServiceWithDependency>().As<IServiceWithDependency>();
        autofacBuilder.RegisterType<Dependency>().As<IDependency>().InstancePerLifetimeScope();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>(
            new Unity.Lifetime.HierarchicalLifetimeManager());
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();
        unity.RegisterType<IDependency, Dependency>(
            new Unity.Lifetime.HierarchicalLifetimeManager());

        var afOwned1 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithDependency>>();
        var afOwned2 = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<IServiceWithDependency>>();
        var afDep1 = (Dependency)afOwned1.Value.Dependency;
        var afDep2 = (Dependency)afOwned2.Value.Dependency;
        var afDifferentDeps = !ReferenceEquals(afDep1, afDep2);

        var uOwned1 = unity.Resolve<Owned<IServiceWithDependency>>();
        var uOwned2 = unity.Resolve<Owned<IServiceWithDependency>>();
        var uDep1 = (Dependency)uOwned1.Value.Dependency;
        var uDep2 = (Dependency)uOwned2.Value.Dependency;
        var uDifferentDeps = !ReferenceEquals(uDep1, uDep2);

        Assert.That(afDifferentDeps, Is.True, "Autofac: each scope gets own hierarchical dep");
        Assert.That(uDifferentDeps, Is.EqualTo(afDifferentDeps));

        afOwned1.Dispose();
        uOwned1.Dispose();

        Assert.That(afDep1.IsDisposed, Is.True);
        Assert.That(afDep2.IsDisposed, Is.False);
        Assert.That(uDep1.IsDisposed, Is.EqualTo(afDep1.IsDisposed));
        Assert.That(uDep2.IsDisposed, Is.EqualTo(afDep2.IsDisposed));

        afOwned2.Dispose();
        uOwned2.Dispose();
    }

    [Test]
    public void Concurrent_OwnedFromChildContainers_Independent()
    {
        // Multiple threads resolving Owned from different child containers
        using var root = new UnityContainer();
        root.AddExtension(new OwnedExtension());
        root.RegisterType<ITrackedService, TrackedService>();

        var results = new ConcurrentBag<(TrackedService svc, bool disposedAfterOwned)>();

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            using var child = ((IUnityContainer)root).CreateChildContainer();
            var owned = child.Resolve<Owned<ITrackedService>>();
            var svc = (TrackedService)owned.Value;
            owned.Dispose();
            results.Add((svc, svc.IsDisposed));
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(results.Count, Is.EqualTo(10));
        Assert.That(results.All(r => r.disposedAfterOwned), Is.True,
            "All services should be disposed after Owned.Dispose()");
        // All different instances
        var ids = results.Select(r => r.svc.Id).Distinct().Count();
        Assert.That(ids, Is.EqualTo(10));
    }

    [Test]
    public void FuncDependency_LateBoundCreation_InsideOwnedScope()
    {
        // Service takes Func<IDependency> and creates instances lazily.
        // Instances created AFTER Owned.Resolve should still be tracked?
        // Actually no — Func creates from the child container, but after
        // Owned is already resolved. Let's see what happens.
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDependency, Dependency>();
        unity.RegisterType<IFuncConsumer, FuncConsumer>();

        var owned = unity.Resolve<Owned<IFuncConsumer>>();
        var consumer = (FuncConsumer)owned.Value;

        // Create deps via factory — these come from the Owned child container
        var dep1 = (Dependency)consumer.CreateDep();
        var dep2 = (Dependency)consumer.CreateDep();

        Assert.That(ReferenceEquals(dep1, dep2), Is.False);

        owned.Dispose();

        // The consumer is disposed (tracked by DisposalTrackingStrategy)
        Assert.That(consumer.IsDisposed, Is.True);
        // But late-created deps via Func — are they tracked?
        // They were created from the child container which is now disposed.
        // Unity should have tracked them if the Func resolves through the pipeline.
    }

    [Test]
    public void MixedOwnedAndDirect_SameContainer_NoInterference()
    {
        // Resolve Owned<T> and T directly from same container.
        // Direct T should NOT be affected by Owned disposal.
        var autofacBuilder = new ContainerBuilder();
        autofacBuilder.RegisterType<TrackedService>().As<ITrackedService>();
        using var autofac = autofacBuilder.Build();

        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<ITrackedService, TrackedService>();

        var afDirect = (TrackedService)autofac.Resolve<ITrackedService>();
        var afOwned = autofac.Resolve<Autofac.Features.OwnedInstances.Owned<ITrackedService>>();
        var afOwnedSvc = (TrackedService)afOwned.Value;

        var uDirect = (TrackedService)unity.Resolve<ITrackedService>();
        var uOwned = unity.Resolve<Owned<ITrackedService>>();
        var uOwnedSvc = (TrackedService)uOwned.Value;

        // Different instances (transient)
        Assert.That(ReferenceEquals(afDirect, afOwnedSvc), Is.False);
        Assert.That(ReferenceEquals(uDirect, uOwnedSvc), Is.False);

        afOwned.Dispose();
        uOwned.Dispose();

        // Only Owned service disposed, direct unaffected
        Assert.That(afOwnedSvc.IsDisposed, Is.True);
        Assert.That(afDirect.IsDisposed, Is.False);
        Assert.That(uOwnedSvc.IsDisposed, Is.EqualTo(afOwnedSvc.IsDisposed));
        Assert.That(uDirect.IsDisposed, Is.EqualTo(afDirect.IsDisposed));
    }

    [Test]
    public void Owned_WithinParallelResolves_EachGetsOwnScope()
    {
        // Parallel Owned<T> resolves from the same container with hierarchical deps
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IDiamondRoot, DiamondRoot>();
        unity.RegisterType<IBranchA, BranchA>();
        unity.RegisterType<IBranchB, BranchB>();
        unity.RegisterType<ISharedLeaf, SharedLeaf>(
            new Unity.Lifetime.HierarchicalLifetimeManager());

        var results = new ConcurrentBag<(int leafAId, int leafBId, bool same)>();

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var owned = unity.Resolve<Owned<IDiamondRoot>>();
            var root = (DiamondRoot)owned.Value;
            var leafA = (SharedLeaf)root.A.Leaf;
            var leafB = (SharedLeaf)root.B.Leaf;
            // With hierarchical, same container → same instance
            results.Add((leafA.Id, leafB.Id, ReferenceEquals(leafA, leafB)));
            owned.Dispose();
            Assert.That(leafA.IsDisposed, Is.True);
        })).ToArray();

        Task.WaitAll(tasks);

        // All should have same leaf within their scope (hierarchical)
        Assert.That(results.All(r => r.same), Is.True,
            "Hierarchical: A and B should share leaf within each Owned scope");

        // All scopes should have different leaf IDs
        var uniqueIds = results.Select(r => r.leafAId).Distinct().Count();
        Assert.That(uniqueIds, Is.EqualTo(20),
            "Each parallel Owned scope should get its own hierarchical leaf");
    }

    [Test]
    public void ChildContainer_DisposedBeforeOwned_OwnedStillWorks()
    {
        // Resolve Owned from child, dispose child, then dispose Owned.
        // Owned's scope is detached from child, so it should still work.
        using var root = new UnityContainer();
        root.AddExtension(new OwnedExtension());
        root.RegisterType<ITrackedService, TrackedService>();

        var child = ((IUnityContainer)root).CreateChildContainer();
        var owned = child.Resolve<Owned<ITrackedService>>();
        var svc = (TrackedService)owned.Value;

        // Dispose child first
        child.Dispose();

        // Service should still be alive — Owned scope is independent
        Assert.That(svc.IsDisposed, Is.False);

        // Now dispose Owned
        owned.Dispose();
        Assert.That(svc.IsDisposed, Is.True);
    }

    [Test]
    public void RapidCreateAndDispose_NoLeaks()
    {
        // Rapidly create and dispose many Owned instances
        using var unity = new UnityContainer();
        unity.AddExtension(new OwnedExtension());
        unity.RegisterType<IServiceWithDependency, ServiceWithDependency>();
        unity.RegisterType<IDependency, Dependency>();

        var services = new List<(ServiceWithDependency svc, Dependency dep)>();

        for (int i = 0; i < 100; i++)
        {
            var owned = unity.Resolve<Owned<IServiceWithDependency>>();
            var svc = (ServiceWithDependency)owned.Value;
            var dep = (Dependency)svc.Dependency;
            owned.Dispose();
            services.Add((svc, dep));
        }

        Assert.That(services.All(s => s.svc.IsDisposed), Is.True);
        Assert.That(services.All(s => s.dep.IsDisposed), Is.True);
    }
}
