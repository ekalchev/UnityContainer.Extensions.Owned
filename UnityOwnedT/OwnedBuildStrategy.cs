using Unity;
using Unity.Builder;
using Unity.Lifetime;
using Unity.Resolution;
using Unity.Strategies;

namespace UnityOwnedT;

public class OwnedBuildStrategy : BuilderStrategy
{
    public override void PreBuildUp(ref BuilderContext context)
    {
        var type = context.Type;

        if (!type.IsGenericType)
            return;

        var genericDef = type.GetGenericTypeDefinition();

        if (genericDef == typeof(Owned<>))
        {
            HandleOwned(ref context, type);
            return;
        }

        if (IsFuncReturningOwned(type, out var funcParamTypes, out var ownedType))
        {
            HandleFuncOwned(ref context, type, funcParamTypes, ownedType!);
        }
    }

    private static void HandleOwned(ref BuilderContext context, Type ownedType,
        params ResolverOverride[] overrides)
    {
        var innerType = ownedType.GetGenericArguments()[0];

        // Check if T is registered as singleton by inspecting the lifetime manager.
        // If so, return the singleton with a no-op scope (parent owns its lifetime).
        if (IsSingleton(context.Container, innerType))
        {
            var singleton = context.Container.Resolve(innerType, context.Name);
            var owned = Activator.CreateInstance(ownedType, singleton, NoOpScope.Instance);
            context.Existing = owned;
            context.BuildComplete = true;
            return;
        }

        var child = context.Container.CreateChildContainer();

        try
        {
            ReRegisterWithTracking(context.Container, child);

            if (!innerType.IsInterface && !innerType.IsAbstract)
                child.RegisterType(innerType, new HierarchicalLifetimeManager());

            var resolved = child.Resolve(innerType, context.Name, overrides);
            var owned = Activator.CreateInstance(ownedType, resolved, (IDisposable)child);
            context.Existing = owned;
            context.BuildComplete = true;
        }
        catch
        {
            child.Dispose();
            throw;
        }
    }

    private static bool IsSingleton(IUnityContainer container, Type type)
    {
        foreach (var reg in container.Registrations)
        {
            if (reg.RegisteredType == type &&
                reg.LifetimeManager is ContainerControlledLifetimeManager)
                return true;
        }
        return false;
    }

    private static void HandleFuncOwned(ref BuilderContext context, Type funcType,
        Type[] paramTypes, Type ownedType)
    {
        var innerType = ownedType.GetGenericArguments()[0];
        var container = context.Container;

        var factory = CreateFactory(container, innerType, ownedType, funcType, paramTypes);
        context.Existing = factory;
        context.BuildComplete = true;
    }

    private static Delegate CreateFactory(IUnityContainer container, Type innerType,
        Type ownedType, Type funcType, Type[] paramTypes)
    {
        return paramTypes.Length switch
        {
            1 => MakeFunc1(container, innerType, ownedType, paramTypes[0]),
            2 => MakeFunc2(container, innerType, ownedType, paramTypes[0], paramTypes[1]),
            3 => MakeFunc3(container, innerType, ownedType, paramTypes[0], paramTypes[1], paramTypes[2]),
            _ => throw new NotSupportedException(
                $"Func with {paramTypes.Length} parameters + Owned<T> is not supported. Max 3 parameters.")
        };
    }

    private static Delegate MakeFunc1(IUnityContainer container, Type innerType,
        Type ownedType, Type p1Type)
    {
        Func<object?[], object> resolver = args =>
            ResolveOwned(container, innerType, ownedType,
                new ParameterOverride(p1Type, args[0]));

        var method = typeof(OwnedBuildStrategy)
            .GetMethod(nameof(CreateTypedFunc1), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(p1Type, ownedType);

        return (Delegate)method.Invoke(null, [resolver])!;
    }

    private static Func<T1, TResult> CreateTypedFunc1<T1, TResult>(
        Func<object?[], object> resolver)
    {
        return (p1) => (TResult)resolver([p1]);
    }

    private static Delegate MakeFunc2(IUnityContainer container, Type innerType,
        Type ownedType, Type p1Type, Type p2Type)
    {
        Func<object?[], object> resolver = args =>
            ResolveOwned(container, innerType, ownedType,
                new ParameterOverride(p1Type, args[0]),
                new ParameterOverride(p2Type, args[1]));

        var method = typeof(OwnedBuildStrategy)
            .GetMethod(nameof(CreateTypedFunc2), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(p1Type, p2Type, ownedType);

        return (Delegate)method.Invoke(null, [resolver])!;
    }

    private static Func<T1, T2, TResult> CreateTypedFunc2<T1, T2, TResult>(
        Func<object?[], object> resolver)
    {
        return (p1, p2) => (TResult)resolver([p1, p2]);
    }

    private static Delegate MakeFunc3(IUnityContainer container, Type innerType,
        Type ownedType, Type p1Type, Type p2Type, Type p3Type)
    {
        Func<object?[], object> resolver = args =>
            ResolveOwned(container, innerType, ownedType,
                new ParameterOverride(p1Type, args[0]),
                new ParameterOverride(p2Type, args[1]),
                new ParameterOverride(p3Type, args[2]));

        var method = typeof(OwnedBuildStrategy)
            .GetMethod(nameof(CreateTypedFunc3), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(p1Type, p2Type, p3Type, ownedType);

        return (Delegate)method.Invoke(null, [resolver])!;
    }

    private static Func<T1, T2, T3, TResult> CreateTypedFunc3<T1, T2, T3, TResult>(
        Func<object?[], object> resolver)
    {
        return (p1, p2, p3) => (TResult)resolver([p1, p2, p3]);
    }

    private static object ResolveOwned(IUnityContainer container, Type innerType,
        Type ownedType, params ResolverOverride[] overrides)
    {
        var child = container.CreateChildContainer();

        try
        {
            ReRegisterWithTracking(container, child);

            if (!innerType.IsInterface && !innerType.IsAbstract)
                child.RegisterType(innerType, new HierarchicalLifetimeManager());

            var resolved = child.Resolve(innerType, overrides);
            return Activator.CreateInstance(ownedType, resolved, (IDisposable)child)!;
        }
        catch
        {
            child.Dispose();
            throw;
        }
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }

    private static void ReRegisterWithTracking(IUnityContainer parent, IUnityContainer child)
    {
        foreach (var reg in parent.Registrations)
        {
            if (reg.RegisteredType == reg.MappedToType)
                continue;

            child.RegisterType(
                reg.RegisteredType,
                reg.MappedToType,
                new HierarchicalLifetimeManager());
        }
    }

    private static bool IsFuncReturningOwned(Type type, out Type[] paramTypes, out Type? ownedType)
    {
        paramTypes = [];
        ownedType = null;

        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        var args = type.GetGenericArguments();

        bool isFunc = genericDef == typeof(Func<,>) ||
                      genericDef == typeof(Func<,,>) ||
                      genericDef == typeof(Func<,,,>);

        if (!isFunc)
            return false;

        var lastArg = args[^1];
        if (!lastArg.IsGenericType || lastArg.GetGenericTypeDefinition() != typeof(Owned<>))
            return false;

        paramTypes = args[..^1];
        ownedType = lastArg;
        return true;
    }
}
