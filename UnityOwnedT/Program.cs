using Unity;
using UnityOwnedT;

internal class Program
{
    static void Main(string[] args)
    {
        using var container = new UnityContainer();
        container.AddExtension(new OwnedExtension());
        container.RegisterSingleton<IRepository, Repository>();
        container.RegisterType<IService, Service>();

        Console.WriteLine("=== Demo 1: Owned<IRepository> resolved directly ===");
        var owned = container.Resolve<Owned<IRepository>>();
        owned.Value.Save("direct usage");
        Console.WriteLine("Disposing owned scope...");
        owned.Dispose();

        Console.WriteLine();
        Console.WriteLine("=== Demo 2: Service with Owned<IRepository> ===");
        var service = container.Resolve<IService>();
        service.DoWork("via service");

        Console.WriteLine();
        Console.WriteLine("=== Demo 3: Each resolve gets its own scope ===");
        var owned1 = container.Resolve<Owned<IRepository>>();
        var owned2 = container.Resolve<Owned<IRepository>>();
        Console.WriteLine($"Same instance? {ReferenceEquals(owned1.Value, owned2.Value)}");
        owned1.Dispose();
        owned2.Dispose();

        Console.WriteLine();
        Console.WriteLine("=== Done ===");
    }
}
