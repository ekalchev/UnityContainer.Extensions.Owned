namespace UnityOwnedT;

public sealed class Owned<T> : IDisposable
{
    private readonly IDisposable _scope;

    public T Value { get; }

    internal Owned(T value, IDisposable scope)
    {
        Value = value;
        _scope = scope;
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
