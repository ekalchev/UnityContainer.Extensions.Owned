namespace UnityContainer.Extensions.Owned.Tests;

public interface ITrackedService
{
    int Id { get; }
}

public class TrackedService : ITrackedService, IDisposable
{
    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

public interface IDependency
{
    int Id { get; }
}

public class Dependency : IDependency, IDisposable
{
    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

public interface IConnection
{
    string ConnectionString { get; }
    int MaxRetries { get; }
}

public class Connection : IConnection, IDisposable
{
    public string ConnectionString { get; }
    public int MaxRetries { get; }
    public bool IsDisposed { get; private set; }

    public Connection(string connectionString, int maxRetries)
    {
        ConnectionString = connectionString;
        MaxRetries = maxRetries;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IGreeter
{
    string Name { get; }
}

public class Greeter : IGreeter, IDisposable
{
    public string Name { get; }
    public bool IsDisposed { get; private set; }

    public Greeter(string name)
    {
        Name = name;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IServiceWithSingletonDependency
{
    ITrackedService SingletonDep { get; }
    IDependency TransientDep { get; }
}

public class ServiceWithSingletonDependency : IServiceWithSingletonDependency, IDisposable
{
    public ITrackedService SingletonDep { get; }
    public IDependency TransientDep { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithSingletonDependency(ITrackedService singletonDep, IDependency transientDep)
    {
        SingletonDep = singletonDep;
        TransientDep = transientDep;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IDeepRoot
{
    int Id { get; }
    IDependency Dependency { get; }
}

public class DeepRoot : IDeepRoot, IDisposable
{
    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsDisposed { get; private set; }
    public IDependency Dependency { get; }

    public DeepRoot(IDependency dependency)
    {
        Dependency = dependency;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

public interface INonDisposableMiddle
{
    IDependency Dependency { get; }
}

public class NonDisposableMiddle : INonDisposableMiddle
{
    public IDependency Dependency { get; }

    public NonDisposableMiddle(IDependency dependency)
    {
        Dependency = dependency;
    }
}

public interface IServiceWithNonDisposableMiddle
{
    INonDisposableMiddle Middle { get; }
}

public class ServiceWithNonDisposableMiddle : IServiceWithNonDisposableMiddle, IDisposable
{
    public INonDisposableMiddle Middle { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithNonDisposableMiddle(INonDisposableMiddle middle)
    {
        Middle = middle;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface INonDisposableService
{
    int Id { get; }
}

public class NonDisposableService : INonDisposableService
{
    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

public interface INamedService
{
    string Label { get; }
}

public class NamedServiceA : INamedService, IDisposable
{
    public string Label => "A";
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

public class NamedServiceB : INamedService, IDisposable
{
    public string Label => "B";
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

public interface IConfigurable
{
    string Host { get; }
    int Port { get; }
    IDependency Dependency { get; }
}

public class ConfigurableService : IConfigurable, IDisposable
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public IDependency Dependency { get; set; } = null!;
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IFieldInjected
{
    string Tag { get; }
}

public class FieldInjectedService : IFieldInjected, IDisposable
{
    [Unity.Dependency]
    public string Tag = "";
    public bool IsDisposed { get; private set; }

    string IFieldInjected.Tag => Tag;

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IMultiParamService
{
    string Name { get; }
    int Count { get; }
    IDependency Dependency { get; }
}

public class MultiParamService : IMultiParamService, IDisposable
{
    public string Name { get; }
    public int Count { get; }
    public IDependency Dependency { get; }
    public bool IsDisposed { get; private set; }

    public MultiParamService(string name, int count, IDependency dependency)
    {
        Name = name;
        Count = count;
        Dependency = dependency;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class DisposalCounter : IDisposable
{
    public int DisposeCount { get; private set; }

    public void Dispose()
    {
        DisposeCount++;
    }
}

// Open generic types
public interface IRepository<T>
{
    int Id { get; }
}

public class Repository<T> : IRepository<T>, IDisposable
{
    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

// Service that depends on Owned<T>
public interface IServiceWithOwnedDependency
{
    IDependency Dependency { get; }
}

public class ServiceWithOwnedDependency : IServiceWithOwnedDependency, IDisposable
{
    private readonly UnityContainer.Extensions.Owned.Owned<IDependency> _ownedDep;
    public IDependency Dependency => _ownedDep.Value;
    public bool IsDisposed { get; private set; }

    public ServiceWithOwnedDependency(UnityContainer.Extensions.Owned.Owned<IDependency> ownedDep)
    {
        _ownedDep = ownedDep;
    }

    public void Dispose()
    {
        _ownedDep.Dispose();
        IsDisposed = true;
    }
}

// Constructor that throws
public interface IFailingService { }

public class FailingService : IFailingService
{
    public FailingService()
    {
        throw new InvalidOperationException("Construction failed");
    }
}

// Service depending on a failing service
public interface IServiceWithFailingDep
{
    IFailingService Failing { get; }
}

public class ServiceWithFailingDep : IServiceWithFailingDep, IDisposable
{
    public IFailingService Failing { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithFailingDep(IFailingService failing, IDependency dependency)
    {
        Failing = failing;
    }

    public void Dispose() => IsDisposed = true;
}

// Service with multiple disposable dependencies
public interface IServiceWithMultipleDeps
{
    ITrackedService Dep1 { get; }
    IDependency Dep2 { get; }
}

public class ServiceWithMultipleDeps : IServiceWithMultipleDeps, IDisposable
{
    public ITrackedService Dep1 { get; }
    public IDependency Dep2 { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithMultipleDeps(ITrackedService dep1, IDependency dep2)
    {
        Dep1 = dep1;
        Dep2 = dep2;
    }

    public void Dispose() => IsDisposed = true;
}

// Dummy entity for open generic tests
public class User { }
public class Order { }

// Diamond dependency: ServiceDiamond → BranchA + BranchB → SharedLeaf
public interface ISharedLeaf
{
    int Id { get; }
}

public class SharedLeaf : ISharedLeaf, IDisposable
{
    private static int _nextId;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add($"SharedLeaf{Id}");
    }

    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

public interface IBranchA
{
    ISharedLeaf Leaf { get; }
}

public class BranchA : IBranchA, IDisposable
{
    public ISharedLeaf Leaf { get; }
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }
    public BranchA(ISharedLeaf leaf) => Leaf = leaf;

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("BranchA");
    }
}

public interface IBranchB
{
    ISharedLeaf Leaf { get; }
}

public class BranchB : IBranchB, IDisposable
{
    public ISharedLeaf Leaf { get; }
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }
    public BranchB(ISharedLeaf leaf) => Leaf = leaf;

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("BranchB");
    }
}

public interface IDiamondRoot
{
    IBranchA A { get; }
    IBranchB B { get; }
}

public class DiamondRoot : IDiamondRoot, IDisposable
{
    public IBranchA A { get; }
    public IBranchB B { get; }
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }
    public DiamondRoot(IBranchA a, IBranchB b) { A = a; B = b; }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("DiamondRoot");
    }
}

// Mixed chain: TransientRoot → SingletonMiddle → TransientLeaf
// Used for testing disposal order when a singleton sits in the middle of a transient chain
public interface ITransientLeaf { }

public class TransientLeaf : ITransientLeaf, IDisposable
{
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("TransientLeaf");
    }
}

public interface ISingletonMiddle
{
    ITransientLeaf Leaf { get; }
}

public class SingletonMiddle : ISingletonMiddle, IDisposable
{
    public ITransientLeaf Leaf { get; }
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }

    public SingletonMiddle(ITransientLeaf leaf)
    {
        Leaf = leaf;
    }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("SingletonMiddle");
    }
}

public interface ITransientRoot
{
    ISingletonMiddle Middle { get; }
}

public class TransientRoot : ITransientRoot, IDisposable
{
    public ISingletonMiddle Middle { get; }
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }

    public TransientRoot(ISingletonMiddle middle)
    {
        Middle = middle;
    }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("TransientRoot");
    }
}

// Nested Owned: OuterOwned depends on Owned<InnerOwned>
// Used for testing root → child1 (outer Owned) → child2 (inner Owned)
public interface IInnerService { }

public class InnerService : IInnerService, IDisposable
{
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }
    public ISingletonMiddle? Singleton { get; }

    public InnerService() { }

    public InnerService(ISingletonMiddle singleton)
    {
        Singleton = singleton;
    }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("InnerService");
    }
}

public interface IOuterService
{
    Owned<IInnerService> InnerOwned { get; }
}

public class OuterService : IOuterService, IDisposable
{
    public Owned<IInnerService> InnerOwned { get; }
    public bool IsDisposed { get; private set; }
    public List<string>? DisposalLog { get; set; }

    public OuterService(Owned<IInnerService> innerOwned)
    {
        InnerOwned = innerOwned;
    }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalLog?.Add("OuterService");
        InnerOwned.Dispose();
    }
}

// Service that creates late-bound instances via Func
public interface IFuncConsumer
{
    IDependency CreateDep();
}

public class FuncConsumer : IFuncConsumer, IDisposable
{
    private readonly Func<IDependency> _factory;
    public bool IsDisposed { get; private set; }
    public FuncConsumer(Func<IDependency> factory) => _factory = factory;
    public IDependency CreateDep() => _factory();
    public void Dispose() => IsDisposed = true;
}

public interface IServiceWithDependency
{
    IDependency Dependency { get; }
    int Id { get; }
}

public class ServiceWithDependency : IServiceWithDependency, IDisposable
{
    private static int _nextId;
    public IDependency Dependency { get; }
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsDisposed { get; private set; }

    public ServiceWithDependency(IDependency dependency)
    {
        Dependency = dependency;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public static void ResetCounter() => Interlocked.Exchange(ref _nextId, 0);
}

// Service with three different lifetime dependencies
public interface ITripleLifetimeService
{
    ITrackedService SingletonDep { get; }
    ISharedLeaf HierarchicalDep { get; }
    IDependency TransientDep { get; }
}

public class TripleLifetimeService : ITripleLifetimeService, IDisposable
{
    public ITrackedService SingletonDep { get; }
    public ISharedLeaf HierarchicalDep { get; }
    public IDependency TransientDep { get; }
    public bool IsDisposed { get; private set; }

    public TripleLifetimeService(ITrackedService singletonDep, ISharedLeaf hierarchicalDep, IDependency transientDep)
    {
        SingletonDep = singletonDep;
        HierarchicalDep = hierarchicalDep;
        TransientDep = transientDep;
    }

    public void Dispose() => IsDisposed = true;
}

// Service with ExternallyControlled dependency
public interface IServiceWithExternalDep
{
    IDependency ExternalDep { get; }
    ITrackedService TransientDep { get; }
}

public class ServiceWithExternalDep : IServiceWithExternalDep, IDisposable
{
    public IDependency ExternalDep { get; }
    public ITrackedService TransientDep { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithExternalDep(IDependency externalDep, ITrackedService transientDep)
    {
        ExternalDep = externalDep;
        TransientDep = transientDep;
    }

    public void Dispose() => IsDisposed = true;
}

// Struct types for value type tests
public interface IValueService
{
    int Id { get; }
    string Name { get; }
}

public struct ValueService : IValueService
{
    public int Id { get; set; }
    public string Name { get; set; }

    public ValueService(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

public struct SimpleStruct
{
    public int Value { get; set; }
}

public struct DisposableStruct : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IServiceWithStructDep
{
    IValueService StructDep { get; }
    IDependency ClassDep { get; }
}

public class ServiceWithStructDep : IServiceWithStructDep, IDisposable
{
    public IValueService StructDep { get; }
    public IDependency ClassDep { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithStructDep(IValueService structDep, IDependency classDep)
    {
        StructDep = structDep;
        ClassDep = classDep;
    }

    public void Dispose() => IsDisposed = true;
}
