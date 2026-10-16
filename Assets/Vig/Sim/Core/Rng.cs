// Deterministic random numbers: mulberry32, exactly as the TS sim's src/engine/rng.ts (uint arithmetic
// wraps the way JS's >>> 0 and Math.imul do).
using System;
using System.Collections.Generic;

namespace Vig.Sim
{
    public sealed class Rng
    {
        public uint S;

        public Rng(uint seed)
        {
            S = seed != 0 ? seed : 0x9e3779b9;
        }

        /// <summary>mulberry32: [0, 1)</summary>
        public double Next()
        {
            unchecked
            {
                S += 0x6d2b79f5;
                uint t = S;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>An integer in [a, b] inclusive.</summary>
        public int Int(int a, int b) => a + (int)Math.Floor(Next() * (b - a + 1));

        public double Range(double a, double b) => a + Next() * (b - a);

        public bool Chance(double p) => Next() < p;

        public T Pick<T>(IReadOnlyList<T> arr) => arr[(int)Math.Floor(Next() * arr.Count)];

        public T Weighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
        {
            double total = 0;
            foreach (var it in items) total += weight(it);
            double r = Next() * total;
            foreach (var it in items)
            {
                r -= weight(it);
                if (r <= 0) return it;
            }
            return items[items.Count - 1];
        }

        public void Shuffle<T>(IList<T> arr)
        {
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(Next() * (i + 1));
                T tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
            }
        }
    }
}
