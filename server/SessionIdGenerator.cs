using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LogLab.Server
{
    public class SessionIdGenerator
    {
        private static readonly Random Random = new Random();

        private static readonly string[] Adjectives;

        private static readonly string[] Nouns;

        private static readonly string[] Verbs;

        static SessionIdGenerator()
        {
            Adjectives = File.ReadAllLines("words/adjectives.txt");
            Nouns = File.ReadAllLines("words/nouns.txt");
            Verbs = File.ReadAllLines("words/verbs.txt");
        }

        public static string GenerateId()
        {
            var adj1 = GetRandomWord(Adjectives);
            var noun1 = GetRandomWord(Nouns);
            var verb = GetRandomWord(Verbs);
            var adj2 = GetRandomWord(Adjectives);
            var noun2 = GetRandomWord(Nouns);

            // Ensure the combination makes sense
            while (!IsValidCombination(adj1, noun1, verb, adj2, noun2))
            {
                adj1 = GetRandomWord(Adjectives);
                noun1 = GetRandomWord(Nouns);
                verb = GetRandomWord(Verbs);
                adj2 = GetRandomWord(Adjectives);
                noun2 = GetRandomWord(Nouns);
            }

            return $"{adj1}-{noun1}-{verb}-{adj2}-{noun2}";
        }

        private static string GetRandomWord(string[] words)
        {
            return words[Random.Next(words.Length)];
        }

        private static bool
        IsValidCombination(
            string adj1,
            string noun1,
            string verb,
            string adj2,
            string noun2
        )
        {
            // Prevent same word from appearing twice
            if (adj1 == adj2 || noun1 == noun2) return false;

            // Prevent nonsensical combinations
            var invalidCombinations =
                new Dictionary<string, string[]> {
                    { "eat", new [] { "sticks", "rocks", "trees" } },
                    { "smash", new [] { "clouds", "air", "water" } },
                    { "fly", new [] { "rocks", "trees", "mountains" } },
                    { "swim", new [] { "fire", "air", "rocks" } }
                };

            if (invalidCombinations.TryGetValue(verb, out var invalidObjects))
            {
                if (invalidObjects.Contains(noun2)) return false;
            }

            return true;
        }

        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var parts = id.Split('-');
            if (parts.Length != 5) return false;

            return Adjectives.Contains(parts[0]) &&
            Nouns.Contains(parts[1]) &&
            Verbs.Contains(parts[2]) &&
            Adjectives.Contains(parts[3]) &&
            Nouns.Contains(parts[4]);
        }
    }
}
