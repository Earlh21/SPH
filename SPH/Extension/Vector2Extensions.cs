using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Extensions
{
    internal static class Vector2Extensions
    {
        public static Vector2 Unit(this Vector2 v)
        {
            return v / v.Length();
        }

        public static float Dot(this Vector2 u, Vector2 v)
        {
            return u.X * v.X + u.Y * v.Y;
        }

        public static Vector2 To(this Vector2 u, Vector2 v)
        {
            return v - u;
        }

        public static float DistanceTo(this Vector2 u, Vector2 v)
        {
            return MathF.Sqrt((u.X - v.X) * (u.X - v.X) + (u.Y - u.Y) * (v.Y - v.Y));
        }

        public static Vector2 Sum(this IEnumerable<Vector2> enumerable)
        {
            Vector2 sum = Vector2.Zero;

            foreach(var item in enumerable)
            {
                sum += item;
            }

            return sum;
        }

        public static Vector2 Sum<T>(this IEnumerable<T> enumerable, Func<T, Vector2> selector)
        {
            return enumerable.Select(selector).Sum();
        }
    }
}
