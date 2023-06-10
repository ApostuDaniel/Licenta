using System;
using System.Collections.Generic;
using System.Linq;
using static Unity.VisualScripting.Member;

namespace Assets.Scripts.Neat
{
    public static class Utils
    {
        private static readonly System.Random rng = new();

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static (byte r, byte g, byte b) CreateColorFromId(int id)
        {
            String hash;
            using var md5 = System.Security.Cryptography.MD5.Create();
            hash = String.Concat(md5.ComputeHash(BitConverter
              .GetBytes(id))
              .Select(x => x.ToString("x2")));

            hash = hash.Substring(hash.Length - 7, 6);

            byte r = (byte)Int32.Parse(hash[..2], System.Globalization.NumberStyles.HexNumber);
            byte g = (byte)Int32.Parse(hash[2..4], System.Globalization.NumberStyles.HexNumber);
            byte b = (byte)Int32.Parse(hash[4..6], System.Globalization.NumberStyles.HexNumber);

            return (r, g, b);
        }
    }
}