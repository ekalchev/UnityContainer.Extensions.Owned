using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastExpressionCompiler;
using Unity.Extensions.Owned;

BenchmarkRunner.Run<OwnedCreationBenchmark>();

[MemoryDiagnoser]
public class OwnedCreationBenchmark
{
    private static readonly Type OwnedType = typeof(Owned<string>);
    private static readonly Type InnerType = typeof(string);
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Func<object, IDisposable, object> CachedFastCompiled;
    private static readonly Func<object, IDisposable, object> CachedSystemCompiled;

    private readonly object _value = "hello";
    private readonly IDisposable _scope = NoOpScope.Instance;

    static OwnedCreationBenchmark()
    {
        var ctor = OwnedType.GetConstructor(Flags, null, [InnerType, typeof(IDisposable)], null)!;

        var valueParam = Expression.Parameter(typeof(object), "value");
        var scopeParam = Expression.Parameter(typeof(IDisposable), "scope");
        var body = Expression.New(ctor, Expression.Convert(valueParam, InnerType), scopeParam);
        var lambda = Expression.Lambda<Func<object, IDisposable, object>>(body, valueParam, scopeParam);

        CachedFastCompiled = lambda.CompileFast();
        CachedSystemCompiled = lambda.Compile();
    }

    [Benchmark(Baseline = true)]
    public object ActivatorCreateInstance()
    {
        return Activator.CreateInstance(
            OwnedType, Flags, null,
            new[] { _value, (object)_scope }, null)!;
    }

    [Benchmark]
    public object FastCompiled_NoCache()
    {
        var ctor = OwnedType.GetConstructor(Flags, null, [InnerType, typeof(IDisposable)], null)!;
        var valueParam = Expression.Parameter(typeof(object), "value");
        var scopeParam = Expression.Parameter(typeof(IDisposable), "scope");
        var body = Expression.New(ctor, Expression.Convert(valueParam, InnerType), scopeParam);
        var lambda = Expression.Lambda<Func<object, IDisposable, object>>(body, valueParam, scopeParam);
        var factory = lambda.CompileFast();
        return factory(_value, _scope);
    }

    [Benchmark]
    public object SystemCompiled_NoCache()
    {
        var ctor = OwnedType.GetConstructor(Flags, null, [InnerType, typeof(IDisposable)], null)!;
        var valueParam = Expression.Parameter(typeof(object), "value");
        var scopeParam = Expression.Parameter(typeof(IDisposable), "scope");
        var body = Expression.New(ctor, Expression.Convert(valueParam, InnerType), scopeParam);
        var lambda = Expression.Lambda<Func<object, IDisposable, object>>(body, valueParam, scopeParam);
        var factory = lambda.Compile();
        return factory(_value, _scope);
    }

    [Benchmark]
    public object FastCompiled_Cached()
    {
        return CachedFastCompiled(_value, _scope);
    }

    [Benchmark]
    public object SystemCompiled_Cached()
    {
        return CachedSystemCompiled(_value, _scope);
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }
}
