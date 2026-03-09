namespace UnityOwnedT;

public class Service : IService, IDisposable
{
    private readonly Owned<IRepository> _ownedRepository;

    public Service(Owned<IRepository> ownedRepository)
    {
        _ownedRepository = ownedRepository;
        Console.WriteLine($"  [Service {GetHashCode()}] Created with Owned<Repository> (inner: {ownedRepository.Value.GetHashCode()})");
    }

    public void DoWork(string data)
    {
        _ownedRepository.Value.Save(data);
    }

    public void Dispose()
    {
        _ownedRepository.Dispose();
        Console.WriteLine($"  [Service {GetHashCode()}] Disposed (and disposed owned scope)");
    }
}
