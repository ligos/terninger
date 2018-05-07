﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.CryptoPrimitives;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;
using MurrayGrant.Terninger.EntropySources.Network;
using MurrayGrant.Terninger.LibLog;

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

        static Generator generatorType = Generator.TerningerCypher;
        static bool nonDeterministic = false;       // Will inject additional entropy into the generator based on timing & memory.
        static CryptoPrimitiveOption cryptoPrimitive = CryptoPrimitiveOption.Default;
        static HashAlgorithmOption hashAlgorithm = HashAlgorithmOption.Default;
        static bool useNativeCrypto = true;        // If true, will use CSP / CNG versions of the crypto primitives, if available.

        static int linearPools = 16;
        static int randomPools = 16;
        static bool includeNetworkSources = false;         // If true, includes network entropy sources. This will make network calls.

        static LogLevel _MinLogLevel = LogLevel.Info;
        static ILog _Logger;

        public enum Generator
        {
            StockRandom,
            CryptoRandom,
            TerningerCypher,
            TerningerPooled,
        }

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
        private static readonly TimeSpan StatusUpdatePeriod = TimeSpan.FromSeconds(2);

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

                // Initialise logging. 
                // In real world, your wouldn't need this as LibLog supports most major logging frameworks auto-magically.
                if (!quiet)
                    LogProvider.SetCurrentLogProvider(new ColoredConsoleLogProvider(_MinLogLevel));

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
            _Logger = LibLog.LogProvider.For<Program>();

            // Hello world!
            if (!quiet)
                _Logger.Info("Terninger CPRNG   © Murray Grant");

            // Load and initialise objects.
            var outStreamAndTarget = GetOutputStream();
            var outputWriter = GetOutputWriter();
            var generatorDetails = CreateRandomGenerator();
            if (!quiet)
            {
                _Logger.Info("Generating {0:N0} random bytes.", byteCount <= 0 ? "∞" : byteCount.ToString("N0"));
                _Logger.Info("Source: {0}", generatorDetails.Description);
                if (!String.IsNullOrEmpty(generatorDetails.ExtraDescription))
                    _Logger.Debug("    " + generatorDetails.ExtraDescription);
                _Logger.Debug("Seed source: {0}", generatorDetails.SeedDescription);
                _Logger.Debug("Output target: {0}, style {1}", outStreamAndTarget.Item2, outputStyle);
                if (outFile == "")
                    // Stdio output needs extra line here
                    Con.WriteLine();
            }

            long generatedBytes = 0L;
            var rng = generatorDetails.Generator;
            var sw = Stopwatch.StartNew();      // Start the clock for a basic measure of performance.
            generatorDetails.WaitForGeneratorReady();
            var nextStatusUpdate = sw.Elapsed.Add(StatusUpdatePeriod);
            using (var outStream = outStreamAndTarget.Item1)
            {
                long remaining = byteCount <= 0 ? Int64.MaxValue : byteCount;

                // Read and write in buffered chunks (for larger requests).
                byte[] buf = new byte[OutBufferSize];
                while (remaining > buf.Length)
                {
                    rng.FillWithRandomBytes(buf);          // Fill one buffer with randomness.
                    generatedBytes = generatedBytes + buf.Length;   // Increment counter.
                    outputWriter(outStream, buf);           // Write the buffer to out output stream.
                    if (byteCount > 0)
                        remaining = remaining - buf.Length;     // Decrement remaining (unless infinite).

                    if (_CancelSignalled) break;
                    if (!quiet 
                        && outFile != "" 
                        && generatedBytes % OneMBAsLong == 0 
                        && sw.Elapsed > nextStatusUpdate)
                    {
                        // Status updates: only if not quiet, not stdout, on MB boundaries and regular interval.
                        _Logger.Info("Generated {0:N0}MB.", generatedBytes / OneMBAsLong);
                        nextStatusUpdate = sw.Elapsed.Add(StatusUpdatePeriod);
                    }
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
            generatorDetails.WaitForGeneratorStopped();

            if (!quiet)
            {
                if (outFile == "")
                    // Stdio output needs extra line here
                    Con.WriteLine();

                Con.WriteLine();
                _Logger.Info("Generated {0:N0} bytes OK.", generatedBytes);
                _Logger.Debug("{0:N0} bytes generated in {1:N2} seconds ({2:N2}MB / sec)", generatedBytes, sw.Elapsed.TotalSeconds, ((double)generatedBytes / OneMBAsDouble) / sw.Elapsed.TotalSeconds);
            }
        }

        private static Tuple<byte[], string> DeriveSeed()
        {
            var seedLength = GetKeysizeBytesForCryptoPrimitive();
            var sha512 = SHA512.Create();
            if (String.IsNullOrEmpty(seed))
                // No seed provided: generate one!
                return Tuple.Create(
                            sha512.ComputeHash(
                                StaticLocalEntropy.Get32().GetAwaiter().GetResult().Concat(
                                    CheapEntropy.Get32()
                                ).ToArray()
                            ).EnsureArraySize(seedLength)
                            , "System environment."
                        );
            if (seed.IsHexString() && seed.Length == seedLength * 2)
                // A hex string of required bytes.
                return Tuple.Create(seed.ParseFromHexString(), $"{seedLength} byte hex seed.");
            else if (File.Exists(seed))
            {
                // A file reference: get the SHA512 hash of it as a seed.
                using (var stream = new FileStream(seed, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024))
                    return Tuple.Create(
                            sha512.ComputeHash(stream).EnsureArraySize(seedLength), 
                            "SHA512 hash of file."
                        );
            }
            else
                // Assume a random set of characters: get the SHA256 hash of the UTF8 string as a seed.
                return Tuple.Create(
                        sha512.ComputeHash(Encoding.UTF8.GetBytes(seed)).EnsureArraySize(seedLength),
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

        private static GeneratorAndDescription CreateRandomGenerator()
        {
            var result = new GeneratorAndDescription();
            if (generatorType == Generator.StockRandom)
            {
                result.Description = "deterministic PRNG - " + typeof(Random).Namespace + "." + typeof(Random).Name;
                var seedAndDescription = DeriveSeed();
                result.SeedDescription = seedAndDescription.Item2;
                result.Generator = new StandardRandomWrapperGenerator(new Random(BitConverter.ToInt32(seedAndDescription.Item1, 0)));
                result.WaitForGeneratorReady = () => { };
                result.WaitForGeneratorStopped = () => { };
            }
            else if (generatorType == Generator.CryptoRandom)
            {
                result.Description = "non-deterministic CPRNG - " + typeof(RandomNumberGenerator).Namespace + "." + typeof(RandomNumberGenerator).Name;
                result.SeedDescription = "No seed required";
                result.Generator = new CryptoRandomWrapperGenerator();
                result.WaitForGeneratorReady = () => { };
                result.WaitForGeneratorStopped = () => { };
            }
            else if (generatorType == Generator.TerningerCypher)
            {
                var primitive = GetCryptoPrimitive();
                var hash = GetHashAlgorithm();
                var counter = new CypherCounter(primitive.BlockSizeBytes);
                var entropyGetter = GetEntropyGetter();
                var seedAndDescription = DeriveSeed();
                result.Description = $"{(nonDeterministic ? "non-" : "")}deterministic PRNG - " + typeof(CypherBasedPrngGenerator).Namespace + "." + typeof(CypherBasedPrngGenerator).Name;
                result.ExtraDescription = $"Using crypto primitive: {cryptoPrimitive}, hash: {hashAlgorithm}";
                result.SeedDescription = seedAndDescription.Item2;
                result.Generator = CypherBasedPrngGenerator.Create(key: seedAndDescription.Item1, cryptoPrimitive: primitive, hashAlgorithm: hash, initialCounter: counter, additionalEntropyGetter: entropyGetter);
                result.WaitForGeneratorReady = () => { };
                result.WaitForGeneratorStopped = () => { };
            }
            else if (generatorType == Generator.TerningerPooled)
            {
                var seedAndDescription = DeriveSeed();
                result.SeedDescription = seedAndDescription.Item2;

                // Accumulator.
                var accKey = SHA512.Create().ComputeHash(
                    StaticLocalEntropy.Get32().GetAwaiter().GetResult().Concat(
                        CheapEntropy.Get32()
                    ).ToArray()
                ).EnsureArraySize(32);
                var accPrng = CypherBasedPrngGenerator.Create(accKey, CryptoPrimitive.Aes256(), SHA512.Create());
                var acc = new EntropyAccumulator(linearPools, randomPools, accPrng, SHA512.Create);

                // Generator.
                var primitive = GetCryptoPrimitive();
                var hash = GetHashAlgorithm();
                var genPrng = CypherBasedPrngGenerator.Create(new byte[32], primitive, hash);
                IEnumerable<IEntropySource> sources = new IEntropySource[] {
                    new UserSuppliedSource(String.IsNullOrEmpty(seed) ? null : seedAndDescription.Item1),
                    new CurrentTimeSource(),
                    new TimerSource(),
                    new GCMemorySource(),
                    new CryptoRandomSource(),
                    new NetworkStatsSource(),
                    new ProcessStatsSource(),
                };
                if (includeNetworkSources)
                    sources = sources.Concat(new IEntropySource[] {
                            new PingStatsSource(),
                            new ExternalWebContentSource(),
                            new AnuExternalRandomSource(),
                            new BeaconNistExternalRandomSource(),
                            new HotbitsExternalRandomSource(),
                            new RandomNumbersInfoExternalRandomSource(),
                            new RandomOrgExternalRandomSource(),
                            new RandomServerExternalRandomSource(),
                        });
                var generator = new PooledEntropyCprngGenerator(sources, acc, genPrng);
                result.Generator = generator;
                result.Description = $"non-deterministic CPRNG - " + typeof(PooledEntropyCprngGenerator).Namespace + "." + typeof(PooledEntropyCprngGenerator).Name;
                result.ExtraDescription = $"Using {linearPools}+{randomPools} pools (linear+random), {sources.Count()} entropy sources, crypto primitive: {cryptoPrimitive}, hash: {hashAlgorithm}";
                result.WaitForGeneratorReady = () => {
                    generator.StartAndWaitForFirstSeed().Wait(TimeSpan.FromSeconds(60));
                };
                result.WaitForGeneratorStopped = () => {
                    generator.Stop().Wait(TimeSpan.FromSeconds(60));
                };
            }
            else
                throw new Exception("Unexpected Generator type: " + generatorType);
            return result;
        }
        private class GeneratorAndDescription
        {
            public IRandomNumberGenerator Generator { get; set; }
            public string Description { get; set; }
            public string ExtraDescription { get; set; }
            public string SeedDescription { get; set; }
            public Action WaitForGeneratorReady { get; set; }
            public Action WaitForGeneratorStopped { get; set; }
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
                    i++;
                }
                else if (arg == "g" || arg == "generator")
                {
                    if (!Enum.TryParse<Generator>(args[i + 1], true, out generatorType))
                    {
                        Con.WriteLine("Unknown 'generator' option '{0}'.", args[i + 1]);
                        return false;
                    }
                    i++;
                }
                else if (arg == "s" || arg == "seed")
                {
                    seed = args[i + 1];
                    i++;
                }
                else if (arg == "ns" || arg == "netsources")
                {
                    includeNetworkSources = true;
                }
                else if (arg == "poollinear")
                {
                    if (!Int32.TryParse(args[i + 1].Trim(), out linearPools))
                    {
                        Con.WriteLine("Unable to parse number '{0}' for 'poolLinear' option.", args[i + 1]);
                        return false;
                    }
                    i++;
                }
                else if (arg == "poolrandom")
                {
                    if (!Int32.TryParse(args[i + 1].Trim(), out randomPools))
                    {
                        Con.WriteLine("Unable to parse number '{0}' for 'poolRandom' option.", args[i + 1]);
                        return false;
                    }
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
                }
                else if (arg == "outnull")
                {
                    outFile = null;     // Null = null output.
                }
                else if (arg == "q" || arg == "quiet")
                {
                    quiet = true;
                }
                else if (arg == "debug")
                {
                    _MinLogLevel = LibLog.LogLevel.Debug;
                }
                else if (arg == "trace")
                {
                    _MinLogLevel = LibLog.LogLevel.Trace;
                }
                else if (arg == "nd" || arg == "nondeterministic")
                {
                    nonDeterministic = true;
                }
                else if (arg == "managed")
                {
                    useNativeCrypto = false;
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
            Con.WriteLine("Usage: Terninger.exe [options]");
            Con.WriteLine("  -c --byteCount nnn    Generates nnn random bytes (default: {0})", byteCount);
            Con.WriteLine("                          Use negative for infinite (CTRL+C to end)");
            Con.WriteLine();
            Con.WriteLine("  -o --outFile xxx      File to save output to (default: stdout)");
            Con.WriteLine("  --outStdout           Output to stdout (default)");
            Con.WriteLine("  --outNull             Output is discarded");
            Con.WriteLine("  --outStyle xxx        Output style (default: {0})", outputStyle);
            Con.WriteLine("             xxx =        [hex|binary]");
            Con.WriteLine();
            Con.WriteLine("  -g --generator xxx    Type of random generator (default: {0})", generatorType);
            Con.WriteLine("        StockRandom:      System.Random");
            Con.WriteLine("        CryptoRandom:     System.Security.Cryptography.RandomNumberGenerator");
            Con.WriteLine("        TerningerCypher:  Terninger.Generator.CypherBasedPrngGenerator");
            Con.WriteLine("        TerningerPooled:  Terninger.Generator.PooledEntropyCprngGenerator");
            Con.WriteLine("  -s --seed xxx         Initial seed material (default: random)");
            Con.WriteLine("            xxx =         [hex string|file path|any random string]");
            Con.WriteLine("  -ns --netSources      Include network sources for entropy (default: false)");
            Con.WriteLine("  --poolLinear nn       Number of linear pools (default: 16)");
            Con.WriteLine("  --poolRandom nn       Number of random pools (default: 16)");
            Con.WriteLine("  -nd --nonDeterministic  Injects timing / memory based entropy");
            Con.WriteLine("  --managed             Uses managed crypto modules (default: false)");
            Con.WriteLine("  -cp --cryptoPrimitive Determines crypto primitive to use (default: AES256)");
            Con.WriteLine("  -ha --hashAlgorithm   Determines hash algorithm to use (default: SHA512)");
            Con.WriteLine();
            Con.WriteLine("  -q --quiet            Does not display any status messages (default: {0})", quiet ? "hide" : "show");
            Con.WriteLine("  --debug               Show debug messages");
            Con.WriteLine("  --trace               Show low level trace messages");

            Con.WriteLine("  -h -? --help          Displays this message ");
            Con.WriteLine("See {0} for more information", TerningerCPRNG.Website);
        }

    }
}
