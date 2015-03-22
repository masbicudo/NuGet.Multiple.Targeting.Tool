using System;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
{
    class ConsoleHelper
    {
        private static object locker = new object();

        public static void Write(string message, ConsoleColor color, int indent)
        {
            lock (locker)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                if (IsActive)
                    Console.Write(new string(' ', indent * 4) + message);
                Console.ForegroundColor = oldColor;
            }
        }

        public static void Write(string message, ConsoleColor color)
        {
            lock (locker)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                if (IsActive)
                    Console.Write(message);
                Console.ForegroundColor = oldColor;
            }
        }

        public static void WriteLine(string message, ConsoleColor color, int indent)
        {
            lock (locker)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                if (IsActive)
                    Console.WriteLine(new string(' ', indent * 4) + message);
                Console.ForegroundColor = oldColor;
            }
        }

        public static void WriteLine(string message, ConsoleColor color)
        {
            lock (locker)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                if (IsActive)
                    Console.WriteLine(message);
                Console.ForegroundColor = oldColor;
            }
        }

        public static void WriteLine()
        {
            lock (locker)
            {
                if (IsActive)
                    Console.WriteLine();
            }
        }

        public static bool IsActive { get; set; }
    }
}
