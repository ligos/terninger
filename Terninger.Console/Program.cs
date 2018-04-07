using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.CryptoPrimitives;
using MurrayGrant.Terninger.EntropySources;

using Con = System.Console;

namespace MurrayGrant.Terninger.Console
{
    class Program
    {
        // This is a bit of a cheats way of doing command line arguments. Please don't consider it good practice!
        static string seed = "";
        static string outFile = "";
        static long byteCount = 64;
        static OutputStyle outputStyle = OutputStyle.Hex;
        static bool quiet = false;

        static bool nonDeterministic = false;       // Will inject additional entropy into the generator based on timing & memory.
        static CryptoPrimitiveOption cryptoPrimitive = CryptoPrimitiveOption.Default;
        static HashAlgorithmOption hashAlgorithm = HashAlgorithmOption.Default;
        static bool useNativeCrypto = false;        // If true, will use CSP / CNG versions of the crypto primitives, if available.

        public enum OutputStyle
        {
            Hex = 1,
            Binary,
        }

        public enum CryptoPrimitiveOption
        {
            Default = 0,
            Aes128,
            Aes256,
            Sha256,
            Sha512,
            HmacSha256,
            HmacSha512,
        }
        public enum HashAlgorithmOption
        {
            Default = 0,
            Sha256,
            Sha512,
        }

        private static readonly int OutBufferSize = 32 * 1024;
        private static readonly long OneMBAsLong = 1024L * 1024L;
        private static readonly double OneMBAsDouble = 1024.0 * 1024.0;

        private static bool _CancelSignalled = false;

        static void Main(string[] args)
        {
            try
            {
                Con.CancelKeyPress += Console_CancelKeyPress;
                Con.OutputEncoding = Encoding.UTF8;
                if (!ParseCommandLine(args))
                {
                    PrintUsage();
                    Environment.Exit(1);
                }

                // Initialise logging. In real world, your wouldn't need this as LibLog supports most major logging frameworks auto-magically.
                Terninger.LibLog.LogProvider.SetCurrentLogProvider(new LibLog.ColoredConsoleLogProvider());

                RunMain();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                var originalColour = Con.ForegroundColor;
                Con.ForegroundColor = ConsoleColor.Red;
                Con.Write(ex);
                Con.ForegroundColor = originalColour;
                Environment.Exit(2);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // We will handle the CTRL+C break gracefully.
            _CancelSignalled = true;
            e.Cancel = true;
        }


        private static void RunMain()
        {
            // TODO: integrate with logging.

            // Hello world!
            if (!quiet)
                Con.WriteLine("Terninger CPRNG   © Murray Grant");

            // Load and initialise objects.
            var seedAndSource = DeriveSeed();
            var outStreamAndTarget = GetOutputStream();
            var outputWriter = GetOutputWriter();
            var generatorAndSource = CreateRandomGenerator(seedAndSource.Item1);
            if (!quiet)
            {
                Con.WriteLine("Generating {0:N0} random bytes as {1} output.", byteCount, outputStyle);
                Con.WriteLine("Source: {0}", generatorAndSource.Item2);
                Con.WriteLine("Seed source: {0}", seedAndSource.Item2);
                Con.WriteLine("Output target: {0}", outStreamAndTarget.Item2);
                if (outFile == "")
                    // Stdio output needs extra line here
                    Con.WriteLine();
            }

            long generatedBytes = 0L;
            var rng = generatorAndSource.Item1;
            var sw = Stopwatch.StartNew();      // Start the clock for a basic measure of performance.
            using (var outStream = outStreamAndTarget.Item1)
            {
                long remaining = byteCount;

                // Read and write in buffered chunks (for larger requests).
                byte[] buf = new byte[OutBufferSize];
                while (remaining > buf.Length)
                {
                    rng.FillWithRandomBytes(buf);          // Fill one buffer with randomness.
                    generatedBytes = generatedBytes + buf.Length;   // Increment counter.
                    outputWriter(outStream, buf);           // Write the buffer to out output stream.
                    remaining = remaining - buf.Length;     // Decrement remaining.

                    if (_CancelSignalled) break;
                    if (generatedBytes % OneMBAsLong == 0) Con.Write(".");    // Status update.
                }

                // The remaining bytes required.
                if (!_CancelSignalled && remaining > 0L)
                {
                    buf = new byte[(int)remaining];
                    rng.FillWithRandomBytes(buf);
                    generatedBytes = generatedBytes + buf.Length;
                    outputWriter(outStream, buf);
                }
            }
            sw.Stop();

            if (!quiet)
            {
                if (outFile == "")
                    // Stdio output needs extra line here
                    Con.WriteLine();

                Con.WriteLine();
                Con.WriteLine("Wrote {0:N0} bytes in {1:N2} seconds ({2:N2}MB / sec)", generatedBytes, sw.Elapsed.TotalSeconds, ((double)generatedBytes / OneMBAsDouble) / sw.Elapsed.TotalSeconds);
            }
        }

        private static Tuple<byte[], string> DeriveSeed()
        {
            var seedLength = GetKeysizeBytesForCryptoPrimitive();
            if (String.IsNullOrEmpty(seed))
                // No seed: use null array.
                return Tuple.Create(new byte[seedLength], "Null seed - WARNING, INSECURE: the following random numbers are always the same.");
            if (seed.IsHexString() && seed.Length == seedLength * 2)
                // A hex string of required bytes.
                return Tuple.Create(seed.ParseFromHexString(), $"{seedLength} byte hex seed.");
            else if (File.Exists(seed))
            {
                // A file reference: get the SHA512 hash of it as a seed.
                using (var stream = new FileStream(seed, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024))
                    return Tuple.Create(
                            new SHA512Managed().ComputeHash(stream).EnsureArraySize(seedLength), 
                            "SHA512 hash of file."
                        );
            }
            else
                // Assume a random set of characters: get the SHA256 hash of the UTF8 string as a seed.
                return Tuple.Create(
                        new SHA512Managed().ComputeHash(Encoding.UTF8.GetBytes(seed)).EnsureArraySize(seedLength),
                        "SHA512 hash of random string / password / passphrase."
                    );
        }

        private static Tuple<Stream, string> GetOutputStream()
        {
            if (outFile == null)
                // Null output (mostly for benchmarking).
                return Tuple.Create(Stream.Null, "Null stream.");
            else if (outFile == "")
                // Standard output.
                return Tuple.Create(Con.OpenStandardOutput(OutBufferSize), "Standard Output.");
            else
                // File output.
                return Tuple.Create((Stream)new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None, OutBufferSize), outFile);
        }

