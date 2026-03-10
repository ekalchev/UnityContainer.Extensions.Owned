using NUnit.Framework;
using Unity;
using Unity.Lifetime;

namespace UnityContainer.Extensions.Owned.Tests;

[TestFixture]
public class SingletonDisposalOrderTests
{
    /// <summary>
    /// Transients inside an Owned scope are disposed in reverse creation order,
    /// which naturally respects the dependency graph (dependents before dependencies).
    /// </summary>
    [Test]
    public void Owned_transients_disposed_in_reverse_creation_order()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // All transient: Root -> Middle -> Leaf
        // Creation order will be: Leaf, Middle, Root (dependencies first)
        container.RegisterType<IRoot, Root>();
        container.RegisterType<IMiddle, Middle>();
        container.RegisterType<ILeaf, Leaf>();

        Owned<IRoot> owned = container.Resolve<Owned<IRoot>>();
        ((Leaf)((Middle)owned.Value.Middle).Leaf).DisposalLog = disposalOrder;
        ((Middle)owned.Value.Middle).DisposalLog = disposalOrder;
        ((Root)owned.Value).DisposalLog = disposalOrder;

        owned.Dispose();

        // Reverse creation order: Root -> Middle -> Leaf (dependents before dependencies)
        Assert.That(disposalOrder, Is.EqualTo(new[] { "Root", "Middle", "Leaf" }));
    }

    /// <summary>
    /// Singletons are disposed in reverse registration order, NOT reverse creation order.
    /// When registration order matches dependency order (dependencies registered first),
    /// disposal is correct. This test shows the CORRECT case.
    /// </summary>
    [Test]
    public void Singletons_disposed_correctly_when_registered_in_dependency_order()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // Register dependency first, then dependent — matches dependency order
        container.RegisterType<ILeaf, Leaf>(new ContainerControlledLifetimeManager());
        container.RegisterType<IMiddle, Middle>(new ContainerControlledLifetimeManager());
        container.RegisterType<IRoot, Root>(new ContainerControlledLifetimeManager());

        // Resolve root — creates Leaf, Middle, Root in that order
        Root root = (Root)container.Resolve<IRoot>();
        ((Leaf)((Middle)root.Middle).Leaf).DisposalLog = disposalOrder;
        ((Middle)root.Middle).DisposalLog = disposalOrder;
        root.DisposalLog = disposalOrder;

        container.Dispose();

        // Reverse registration order = Root -> Middle -> Leaf
        // This happens to match reverse dependency order — correct by luck
        Assert.That(disposalOrder, Is.EqualTo(new[] { "Root", "Middle", "Leaf" }));
    }

    /// <summary>
    /// When singletons are registered in the WRONG order (dependent before dependency),
    /// disposal order is broken: dependencies are disposed before their dependents.
    /// This is the bug we want to fix.
    /// </summary>
    [Test]
    public void Singletons_disposed_in_wrong_order_when_registered_out_of_dependency_order()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // Register dependent BEFORE dependency — wrong order
        container.RegisterType<IRoot, Root>(new ContainerControlledLifetimeManager());
        container.RegisterType<IMiddle, Middle>(new ContainerControlledLifetimeManager());
        container.RegisterType<ILeaf, Leaf>(new ContainerControlledLifetimeManager());

        // Resolve root — creation order is still Leaf, Middle, Root (dependencies first)
        Root root = (Root)container.Resolve<IRoot>();
        ((Leaf)((Middle)root.Middle).Leaf).DisposalLog = disposalOrder;
        ((Middle)root.Middle).DisposalLog = disposalOrder;
        root.DisposalLog = disposalOrder;

        container.Dispose();

        // Reverse creation order: Root -> Middle -> Leaf (dependents before dependencies)
        Assert.That(disposalOrder, Is.EqualTo(new[] { "Root", "Middle", "Leaf" }));
    }

    /// <summary>
    /// Register A, B, C but resolve them individually in order C, A, B.
    /// Disposal should follow reverse instantiation (B, A, C), not reverse registration (C, B, A).
    /// </summary>
    [Test]
    public void Independent_singletons_disposed_in_reverse_instantiation_order()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // Registration order: A, B, C
        container.RegisterType<IA, A>(new ContainerControlledLifetimeManager());
        container.RegisterType<IB, B>(new ContainerControlledLifetimeManager());
        container.RegisterType<IC, C>(new ContainerControlledLifetimeManager());

        // Instantiation order: C, A, B
        C c = (C)container.Resolve<IC>();
        c.DisposalLog = disposalOrder;

        A a = (A)container.Resolve<IA>();
        a.DisposalLog = disposalOrder;

        B b = (B)container.Resolve<IB>();
        b.DisposalLog = disposalOrder;

        container.Dispose();

        // Reverse instantiation order: B -> A -> C
        Assert.That(disposalOrder, Is.EqualTo(new[] { "B", "A", "C" }));
    }

    /// <summary>
    /// Register dependent before dependency, resolve only the dependent (which triggers
    /// creation of the dependency). Disposal should still be dependent first.
    /// </summary>
    [Test]
    public void Dependent_singleton_resolved_triggers_dependency_creation_disposal_respects_graph()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // Register dependent before dependency
        container.RegisterType<IMiddle, Middle>(new ContainerControlledLifetimeManager());
        container.RegisterType<ILeaf, Leaf>(new ContainerControlledLifetimeManager());

        // Only resolve Middle — Leaf is created as a dependency
        // Creation order: Leaf (dependency), Middle (dependent)
        Middle middle = (Middle)container.Resolve<IMiddle>();
        ((Leaf)middle.Leaf).DisposalLog = disposalOrder;
        middle.DisposalLog = disposalOrder;

        container.Dispose();

        // Reverse creation order: Middle -> Leaf
        Assert.That(disposalOrder, Is.EqualTo(new[] { "Middle", "Leaf" }));
    }

    /// <summary>
    /// Five singletons registered in alphabetical order, resolved in a completely different order.
    /// Disposal should match reverse instantiation.
    /// </summary>
    [Test]
    public void Five_singletons_disposal_follows_reverse_instantiation_not_registration()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // Registration order: A, B, C, D, E
        container.RegisterType<IA, A>(new ContainerControlledLifetimeManager());
        container.RegisterType<IB, B>(new ContainerControlledLifetimeManager());
        container.RegisterType<IC, C>(new ContainerControlledLifetimeManager());
        container.RegisterType<ID, D>(new ContainerControlledLifetimeManager());
        container.RegisterType<IE, E>(new ContainerControlledLifetimeManager());

        // Instantiation order: E, C, A, D, B
        E e = (E)container.Resolve<IE>();
        e.DisposalLog = disposalOrder;

        C c = (C)container.Resolve<IC>();
        c.DisposalLog = disposalOrder;

        A a = (A)container.Resolve<IA>();
        a.DisposalLog = disposalOrder;

        D d = (D)container.Resolve<ID>();
        d.DisposalLog = disposalOrder;

        B b = (B)container.Resolve<IB>();
        b.DisposalLog = disposalOrder;

        container.Dispose();

        // Reverse instantiation order: B, D, A, C, E
        Assert.That(disposalOrder, Is.EqualTo(new[] { "B", "D", "A", "C", "E" }));
    }

    /// <summary>
    /// Resolving a singleton multiple times does not change its position in the disposal order.
    /// </summary>
    [Test]
    public void Resolving_singleton_twice_does_not_change_disposal_position()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        container.RegisterType<IA, A>(new ContainerControlledLifetimeManager());
        container.RegisterType<IB, B>(new ContainerControlledLifetimeManager());

        // Instantiate A first, then B
        A a = (A)container.Resolve<IA>();
        a.DisposalLog = disposalOrder;

        B b = (B)container.Resolve<IB>();
        b.DisposalLog = disposalOrder;

        // Resolve A again — should NOT move it after B
        container.Resolve<IA>();

        container.Dispose();

        // Reverse instantiation: B -> A (unchanged by second resolve)
        Assert.That(disposalOrder, Is.EqualTo(new[] { "B", "A" }));
    }

    /// <summary>
    /// Mixed lifetimes: both singletons and hierarchical are reordered because
    /// HierarchicalLifetimeManager inherits from ContainerControlledLifetimeManager.
    /// </summary>
    [Test]
    public void Reorder_affects_both_singletons_and_hierarchical()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        container.RegisterType<IA, A>(new ContainerControlledLifetimeManager());
        container.RegisterType<IB, B>(new HierarchicalLifetimeManager());

        A a = (A)container.Resolve<IA>();
        a.DisposalLog = disposalOrder;

        B b = (B)container.Resolve<IB>();
        b.DisposalLog = disposalOrder;

        container.Dispose();

        // Both disposed in reverse creation order: B then A
        Assert.That(disposalOrder, Is.EqualTo(new[] { "B", "A" }));
    }

    /// <summary>
    /// Hierarchical lifetime managers are reordered to reverse creation order,
    /// same as singletons. Register B before A, instantiate A before B.
    /// Disposal should follow reverse instantiation (B, A), not reverse registration (A, B).
    /// </summary>
    [Test]
    public void Hierarchical_disposed_in_reverse_instantiation_order()
    {
        List<string> disposalOrder = new List<string>();

        var container = new Unity.UnityContainer();
        container.AddExtension(new OwnedExtension());

        // Registration order: B, A
        container.RegisterType<IB, B>(new HierarchicalLifetimeManager());
        container.RegisterType<IA, A>(new HierarchicalLifetimeManager());

        // Instantiation order: A, B
        A a = (A)container.Resolve<IA>();
        a.DisposalLog = disposalOrder;

        B b = (B)container.Resolve<IB>();
        b.DisposalLog = disposalOrder;

        container.Dispose();

        // Reverse instantiation order: B -> A
        Assert.That(disposalOrder, Is.EqualTo(new[] { "B", "A" }));
    }

    #region Test types

    public interface IA { }
    public class A : IA, IDisposable
    {
        public List<string>? DisposalLog { get; set; }
        public void Dispose() => DisposalLog?.Add("A");
    }

    public interface IB { }
    public class B : IB, IDisposable
    {
        public List<string>? DisposalLog { get; set; }
        public void Dispose() => DisposalLog?.Add("B");
    }

    public interface IC { }
    public class C : IC, IDisposable
    {
        public List<string>? DisposalLog { get; set; }
        public void Dispose() => DisposalLog?.Add("C");
    }

    public interface ID { }
    public class D : ID, IDisposable
    {
        public List<string>? DisposalLog { get; set; }
        public void Dispose() => DisposalLog?.Add("D");
    }

    public interface IE { }
    public class E : IE, IDisposable
    {
        public List<string>? DisposalLog { get; set; }
        public void Dispose() => DisposalLog?.Add("E");
    }

    #endregion independent types

    #region Test types

    public interface ILeaf { }

    public class Leaf : ILeaf, IDisposable
    {
        public List<string>? DisposalLog { get; set; }
        public void Dispose() => DisposalLog?.Add("Leaf");
    }

    public interface IMiddle
    {
        ILeaf Leaf { get; }
    }

    public class Middle : IMiddle, IDisposable
    {
        public ILeaf Leaf { get; }
        public List<string>? DisposalLog { get; set; }

        public Middle(ILeaf leaf)
        {
            Leaf = leaf;
        }

        public void Dispose() => DisposalLog?.Add("Middle");
    }

    public interface IRoot
    {
        IMiddle Middle { get; }
    }

    public class Root : IRoot, IDisposable
    {
        public IMiddle Middle { get; }
        public List<string>? DisposalLog { get; set; }

        public Root(IMiddle middle)
        {
            Middle = middle;
        }

        public void Dispose() => DisposalLog?.Add("Root");
    }

    #endregion
}
