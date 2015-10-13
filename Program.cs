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
            var dict = new Dictionary<int, int>();//<int, int>();
            for (int i = 0; i < 10000000; ++i)
            {
                dict.Add(i, i + 1);
            }


            Console.WriteLine("Done first");
            Console.ReadLine();

            //  dict.Add(123, "apa");
            //  dict.Add(456, "bepa");

            // dict.Remove(456);
            // dict.TrimExcess();
        }
    }
}
