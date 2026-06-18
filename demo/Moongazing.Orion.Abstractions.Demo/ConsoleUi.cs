namespace Moongazing.Orion.Abstractions.Demo;

/// <summary>Small console formatting helpers so each demo prints a consistent, readable section.</summary>
internal static class ConsoleUi
{
    public static void Banner()
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("  Orion.Abstractions - runnable feature demo");
        Console.WriteLine("  Shared foundation primitives for the Orion .NET family");
        Console.WriteLine("============================================================");
        Console.WriteLine();
    }

    public static void Section(int number, string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- [{number}] {title} ".PadRight(60, '-'));
    }

    public static void Step(string message) => Console.WriteLine($"    {message}");

    public static void Done()
    {
        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine("  All demos ran to completion.");
        Console.WriteLine("============================================================");
    }
}
