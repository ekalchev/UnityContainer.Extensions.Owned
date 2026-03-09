using NUnit.Framework;
using Unity;
using Unity.Lifetime;

namespace UnityOwnedT.Tests;

[TestFixture]
public class LifetimeManagerTests
{
    [SetUp]
    public void SetUp()
    {
        TrackedService.ResetCounter();
        Dependency.ResetCounter();
        ServiceWithDependency.ResetCounter();
    }

    [Test]
    public void Transient_NewInstanceEveryResolve()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<ITrackedService, TrackedService>();

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<ITrackedService, TrackedService>();

        var p1 = plain.Resolve<ITrackedService>();
        var p2 = plain.Resolve<ITrackedService>();

        var o1 = withOwned.Resolve<ITrackedService>();
        var o2 = withOwned.Resolve<ITrackedService>();

        Assert.That(ReferenceEquals(p1, p2), Is.False);
        Assert.That(ReferenceEquals(o1, o2), Is.EqualTo(ReferenceEquals(p1, p2)));
    }

    [Test]
    public void Transient_NotDisposedOnContainerDispose()
    {
        TrackedService pService, oService;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<ITrackedService, TrackedService>();
            pService = (TrackedService)plain.Resolve<ITrackedService>();
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<ITrackedService, TrackedService>();
            oService = (TrackedService)withOwned.Resolve<ITrackedService>();
        }

        Assert.That(pService.IsDisposed, Is.False);
        Assert.That(oService.IsDisposed, Is.EqualTo(pService.IsDisposed));
    }

    [Test]
    public void Transient_DependencyNotDisposedOnContainerDispose()
    {
        ServiceWithDependency pService, oService;
        Dependency pDep, oDep;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<IDependency, Dependency>();
            plain.RegisterType<IServiceWithDependency, ServiceWithDependency>();
            pService = (ServiceWithDependency)plain.Resolve<IServiceWithDependency>();
            pDep = (Dependency)pService.Dependency;
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<IDependency, Dependency>();
            withOwned.RegisterType<IServiceWithDependency, ServiceWithDependency>();
            oService = (ServiceWithDependency)withOwned.Resolve<IServiceWithDependency>();
            oDep = (Dependency)oService.Dependency;
        }

        Assert.That(pService.IsDisposed, Is.False);
        Assert.That(pDep.IsDisposed, Is.False);
        Assert.That(oService.IsDisposed, Is.EqualTo(pService.IsDisposed));
        Assert.That(oDep.IsDisposed, Is.EqualTo(pDep.IsDisposed));
    }

    [Test]
    public void ContainerControlled_SameInstanceEveryResolve()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<ITrackedService, TrackedService>(new ContainerControlledLifetimeManager());

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<ITrackedService, TrackedService>(new ContainerControlledLifetimeManager());

        var p1 = plain.Resolve<ITrackedService>();
        var p2 = plain.Resolve<ITrackedService>();

        var o1 = withOwned.Resolve<ITrackedService>();
        var o2 = withOwned.Resolve<ITrackedService>();

        Assert.That(ReferenceEquals(p1, p2), Is.True);
        Assert.That(ReferenceEquals(o1, o2), Is.EqualTo(ReferenceEquals(p1, p2)));
    }

    [Test]
    public void ContainerControlled_DisposedOnContainerDispose()
    {
        TrackedService pService, oService;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<ITrackedService, TrackedService>(new ContainerControlledLifetimeManager());
            pService = (TrackedService)plain.Resolve<ITrackedService>();
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<ITrackedService, TrackedService>(new ContainerControlledLifetimeManager());
            oService = (TrackedService)withOwned.Resolve<ITrackedService>();
        }

        Assert.That(pService.IsDisposed, Is.True);
        Assert.That(oService.IsDisposed, Is.EqualTo(pService.IsDisposed));
    }

    [Test]
    public void Hierarchical_SameInstancePerContainer_DifferentPerChild()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<ITrackedService, TrackedService>(new HierarchicalLifetimeManager());
        using var pChild = ((IUnityContainer)plain).CreateChildContainer();

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<ITrackedService, TrackedService>(new HierarchicalLifetimeManager());
        using var oChild = ((IUnityContainer)withOwned).CreateChildContainer();

        var pParent1 = plain.Resolve<ITrackedService>();
        var pParent2 = plain.Resolve<ITrackedService>();
        var pChildInst = pChild.Resolve<ITrackedService>();

        var oParent1 = withOwned.Resolve<ITrackedService>();
        var oParent2 = withOwned.Resolve<ITrackedService>();
        var oChildInst = oChild.Resolve<ITrackedService>();

        Assert.That(ReferenceEquals(pParent1, pParent2), Is.True);
        Assert.That(ReferenceEquals(pParent1, pChildInst), Is.False);

        Assert.That(ReferenceEquals(oParent1, oParent2), Is.EqualTo(ReferenceEquals(pParent1, pParent2)));
        Assert.That(ReferenceEquals(oParent1, oChildInst), Is.EqualTo(ReferenceEquals(pParent1, pChildInst)));
    }

    [Test]
    public void Hierarchical_DisposedWhenOwningContainerDisposed()
    {
        TrackedService pParentInst, pChildInst;
        TrackedService oParentInst, oChildInst;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<ITrackedService, TrackedService>(new HierarchicalLifetimeManager());
            pParentInst = (TrackedService)plain.Resolve<ITrackedService>();

            using (var pChild = ((IUnityContainer)plain).CreateChildContainer())
            {
                pChildInst = (TrackedService)pChild.Resolve<ITrackedService>();
            }

            Assert.That(pChildInst.IsDisposed, Is.True);
            Assert.That(pParentInst.IsDisposed, Is.False);
        }
        Assert.That(pParentInst.IsDisposed, Is.True);

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<ITrackedService, TrackedService>(new HierarchicalLifetimeManager());
            oParentInst = (TrackedService)withOwned.Resolve<ITrackedService>();

            using (var oChild = ((IUnityContainer)withOwned).CreateChildContainer())
            {
                oChildInst = (TrackedService)oChild.Resolve<ITrackedService>();
            }

            Assert.That(oChildInst.IsDisposed, Is.EqualTo(pChildInst.IsDisposed));
            Assert.That(oParentInst.IsDisposed, Is.False);
        }
        Assert.That(oParentInst.IsDisposed, Is.EqualTo(pParentInst.IsDisposed));
    }

    [Test]
    public void PerResolve_SameInstanceWithinSingleResolve()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<IDependency, Dependency>(new PerResolveLifetimeManager());
        plain.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<IDependency, Dependency>(new PerResolveLifetimeManager());
        withOwned.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        var pSvc1 = (ServiceWithDependency)plain.Resolve<IServiceWithDependency>();
        var pSvc2 = (ServiceWithDependency)plain.Resolve<IServiceWithDependency>();

        var oSvc1 = (ServiceWithDependency)withOwned.Resolve<IServiceWithDependency>();
        var oSvc2 = (ServiceWithDependency)withOwned.Resolve<IServiceWithDependency>();

        // Different resolve calls get different dependency instances
        var pDifferentAcrossResolves = !ReferenceEquals(pSvc1.Dependency, pSvc2.Dependency);
        var oDifferentAcrossResolves = !ReferenceEquals(oSvc1.Dependency, oSvc2.Dependency);

        Assert.That(pDifferentAcrossResolves, Is.True);
        Assert.That(oDifferentAcrossResolves, Is.EqualTo(pDifferentAcrossResolves));
    }

    [Test]
    public void PerResolve_NotDisposedOnContainerDispose()
    {
        Dependency pDep, oDep;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<IDependency, Dependency>(new PerResolveLifetimeManager());
            pDep = (Dependency)plain.Resolve<IDependency>();
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<IDependency, Dependency>(new PerResolveLifetimeManager());
            oDep = (Dependency)withOwned.Resolve<IDependency>();
        }

        Assert.That(pDep.IsDisposed, Is.False);
        Assert.That(oDep.IsDisposed, Is.EqualTo(pDep.IsDisposed));
    }

    [Test]
    public void PerThread_SameInstanceOnSameThread()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<ITrackedService, TrackedService>(new PerThreadLifetimeManager());

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<ITrackedService, TrackedService>(new PerThreadLifetimeManager());

        var p1 = plain.Resolve<ITrackedService>();
        var p2 = plain.Resolve<ITrackedService>();

        var o1 = withOwned.Resolve<ITrackedService>();
        var o2 = withOwned.Resolve<ITrackedService>();

        Assert.That(ReferenceEquals(p1, p2), Is.True);
        Assert.That(ReferenceEquals(o1, o2), Is.EqualTo(ReferenceEquals(p1, p2)));
    }

    [Test]
    public void PerThread_DifferentInstanceOnDifferentThread()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<ITrackedService, TrackedService>(new PerThreadLifetimeManager());

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<ITrackedService, TrackedService>(new PerThreadLifetimeManager());

        var pMain = plain.Resolve<ITrackedService>();
        ITrackedService? pOther = null;
        var t1 = new Thread(() => pOther = plain.Resolve<ITrackedService>());
        t1.Start();
        t1.Join();

        var oMain = withOwned.Resolve<ITrackedService>();
        ITrackedService? oOther = null;
        var t2 = new Thread(() => oOther = withOwned.Resolve<ITrackedService>());
        t2.Start();
        t2.Join();

        var pDifferent = !ReferenceEquals(pMain, pOther);
        var oDifferent = !ReferenceEquals(oMain, oOther);

        Assert.That(pDifferent, Is.True);
        Assert.That(oDifferent, Is.EqualTo(pDifferent));
    }

    [Test]
    public void PerThread_NotDisposedOnContainerDispose()
    {
        TrackedService pService, oService;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<ITrackedService, TrackedService>(new PerThreadLifetimeManager());
            pService = (TrackedService)plain.Resolve<ITrackedService>();
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<ITrackedService, TrackedService>(new PerThreadLifetimeManager());
            oService = (TrackedService)withOwned.Resolve<ITrackedService>();
        }

        Assert.That(pService.IsDisposed, Is.False);
        Assert.That(oService.IsDisposed, Is.EqualTo(pService.IsDisposed));
    }

    [Test]
    public void ExternallyControlled_SameInstanceWhileAlive()
    {
        using var plain = new UnityContainer();
        plain.RegisterType<ITrackedService, TrackedService>(new ExternallyControlledLifetimeManager());

        using var withOwned = new UnityContainer();
        withOwned.AddExtension(new OwnedExtension());
        withOwned.RegisterType<ITrackedService, TrackedService>(new ExternallyControlledLifetimeManager());

        var p1 = plain.Resolve<ITrackedService>();
        var p2 = plain.Resolve<ITrackedService>();

        var o1 = withOwned.Resolve<ITrackedService>();
        var o2 = withOwned.Resolve<ITrackedService>();

        Assert.That(ReferenceEquals(p1, p2), Is.True);
        Assert.That(ReferenceEquals(o1, o2), Is.EqualTo(ReferenceEquals(p1, p2)));
    }

    [Test]
    public void ExternallyControlled_NotDisposedOnContainerDispose()
    {
        TrackedService pService, oService;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<ITrackedService, TrackedService>(new ExternallyControlledLifetimeManager());
            pService = (TrackedService)plain.Resolve<ITrackedService>();
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<ITrackedService, TrackedService>(new ExternallyControlledLifetimeManager());
            oService = (TrackedService)withOwned.Resolve<ITrackedService>();
        }

        Assert.That(pService.IsDisposed, Is.False);
        Assert.That(oService.IsDisposed, Is.EqualTo(pService.IsDisposed));
    }

    [Test]
    public void MultipleLifetimes_MixedRegistrations_BehaviorPreserved()
    {
        TrackedService pSingleton, oSingleton;
        TrackedService pTransient1, pTransient2, oTransient1, oTransient2;
        Dependency pDep, oDep;

        using (var plain = new UnityContainer())
        {
            plain.RegisterType<ITrackedService, TrackedService>(new ContainerControlledLifetimeManager());
            plain.RegisterType<IDependency, Dependency>();
            plain.RegisterType<IServiceWithDependency, ServiceWithDependency>();

            pSingleton = (TrackedService)plain.Resolve<ITrackedService>();
            pTransient1 = (TrackedService)plain.Resolve<TrackedService>();
            pTransient2 = (TrackedService)plain.Resolve<TrackedService>();
            var pSvc = (ServiceWithDependency)plain.Resolve<IServiceWithDependency>();
            pDep = (Dependency)pSvc.Dependency;
        }

        using (var withOwned = new UnityContainer())
        {
            withOwned.AddExtension(new OwnedExtension());
            withOwned.RegisterType<ITrackedService, TrackedService>(new ContainerControlledLifetimeManager());
            withOwned.RegisterType<IDependency, Dependency>();
            withOwned.RegisterType<IServiceWithDependency, ServiceWithDependency>();

            oSingleton = (TrackedService)withOwned.Resolve<ITrackedService>();
            oTransient1 = (TrackedService)withOwned.Resolve<TrackedService>();
            oTransient2 = (TrackedService)withOwned.Resolve<TrackedService>();
            var oSvc = (ServiceWithDependency)withOwned.Resolve<IServiceWithDependency>();
            oDep = (Dependency)oSvc.Dependency;
        }

        // Singleton disposed with container
        Assert.That(pSingleton.IsDisposed, Is.True);
        Assert.That(oSingleton.IsDisposed, Is.EqualTo(pSingleton.IsDisposed));

        // Transients not disposed
        Assert.That(pTransient1.IsDisposed, Is.False);
        Assert.That(pTransient2.IsDisposed, Is.False);
        Assert.That(oTransient1.IsDisposed, Is.EqualTo(pTransient1.IsDisposed));
        Assert.That(oTransient2.IsDisposed, Is.EqualTo(pTransient2.IsDisposed));

        // Transient dependency not disposed
        Assert.That(pDep.IsDisposed, Is.False);
        Assert.That(oDep.IsDisposed, Is.EqualTo(pDep.IsDisposed));
    }

    [Test]
    public void Concurrent_OwnedDoesNotLeakTrackingToParallelTransientResolves()
    {
        using var container = new UnityContainer();
        container.AddExtension(new OwnedExtension());
        container.RegisterType<ITrackedService, TrackedService>();

        const int iterations = 100;
        var directInstances = new TrackedService[iterations];
        var ownedInstances = new Owned<ITrackedService>[iterations];
        var barrier = new Barrier(2);

        // Thread 1: resolve transients directly (no Owned)
        var directThread = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
                directInstances[i] = (TrackedService)container.Resolve<ITrackedService>();
        });

        // Thread 2: resolve and dispose Owned<T> concurrently
        var ownedThread = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                ownedInstances[i] = container.Resolve<Owned<ITrackedService>>();
                ownedInstances[i].Dispose();
            }
        });

        directThread.Start();
        ownedThread.Start();
        directThread.Join();
        ownedThread.Join();

        // All directly resolved transients must NOT be disposed
        for (int i = 0; i < iterations; i++)
            Assert.That(directInstances[i].IsDisposed, Is.False,
                $"Direct transient instance {i} was incorrectly disposed");

        // All Owned-resolved instances should be disposed
        for (int i = 0; i < iterations; i++)
            Assert.That(((TrackedService)ownedInstances[i].Value).IsDisposed, Is.True,
                $"Owned instance {i} was not disposed");
    }

    [Test]
    public void Concurrent_MultipleOwnedResolvesDoNotInterfere()
    {
        using var container = new UnityContainer();
        container.AddExtension(new OwnedExtension());
        container.RegisterType<IDependency, Dependency>();
        container.RegisterType<IServiceWithDependency, ServiceWithDependency>();

        const int threads = 10;
        const int perThread = 20;
        var results = new (ServiceWithDependency svc, Dependency dep, bool svcDisposed, bool depDisposed)[threads * perThread];
        var barrier = new Barrier(threads);

        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < perThread; i++)
            {
                var owned = container.Resolve<Owned<IServiceWithDependency>>();
                var svc = (ServiceWithDependency)owned.Value;
                var dep = (Dependency)svc.Dependency;
                owned.Dispose();

                var idx = t * perThread + i;
                results[idx] = (svc, dep, svc.IsDisposed, dep.IsDisposed);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        for (int i = 0; i < results.Length; i++)
        {
            Assert.That(results[i].svcDisposed, Is.True,
                $"Service {i} was not disposed");
            Assert.That(results[i].depDisposed, Is.True,
                $"Dependency {i} was not disposed");
        }
    }

    [Test]
    public void Concurrent_TransientResolvesDuringOwnedDisposal_NotAffected()
    {
        using var container = new UnityContainer();
        container.AddExtension(new OwnedExtension());
        container.RegisterType<ITrackedService, TrackedService>();

        const int iterations = 100;
        var directBefore = new TrackedService[iterations];
        var directDuring = new TrackedService[iterations];
        var directAfter = new TrackedService[iterations];

        // Resolve some transients before any Owned usage
        for (int i = 0; i < iterations; i++)
            directBefore[i] = (TrackedService)container.Resolve<ITrackedService>();

        // Resolve Owned and direct transients concurrently
        var barrier = new Barrier(2);
        var ownedThread = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var owned = container.Resolve<Owned<ITrackedService>>();
                owned.Dispose();
            }
        });

        var directThread = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
                directDuring[i] = (TrackedService)container.Resolve<ITrackedService>();
        });

        ownedThread.Start();
        directThread.Start();
        ownedThread.Join();
        directThread.Join();

        // Resolve some transients after all Owned are disposed
        for (int i = 0; i < iterations; i++)
            directAfter[i] = (TrackedService)container.Resolve<ITrackedService>();

        // None of the directly resolved transients should be disposed
        for (int i = 0; i < iterations; i++)
        {
            Assert.That(directBefore[i].IsDisposed, Is.False, $"Before[{i}] disposed");
            Assert.That(directDuring[i].IsDisposed, Is.False, $"During[{i}] disposed");
            Assert.That(directAfter[i].IsDisposed, Is.False, $"After[{i}] disposed");
        }
    }
}
