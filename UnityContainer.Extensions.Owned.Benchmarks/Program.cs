using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastExpressionCompiler;
using Unity;
using UnityContainer.Extensions.Owned;

BenchmarkRunner.Run(new[]
{
    typeof(OwnedCreationBenchmark),
    typeof(ExtensionOverheadBenchmark)
});

/// <summary>
/// Benchmarks different ways to construct Owned&lt;T&gt; instances.
/// </summary>
[MemoryDiagnoser]
public class OwnedCreationBenchmark
{
    private static readonly Type OwnedType = typeof(Owned<string>);
    private static readonly Type InnerType = typeof(string);
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Func<object, IDisposable, object> CachedFastCompiled;
    private static readonly Func<object, IDisposable, object> CachedSystemCompiled;
    private static readonly Func<object, IDisposable, object> CachedMakeGenericDelegate;
    private static readonly Func<object, IDisposable, object> CachedILEmit;

    private static readonly MethodInfo GenericCreateMethod = typeof(OwnedCreationBenchmark)
        .GetMethod(nameof(CreateOwnedGeneric), BindingFlags.Static | BindingFlags.NonPublic)!;

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

        var method = GenericCreateMethod.MakeGenericMethod(InnerType);
        CachedMakeGenericDelegate = (Func<object, IDisposable, object>)method.CreateDelegate(
            typeof(Func<object, IDisposable, object>));

        CachedILEmit = BuildILFactory(OwnedType, InnerType, ctor);
    }

    private static object CreateOwnedGeneric<T>(object value, IDisposable scope)
        => new Owned<T>((T)value, scope);

    private static Func<object, IDisposable, object> BuildILFactory(Type ownedType, Type innerType, ConstructorInfo ctor)
    {
        var dm = new DynamicMethod(
            name: "CreateOwned",
            returnType: typeof(object),
            parameterTypes: [typeof(object), typeof(IDisposable)],
            m: ownedType.Module,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);             // load value (object)
        il.Emit(OpCodes.Castclass, innerType); // cast to T
        il.Emit(OpCodes.Ldarg_1);             // load scope (IDisposable)
        il.Emit(OpCodes.Newobj, ctor);        // new Owned<T>(value, scope)
        il.Emit(OpCodes.Ret);

        return (Func<object, IDisposable, object>)dm.CreateDelegate(typeof(Func<object, IDisposable, object>));
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

    [Benchmark]
    public object MakeGenericMethod_NoCache()
    {
        var method = GenericCreateMethod.MakeGenericMethod(InnerType);
        var factory = (Func<object, IDisposable, object>)method.CreateDelegate(
            typeof(Func<object, IDisposable, object>));
        return factory(_value, _scope);
    }

    [Benchmark]
    public object MakeGenericMethod_Cached()
    {
        return CachedMakeGenericDelegate(_value, _scope);
    }

    [Benchmark]
    public object ILEmit_NoCache()
    {
        var ctor = OwnedType.GetConstructor(Flags, null, [InnerType, typeof(IDisposable)], null)!;
        var factory = BuildILFactory(OwnedType, InnerType, ctor);
        return factory(_value, _scope);
    }

    [Benchmark]
    public object ILEmit_Cached()
    {
        return CachedILEmit(_value, _scope);
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Measures the overhead of having OwnedExtension registered on non-Owned resolutions.
/// Compares resolving plain types with and without the extension to quantify pipeline cost.
/// </summary>
[MemoryDiagnoser]
public class ExtensionOverheadBenchmark
{
    private Unity.UnityContainer containerWithout = null!;
    private Unity.UnityContainer containerWith = null!;

    [GlobalSetup]
    public void Setup()
    {
        containerWithout = new Unity.UnityContainer();
        containerWithout.RegisterType<ISimpleService, SimpleService>();
        containerWithout.RegisterType<IServiceWithDep, ServiceWithDep>();

        containerWith = new Unity.UnityContainer();
        containerWith.AddExtension(new OwnedExtension());
        containerWith.RegisterType<ISimpleService, SimpleService>();
        containerWith.RegisterType<IServiceWithDep, ServiceWithDep>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        containerWithout.Dispose();
        containerWith.Dispose();
    }

    // --- Simple transient (no dependencies) ---

    [Benchmark(Baseline = true)]
    public object Transient_WithoutExtension()
    {
        return containerWithout.Resolve<ISimpleService>();
    }

    [Benchmark]
    public object Transient_WithExtension()
    {
        return containerWith.Resolve<ISimpleService>();
    }

    // --- Transient with one dependency ---

    [Benchmark]
    public object TransientWithDep_WithoutExtension()
    {
        return containerWithout.Resolve<IServiceWithDep>();
    }

    [Benchmark]
    public object TransientWithDep_WithExtension()
    {
        return containerWith.Resolve<IServiceWithDep>();
    }

    // --- Owned<T> resolution (only available with extension) ---

    [Benchmark]
    public object OwnedTransient_WithExtension()
    {
        var owned = containerWith.Resolve<Owned<ISimpleService>>();
        owned.Dispose();
        return owned;
    }

    [Benchmark]
    public object OwnedTransientWithDep_WithExtension()
    {
        var owned = containerWith.Resolve<Owned<IServiceWithDep>>();
        owned.Dispose();
        return owned;
    }
}

// Benchmark helper types
public interface ISimpleService { }

public class SimpleService : ISimpleService { }

public interface IServiceWithDep
{
    ISimpleService Dependency { get; }
}

public class ServiceWithDep : IServiceWithDep
{
    public ISimpleService Dependency { get; }

    public ServiceWithDep(ISimpleService dependency)
    {
        Dependency = dependency;
    }
}