        private static Action<Stream, byte[]> GetOutputWriter()
        {
            if (outputStyle == OutputStyle.Hex)
                return (output, buf) =>
                {
                    // Format bytes to hex.
                    for (int i = 0; i < buf.Length; i++)
                    {
                        byte b = buf[i];
                        output.WriteByte(b.ToHexAsciiHighNibble());
                        output.WriteByte(b.ToHexAsciiLowNibble());
                    }
                };
            else if (outputStyle == OutputStyle.Binary)
                // Direct copy.
                return (output, buf) => output.Write(buf, 0, buf.Length);
            else
                throw new Exception("Unexpected outputStyle: " + outputStyle);
        }

        private static Tuple<IRandomNumberGenerator, string> CreateRandomGenerator(byte[] key)
        {
            var primitive = GetCryptoPrimitive();
            var hash = GetHashAlgorithm();
            var counter = new CypherCounter(primitive.BlockSizeBytes);
            var entropyGetter = GetEntropyGetter();
            var result = (IRandomNumberGenerator)CypherBasedPrngGenerator.Create(key, primitive, hash, counter, entropyGetter);
            return Tuple.Create(result, $"{(nonDeterministic ? "non-" : "")}deterministic PRNG, using crypto primitive: {cryptoPrimitive}, hash: {hashAlgorithm}");
        }

        private static ICryptoPrimitive GetCryptoPrimitive()
        {
            if ((cryptoPrimitive == CryptoPrimitiveOption.Default || cryptoPrimitive == CryptoPrimitiveOption.Aes256) && useNativeCrypto)
                return CryptoPrimitive.Aes256Native();
            else if ((cryptoPrimitive == CryptoPrimitiveOption.Default || cryptoPrimitive == CryptoPrimitiveOption.Aes256) && !useNativeCrypto)
                return CryptoPrimitive.Aes256Managed();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Aes128 && useNativeCrypto)
                return CryptoPrimitive.Aes128Native();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Aes128 && !useNativeCrypto)
                return CryptoPrimitive.Aes128Managed();

