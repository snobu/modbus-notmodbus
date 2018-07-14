using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace modbus_notmodbus
{
    public class Misc
    {
        public static IConfiguration ParseConfig()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            IConfiguration config = builder.Build();

            return config;
        }

        public static async Task WaitFor(TimeSpan t)
        {
            Console.WriteLine($"\nRetrying in {t.TotalSeconds} seconds...\n\n");
            await Task.Delay(t);
        }

        public static void LogInfo(string s)
        {
            Console.BackgroundColor = ConsoleColor.DarkYellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine(s);
            Console.ResetColor();
        }

        public static void LogTimeout(string s)
        {
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine($"[TIMEOUT] {s}");
            Console.ResetColor();
        }

        public static void LogException(string s)
        {
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[EXCEPTION] {s}");
            Console.ResetColor();
        }

        public static void LogDebug(string s)
        {
            Console.WriteLine($"[DEBUG] {s}");
            Console.ResetColor();
        }
    }
}