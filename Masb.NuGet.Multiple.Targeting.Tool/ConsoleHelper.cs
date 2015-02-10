using System;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    class ConsoleHelper
    {
        public static void Write(string message, ConsoleColor color, int indent)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (IsActive)
                Console.Write(new string(' ', indent * 4) + message);
            Console.ForegroundColor = oldColor;
        }

        public static void Write(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (IsActive)
                Console.Write(message);
            Console.ForegroundColor = oldColor;
        }

        public static void WriteLine(string message, ConsoleColor color, int indent)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (IsActive)
                Console.WriteLine(new string(' ', indent * 4) + message);
            Console.ForegroundColor = oldColor;
        }

        public static void WriteLine(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (IsActive)
                Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        public static void WriteLine()
        {
            if (IsActive)
                Console.WriteLine();
        }

        public static bool IsActive { get; set; }
    }
}
