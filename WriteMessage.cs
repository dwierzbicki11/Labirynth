public static class Message
{
    private static string logo = "CyberEngine";
    public static void ok(string msg)
    {
        Console.Write($"[{logo}] ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(msg);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
    }
    public static void warning(string msg)
    {
        Console.Write($"[{logo}] ");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write(msg);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
    }
    public static void error(string msg)
    {
        Console.Write($"[{logo}] ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(msg);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
    
    }
    public static void info(string msg)
    {
        Console.Write($"[{logo}] ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(msg);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
    }
}