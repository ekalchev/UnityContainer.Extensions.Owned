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
