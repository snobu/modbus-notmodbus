using System;
using System.Threading.Tasks;

namespace modbus_notmodbus
{
    public static class Spinner
    {
        public static async Task SleepSpinner(uint pollingInterval)
        {
            Console.Write($"Sleeping for {pollingInterval} seconds  ");
            for (byte i = 0; i < pollingInterval; i++)
            {
                foreach (string s in new string[] { "|", "/", "-", "\\" })
                {
                    await Task.Delay(250);
                    Console.Write($"\b{s}");
                }
            }
            Console.Write("\n");
        }
    }
}