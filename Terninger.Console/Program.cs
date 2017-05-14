using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Generator;

using Con = System.Console;

namespace MurrayGrant.Terninger.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO: allow enough command line options to funnel data to dieharder
            //     - format as hex vs binary
            //     - length of data to produce
            var allArgs = args.Aggregate("", (acc, next) => acc + (next ?? ""));
            var key = new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes(allArgs));

            var crng = new BlockCypherCprngGenerator(key);
            var bytes = crng.GetRandomBytes(128);
            Con.WriteLine(bytes.ToHexString());
        }
    }
}
