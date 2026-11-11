using System;

namespace NAV.Core
{
    /// <summary>
    /// The chat's one road, solo and online. The chat window (UI ChatUI) sends through Send
    /// and shows what Received brings; online, multiplayer (NetworkWorldSync) plugs in as the
    /// Sender and raises Received for every line (its own included). Solo there is no sender:
    /// the line just comes back to the player. Neither side knows the other.
    /// </summary>
    public static class ChatChannel
    {
        /// <summary>Online: sends a line to everyone (and back here). Null solo.</summary>
        public static Action<string> Sender { get; set; }

        /// <summary>A line to show: who said it (null = a system line) and what.</summary>
        public static event Action<string, string> Received;

        /// <summary>Who the player is in the chat solo.</summary>
        public static string LocalName { get; set; } = "You";

        public static void Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string line = text.Trim();
            if (line.Length > 200)
            {
                line = line.Substring(0, 200);
            }

            if (Sender != null)
            {
                Sender(line);
            }
            else
            {
                Received?.Invoke(LocalName, line);
            }
        }

        /// <summary>A line from the network, or a system line (who = null).</summary>
        public static void Receive(string who, string text)
        {
            Received?.Invoke(who, text);
        }
    }
}
