namespace UnityOwnedT.Tests;

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
