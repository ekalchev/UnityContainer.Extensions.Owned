namespace UnityOwnedT;

public class Repository : IRepository
{
    public Repository()
    {
        Console.WriteLine($"  [Repository {GetHashCode()}] Created");
    }

    public void Save(string data)
    {
        Console.WriteLine($"  [Repository {GetHashCode()}] Saving: {data}");
    }

    public void Dispose()
    {
        Console.WriteLine($"  [Repository {GetHashCode()}] Disposed");
    }
}
