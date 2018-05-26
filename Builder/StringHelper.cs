using System;
using System.Numerics;
using System.Text;
using Microsoft.VisualBasic.CompilerServices;

namespace RSCoreLib
    {
    //USE this with caution! It uses a hacky dirty approach to convert a string into any encoding.
    public static class StringEncoder
        {
        public const string BASE36CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string BASE62CHARS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        public static string ToBase36String(int value)
            {
            return ToString(value, BASE36CHARS);
            }

        public static int FromBase36String (string value)
            {
            return (int)FromString(value, BASE36CHARS);
            }

        public static string ToString (BigInteger toConvert, string charset)
            {
            if (toConvert.Sign < 0)
                throw new ArgumentException("This method only allows positive numbers to be converted");

            var builder = new StringBuilder();
            while (toConvert != 0)
                {
                BigInteger remainder;
                toConvert = BigInteger.DivRem(toConvert, charset.Length, out remainder);
                builder.Insert(0, charset[Math.Abs(((int)remainder))]);
                }
            return builder.ToString();
            }

        public static BigInteger FromString (string encoded, string charset)
            {
            BigInteger i = new BigInteger();
            bool isFirst = true;
            foreach(char c in encoded)
                {
                int index = charset.IndexOf(c);
                if (index < 0)
                    throw new ArgumentException("The provided string contains a char which is not in the charset.");

                if (!isFirst)
                    i = BigInteger.Multiply(i, charset.Length);

                isFirst = false;
                i = BigInteger.Add(i, new BigInteger(index));
                }

            return i;
            }
        }

    public static class StringExtensions
        {
        public static bool Matches(this string s, string pattern, bool ignoreCase = false)
            {
            if (s == null || pattern == null)
                return false;

            if (pattern == "*")
                return true; //shortcut, the method below is heavy!

            return LikeOperator.LikeString(s, pattern, ignoreCase ? Microsoft.VisualBasic.CompareMethod.Text : Microsoft.VisualBasic.CompareMethod.Binary);
            }
        }
    }