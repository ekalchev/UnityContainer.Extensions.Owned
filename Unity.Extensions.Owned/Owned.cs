namespace Unity.Extensions.Owned;

public sealed class Owned<T> : IDisposable
{
    private readonly IDisposable scope;

    public T Value { get; }

    internal Owned(T value, IDisposable scope)
    {
        Value = value;
        this.scope = scope;
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
