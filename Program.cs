using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MemoryReuseDictionary.cs
{
    class Program
    {
        static void Main(string[] args)
        {
            int xx = -1;
            int yy = xx << 1;

            var dict = new MemoryReuseDictionary<int, int>();//<int, int>();
            for (int i = 0; i < 10000000; ++i)
            {
                dict.Add(i, i + 1);
            }
            dict.TrimExcess();


            Console.WriteLine("Done first");
            Console.ReadLine();

            //  dict.Add(123, "apa");
            //  dict.Add(456, "bepa");

            // dict.Remove(456);
            // dict.TrimExcess();
        }


        const uint PRIME32_2 = 2246822519U;
        const uint PRIME32_3 = 3266489917U;

        private static uint rotate(uint v)
        {
            v ^= v >> 15;
            v *= PRIME32_2;
            v ^= v >> 13;
            v *= PRIME32_3;
            v ^= v >> 16;
            return v;
        }
    }
}
