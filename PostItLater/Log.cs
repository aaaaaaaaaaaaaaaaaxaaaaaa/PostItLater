namespace PostItLater
{
    using System;

    class Log
    {
        public static void Info(string text, ConsoleColor color = ConsoleColor.White)
        {
            if (!Program.Verbose) { return; }

            Console.ForegroundColor = color;
            Console.WriteLine("[INFO] " + text);
            Console.ResetColor();
        }

        public static void Error(string text)
        {
            if (!Program.Verbose) { return; }

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("[ERROR] " + text);
            Console.ResetColor();
        }

        public static void Warn(string text)
        {
            if (!Program.Verbose) { return; }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("[WARN] " + text);
            Console.ResetColor();
        }
    }
}
