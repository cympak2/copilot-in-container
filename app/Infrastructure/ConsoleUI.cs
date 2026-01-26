namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Provides colored console output for better UX.
/// </summary>
public static class ConsoleUI
{
    private const string Red = "\u001b[0;31m";
    private const string Green = "\u001b[0;32m";
    private const string Yellow = "\u001b[1;33m";
    private const string Reset = "\u001b[0m";

    public static void PrintError(string message)
    {
        Console.Error.WriteLine($"{Red}❌ {message}{Reset}");
    }

    public static void PrintSuccess(string message)
    {
        Console.WriteLine($"{Green}✅ {message}{Reset}");
    }

    public static void PrintWarning(string message)
    {
        Console.WriteLine($"{Yellow}⚠️  {message}{Reset}");
    }

    public static void PrintInfo(string message)
    {
        Console.WriteLine($"ℹ️  {message}");
    }
}
