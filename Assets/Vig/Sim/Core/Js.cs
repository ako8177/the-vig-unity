// JavaScript's number semantics where the sim depends on them, so the C# sim lands on the same night
// as the TypeScript one it was ported from (same seed + same commands = same state, bit for bit;
// tools/golden checks it). C# 9: Unity 6 compiles this assembly too.
using System;
using System.Globalization;
using System.Text;

namespace Vig.Sim
{
    public static class Js
    {
        /// <summary>JS Math.round: halves go up (toward +infinity), unlike Math.Round's banker's rounding.</summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        public static int RoundInt(double x) => (int)Round(x);

        /// <summary>A number as JS's Number.prototype.toString prints it (ECMAScript Number::toString).</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsPositiveInfinity(v)) return "Infinity";
            if (double.IsNegativeInfinity(v)) return "-Infinity";
            if (v == 0) return "0";
            var sb = new StringBuilder();
            if (v < 0) { sb.Append('-'); v = -v; }
            // .NET Core's "R" is the shortest round-trip digit string, the same digits JS picks;
            // only the layout differs, so take the digits and the exponent and lay them out as JS does
            string digits; int n;
            Decompose(v.ToString("R", CultureInfo.InvariantCulture), out digits, out n);
            int k = digits.Length;
            if (k <= n && n <= 21) { sb.Append(digits); sb.Append('0', n - k); }
            else if (0 < n && n <= 21) { sb.Append(digits, 0, n); sb.Append('.'); sb.Append(digits, n, k - n); }
            else if (-6 < n && n <= 0) { sb.Append("0."); sb.Append('0', -n); sb.Append(digits); }
            else
            {
                int e = n - 1;
                sb.Append(digits[0]);
                if (k > 1) { sb.Append('.'); sb.Append(digits, 1, k - 1); }
                sb.Append('e'); sb.Append(e >= 0 ? '+' : '-'); sb.Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Split a positive number's string into significant digits and n, where value = 0.digits × 10^n.</summary>
        static void Decompose(string s, out string digits, out int n)
        {
            int exp = 0;
            int ei = s.IndexOfAny(new[] { 'E', 'e' });
            if (ei >= 0) { exp = int.Parse(s.Substring(ei + 1), CultureInfo.InvariantCulture); s = s.Substring(0, ei); }
            int dot = s.IndexOf('.');
            string intPart = dot >= 0 ? s.Substring(0, dot) : s;
            string frac = dot >= 0 ? s.Substring(dot + 1) : "";
            string all = intPart + frac;
            int point = intPart.Length + exp;
            int lead = 0;
            while (lead < all.Length && all[lead] == '0') lead++;
            all = all.Substring(lead);
            point -= lead;
            all = all.TrimEnd('0');
            digits = all;
            n = point;
        }

        /// <summary>FNV-1a over UTF-16 code units, as the TS sim's hashStr (charCodeAt + Math.imul).</summary>
        public static uint HashStr(string s)
        {
            uint h = 2166136261;
            unchecked
            {
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619;
                }
            }
            return h;
        }
    }
}
