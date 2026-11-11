using System;
using System.Collections.Generic;
using System.Linq;

namespace NAV.UI
{
    /// <summary>
    /// The developer console's commands (DevConsoleUI shows it, ` key, when switched on in
    /// Settings > Controls > Developer). Any system can add its own command (Register); a line
    /// is the command's name and its arguments split by spaces; what the command returns is
    /// printed. Commands throw ArgumentException for wrong arguments - the console prints the
    /// message and the usage.
    /// </summary>
    public static class DevConsole
    {
        public sealed class Command
        {
            public string Name;
            public string Usage;
            public string Help;
            public Func<string[], string> Run;
        }

        private static readonly Dictionary<string, Command> Commands = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>A line printed to the console (its own output, the game's errors).</summary>
        public static event Action<string> Printed;

        public static IEnumerable<Command> All => Commands.Values.OrderBy(c => c.Name);

        public static void Register(string name, string usage, string help, Func<string[], string> run)
        {
            Commands[name] = new Command { Name = name, Usage = usage, Help = help, Run = run };
        }

        public static void Print(string text)
        {
            Printed?.Invoke(text);
        }

        public static void Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Print("> " + line.Trim());
            if (!Commands.TryGetValue(parts[0], out Command command))
            {
                Print($"Unknown command '{parts[0]}'. Type help.");
                return;
            }

            try
            {
                string result = command.Run(parts.Skip(1).ToArray());
                if (!string.IsNullOrEmpty(result))
                {
                    Print(result);
                }
            }
            catch (ArgumentException e)
            {
                Print($"{e.Message}  Usage: {command.Usage}");
            }
            catch (Exception e)
            {
                Print($"Error: {e.Message}");
                UnityEngine.Debug.LogException(e);
            }
        }

        /// <summary>Command names starting with the text (Tab completion).</summary>
        public static IEnumerable<string> Complete(string start)
        {
            return Commands.Keys.Where(k => k.StartsWith(start, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k);
        }

        // ---- argument helpers ----

        public static float Float(string[] args, int index, string what)
        {
            if (index >= args.Length || !float.TryParse(args[index].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                throw new ArgumentException($"Expected a number for {what}.");
            }

            return value;
        }

        public static int Int(string[] args, int index, int fallback)
        {
            return index < args.Length && int.TryParse(args[index], out int value) ? value : fallback;
        }
    }
}