            else if (cryptoPrimitive == CryptoPrimitiveOption.Sha256 && useNativeCrypto)
                return CryptoPrimitive.Sha256Native();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Sha256 && !useNativeCrypto)
                return CryptoPrimitive.Sha256Managed();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Sha512 && useNativeCrypto)
                return CryptoPrimitive.Sha512Native();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Sha512 && !useNativeCrypto)
                return CryptoPrimitive.Sha512Managed();

            else if (cryptoPrimitive == CryptoPrimitiveOption.HmacSha256)
                return CryptoPrimitive.HmacSha256();
            else if (cryptoPrimitive == CryptoPrimitiveOption.HmacSha512)
                return CryptoPrimitive.HmacSha512();

            else
                throw new Exception("Unknown crypto primitive: " + cryptoPrimitive);
        }
        private static HashAlgorithm GetHashAlgorithm()
        {
            if ((hashAlgorithm == HashAlgorithmOption.Default || hashAlgorithm == HashAlgorithmOption.Sha512) && useNativeCrypto)
                return new SHA512Cng();
            else if ((hashAlgorithm == HashAlgorithmOption.Default || hashAlgorithm == HashAlgorithmOption.Sha512) && !useNativeCrypto)
                return new SHA512Managed();
            else if (hashAlgorithm == HashAlgorithmOption.Sha256 && useNativeCrypto)
                return new SHA256Cng();
            else if (hashAlgorithm == HashAlgorithmOption.Sha256 && !useNativeCrypto)
                return new SHA256Managed();

            else
                throw new Exception("Unknown hash algorithm: " + hashAlgorithm);
        }
        private static Func<byte[]> GetEntropyGetter()
        {
            if (nonDeterministic)
                return CheapEntropy.Get16;
            else
                return CheapEntropy.GetNull;
        }

        private static int GetKeysizeBytesForCryptoPrimitive()
        {
            return GetCryptoPrimitive().KeySizeBytes;
        }

        private static bool ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLower().Trim();
                if (arg.StartsWith("-") || arg.StartsWith("--") || arg.StartsWith("/"))
                    arg = arg.Replace("--", "").Replace("-", "").Replace("/", "");

                if (arg == "c" || arg == "bytecount")
                {
                    if (!Int64.TryParse(args[i + 1].Trim(), out byteCount))
                    {
                        Con.WriteLine("Unable to parse number '{0}' for 'byteCount' option.", args[i + 1]);
                        return false;
                    }
                    if (byteCount <= 0)
                    {
                        Con.WriteLine("ByteCount option '{0}' must be at least 1.", args[i + 1]);
                        return false;
                    }
                    i++;
                }
                else if (arg == "s" || arg == "seed")
                {
                    seed = args[i + 1];
                    i++;
                }
                else if (arg == "s" || arg == "seed")
                {
                    seed = args[i + 1];
                    i++;
                }
                else if (arg == "outstyle")
                {
                    if (!Enum.TryParse<OutputStyle>(args[i + 1], true, out outputStyle))
                    {
                        Con.WriteLine("Unknown 'outStyle' option '{0}'.", args[i + 1]);
                        return false;
                    }
                    i++;
                }
                else if (arg == "o" || arg == "outfile")
                {
                    outFile = args[i + 1];
                    i++;
                }
                else if (arg == "outstdout")
                {
                    outFile = "";       // Empty string = stdout.
                    i++;
                }
                else if (arg == "outnull")
                {
                    outFile = null;     // Null = null output.
                    i++;
                }
                else if (arg == "q" || arg == "quiet")
                {
                    quiet = true;
                }
                else if (arg == "nd" || arg == "nondeterministic")
                {
                    nonDeterministic = true;
                }
                else if (arg == "n" || arg == "native")
                {
                    useNativeCrypto = true;
                }
                else if (arg == "cp" || arg == "cryptoprimitive")
                {
                    if (!Enum.TryParse<CryptoPrimitiveOption>(args[i + 1], true, out cryptoPrimitive))
                    {
                        Con.WriteLine("Unknown 'cryptoPrimitive' option '{0}'.", args[i + 1]);
                        return false;
                    }
                    i++;
                }
                else if (arg == "ha" || arg == "hashAlgorithm")
                {
                    if (!Enum.TryParse<HashAlgorithmOption>(args[i + 1], true, out hashAlgorithm))
                    {
                        Con.WriteLine("Unknown 'hashAlgorithm' option '{0}'.", args[i + 1]);
                        return false;
                    }
                    i++;
                }
                else if (arg == "h" || arg == "?" || arg == "help")
                {
                    PrintUsage();
                    Environment.Exit(0);
                }
                else
                {
                    Con.WriteLine("Unknown argument '{0}'.", arg);
                    return false;
                }
            }

            return true;
        }

        static void PrintUsage()
        {
            // TODO: --randomSeed -> use static local entropy
            // TODO: --generator -> pooled | cypher | cypto random | standard
            // TODO: --byteCount -1 = infinite
            // TODO: --networkSources -> include network sources
            // TODO: --poolRandom -> random pool count (default = 16)
            // TODO: --poolLinear -> linear pool count (default = 16)

            Con.WriteLine("Usage: Terninger.exe [options]");
            Con.WriteLine("  -c --byteCount nnn    Generates nnn random bytes (default: {0})", byteCount);
            Con.WriteLine("  -s --seed xxx         Initial seed material (default: null)");
            Con.WriteLine("            xxx =         [hex string|file path|any random string]");
            Con.WriteLine("  -o --outFile xxx      File to save output to (default: stdout)");
            Con.WriteLine("  --outStdout           Output to stdout (default)");
            Con.WriteLine("  --outNull             Output is discarded");
            Con.WriteLine("  --outStyle xxx        Output style (default: {0})", outputStyle);
            Con.WriteLine("             xxx =        [hex|binary]");
            Con.WriteLine("  -nd --nonDeterministic  Injects timing / memory based entropy");
            Con.WriteLine("  -n --native           Uses native crypto modules, if available");
            Con.WriteLine("  -cp --cryptoPrimitive Determines crypto primitive to use (default: AES256)");
            Con.WriteLine();
            Con.WriteLine("  -q --quiet            Does not display any status messages (default: {0})", quiet ? "hide" : "show");
            Con.WriteLine("  -h -? --help          Displays this message ");
            Con.WriteLine("See {0} for more information", TerningerCPRNG.Website);
        }

    }
}
