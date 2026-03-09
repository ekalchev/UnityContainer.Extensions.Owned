namespace UnityOwnedT;

public interface IRepository : IDisposable
{
    void Save(string data);
}
