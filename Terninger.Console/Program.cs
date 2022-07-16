using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.CryptoPrimitives;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;
using MurrayGrant.Terninger.EntropySources.Network;
using MurrayGrant.Terninger.LibLog;

using Rand = System.Random;
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
        static ManagedOrNative managedOrNativeCrypto = ManagedOrNative.Default;

        static int linearPools = 20;
        static int randomPools = 12;
        static bool includeNetworkSources = false;         // If true, includes network entropy sources. This will make network calls.

        static string configFile = "Terninger.Config.json";

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
        public enum ManagedOrNative
        {
            Default = 0,
            Managed,
            Native,
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
            var (outStream, outName) = GetOutputStream();
            var outputWriter = GetOutputWriter();
            var generatorDetails = CreateRandomGenerator();
            if (!quiet)
            {
                _Logger.Info("Generating {0:N0} random bytes.", byteCount <= 0 ? "∞" : byteCount.ToString("N0"));
                _Logger.Info("Source: {0}", generatorDetails.Description);
                if (!String.IsNullOrEmpty(generatorDetails.ExtraDescription))
                    _Logger.Debug("    " + generatorDetails.ExtraDescription);
                _Logger.Debug("Seed source: {0}", generatorDetails.SeedDescription);
                _Logger.Debug("Output target: {0}, style {1}", outName, outputStyle);
                if (generatorDetails.ErrorLoadingConfig != null)
                {
                    _Logger.Warn("Error loading config file '{0}'; using defaults.", configFile);
                    _Logger.Warn("  {0} - {1}", generatorDetails.ErrorLoadingConfig.GetType().Name, generatorDetails.ErrorLoadingConfig.Message);
                }
                if (outFile == "")
                    // Stdio output needs extra line here
                    Con.WriteLine();
            }

            long generatedBytes = 0L;
            var rng = generatorDetails.Generator;
            var sw = Stopwatch.StartNew();      // Start the clock for a basic measure of performance.
            generatorDetails.WaitForGeneratorReady();
            var nextStatusUpdate = sw.Elapsed.Add(StatusUpdatePeriod);
            using (outStream)
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

        private static (byte[] seed, string description) DeriveSeed()
        {
            var seedLength = GetKeysizeBytesForCryptoPrimitive();
            var sha512 = SHA512.Create();
            if (String.IsNullOrEmpty(seed))
                // No seed provided: generate one!
                return (
                        sha512.ComputeHash(
                            StaticLocalEntropy.Get32().GetAwaiter().GetResult().Concat(CheapEntropy.Get32()).ToArray()
                        ).EnsureArraySize(seedLength)
                        , "System environment."
                );
            if (seed.IsHexString() && seed.Length == seedLength * 2)
                // A hex string of required bytes.
                return (seed.ParseFromHexString(), $"{seedLength} byte hex seed.");
            else if (File.Exists(seed))
            {
                // A file reference: get the SHA512 hash of it as a seed.
                using (var stream = new FileStream(seed, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024))
                    return (
                            sha512.ComputeHash(stream).EnsureArraySize(seedLength), 
                            "SHA512 hash of file."
                    );
            }
            else
                // Assume a random set of characters: get the SHA512 hash of the UTF8 string as a seed.
                return (
                        sha512.ComputeHash(Encoding.UTF8.GetBytes(seed)).EnsureArraySize(seedLength),
                        "SHA512 hash of random string / password / passphrase."
                );
        }

        private static (Stream stream, string name) GetOutputStream()
        {
            if (outFile == null)
                // Null output (mostly for benchmarking).
                return (Stream.Null, "Null stream.");
            else if (outFile == "")
                // Standard output.
                return (Con.OpenStandardOutput(OutBufferSize), "Standard Output.");
            else
                // File output.
                return (new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None, OutBufferSize), outFile);
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
            var (config, configException) = TryLoadConfig();
            var result = new GeneratorAndDescription();
            if (generatorType == Generator.StockRandom)
            {
                result.Description = "deterministic PRNG - " + typeof(Rand).Namespace + "." + typeof(Rand).Name;
                var (seed, description) = DeriveSeed();
                result.SeedDescription = description;
                result.Generator = new StandardRandomWrapperGenerator(new Rand(BitConverter.ToInt32(seed, 0)));
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
                var (seed, description) = DeriveSeed();
                result.Description = $"{(nonDeterministic ? "non-" : "")}deterministic PRNG - " + typeof(CypherBasedPrngGenerator).Namespace + "." + typeof(CypherBasedPrngGenerator).Name;
                result.ExtraDescription = $"Using crypto primitive: {cryptoPrimitive}, hash: {hashAlgorithm}";
                result.SeedDescription = description;
                result.Generator = CypherBasedPrngGenerator.Create(key: seed, cryptoPrimitive: primitive, hashAlgorithm: hash, initialCounter: counter, additionalEntropyGetter: entropyGetter);
                result.WaitForGeneratorReady = () => { };
                result.WaitForGeneratorStopped = () => { };
            }
            else if (generatorType == Generator.TerningerPooled)
            {
                var (seed, description) = DeriveSeed();
                result.SeedDescription = description;

                // Accumulator.
                var accKey = SHA512.Create().ComputeHash(
                    StaticLocalEntropy.Get32().GetAwaiter().GetResult().Concat(CheapEntropy.Get32()).ToArray()
                ).EnsureArraySize(32);
                var accPrng = CypherBasedPrngGenerator.Create(accKey, CryptoPrimitive.Aes256(), SHA512.Create());
                var acc = new EntropyAccumulator(linearPools, randomPools, accPrng, SHA512.Create);

                // Generator.
                var primitive = GetCryptoPrimitive();
                var hash = GetHashAlgorithm();
                var genPrng = CypherBasedPrngGenerator.Create(new byte[32], primitive, hash);
                IEnumerable<IEntropySource> sources = new IEntropySource[] {
                    new UserSuppliedSource(seed),
                    new CurrentTimeSource(),
                    new TimerSource(),
                    new GCMemorySource(),
                    new CryptoRandomSource(config?.EntropySources?.CryptoRandom),
                    new NetworkStatsSource(config?.EntropySources?.NetworkStats),
                    new ProcessStatsSource(config?.EntropySources?.ProcessStats),
                };
                if (includeNetworkSources)
                {
                    var userAgent = NetworkSources.UserAgent(config?.NetworkUserAgentIdentifier ?? "unconfigured-consoleapp");
                    sources = sources.Concat(new IEntropySource[] {
                            new PingStatsSource(),
                            new ExternalWebContentSource(),
                            new AnuExternalRandomSource(userAgent, config?.EntropySources?.AnuExternal),
                            new BeaconNistExternalRandomSource(userAgent, config?.EntropySources?.BeaconNistExternal),
                            new HotbitsExternalRandomSource(userAgent, config?.EntropySources?.HotbitsExternal),
                            new QrngEthzChExternalRandomSource(userAgent, config?.EntropySources?.QrngEthzChExternal),
                            new RandomNumbersInfoExternalRandomSource(userAgent, config?.EntropySources?.RandomNumbersInfoExternal),
                            new RandomOrgExternalRandomSource(userAgent, config?.EntropySources?.RandomOrgExternal),
                        });
                }
                // As the pooled generator will be churning out entropy as fast as it can, we increase the reseed rate by polling faster and forcing reseeds more frequently.
                var generatorConfig = new PooledEntropyCprngGenerator.PooledGeneratorConfig()
                {
                    MaximumBytesGeneratedBeforeReseed = Int32.MaxValue,
                    PollWaitTimeInNormalPriority = TimeSpan.FromSeconds(1),
                    EntropyToTriggerReseedInNormalPriority = 64,
                };
                var generator = new PooledEntropyCprngGenerator(sources, acc, genPrng, generatorConfig);
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
            result.ErrorLoadingConfig = configException;
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
            public Exception ErrorLoadingConfig { get; set; }
        }

        private static ICryptoPrimitive GetCryptoPrimitive()
        {
            if (cryptoPrimitive == CryptoPrimitiveOption.Default || cryptoPrimitive == CryptoPrimitiveOption.Aes256)
                return GetAes256CryptoPrimitive();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Aes128)
                return GetAes128CryptoPrimitive();

            else if (cryptoPrimitive == CryptoPrimitiveOption.Sha256)
                return GetSha256CryptoPrimitive();
            else if (cryptoPrimitive == CryptoPrimitiveOption.Sha512)
                return GetSha512CryptoPrimitive();

            else if (cryptoPrimitive == CryptoPrimitiveOption.HmacSha256)
                return CryptoPrimitive.HmacSha256();
            else if (cryptoPrimitive == CryptoPrimitiveOption.HmacSha512)
                return CryptoPrimitive.HmacSha512();

            else
                throw new Exception("Unknown crypto primitive: " + cryptoPrimitive);
        }
        private static HashAlgorithm GetHashAlgorithm()
        {
            if ((hashAlgorithm == HashAlgorithmOption.Default || hashAlgorithm == HashAlgorithmOption.Sha512))
                return GetSha512HashAlgorithm();
            else if (hashAlgorithm == HashAlgorithmOption.Sha256)
                return GetSha256HashAlgorithm();

            else
                throw new Exception("Unknown hash algorithm: " + hashAlgorithm);
        }
        private static ICryptoPrimitive GetAes256CryptoPrimitive() => managedOrNativeCrypto == ManagedOrNative.Managed ? CryptoPrimitive.Aes256Managed()
                                                                    : managedOrNativeCrypto == ManagedOrNative.Native ? new BlockCypherCryptoPrimitive(new AesCryptoServiceProvider() { KeySize = 256 })
                                                                    : CryptoPrimitive.Aes256();
        private static ICryptoPrimitive GetAes128CryptoPrimitive() => managedOrNativeCrypto == ManagedOrNative.Managed ? CryptoPrimitive.Aes128Managed()
                                                                    : managedOrNativeCrypto == ManagedOrNative.Native ? new BlockCypherCryptoPrimitive(new AesCryptoServiceProvider() { KeySize = 128 })
                                                                    : CryptoPrimitive.Aes128();
        private static ICryptoPrimitive GetSha256CryptoPrimitive() => managedOrNativeCrypto == ManagedOrNative.Managed ? CryptoPrimitive.Sha256Managed()
                                                                    : managedOrNativeCrypto == ManagedOrNative.Native ? new HashCryptoPrimitive(new SHA256CryptoServiceProvider())
                                                                    : CryptoPrimitive.Sha256();
        private static ICryptoPrimitive GetSha512CryptoPrimitive() => managedOrNativeCrypto == ManagedOrNative.Managed ? CryptoPrimitive.Sha512Managed()
                                                                    : managedOrNativeCrypto == ManagedOrNative.Native ? new HashCryptoPrimitive(new SHA512CryptoServiceProvider())
                                                                    : CryptoPrimitive.Sha512();
        private static HashAlgorithm GetSha256HashAlgorithm() => managedOrNativeCrypto == ManagedOrNative.Managed ? new SHA256Managed()
                                                               : managedOrNativeCrypto == ManagedOrNative.Native ? new SHA256CryptoServiceProvider()
                                                               : SHA256.Create();
        private static HashAlgorithm GetSha512HashAlgorithm() => managedOrNativeCrypto == ManagedOrNative.Managed ? new SHA512Managed()
                                                               : managedOrNativeCrypto == ManagedOrNative.Native ? new SHA512CryptoServiceProvider()
                                                               : SHA512.Create();

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

        private static (TerningerConfiguration config, Exception ex) TryLoadConfig()
        {
            try
            {
                var configJson = File.ReadAllText(configFile);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<TerningerConfiguration>(configJson);
                return (config, null);
            }
            catch (Exception ex)
            {
                return (null, ex);
            }
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
                else if (arg == "native")
                {
                    managedOrNativeCrypto = ManagedOrNative.Native;
                }
                else if (arg == "managed")
                {
                    managedOrNativeCrypto = ManagedOrNative.Managed;
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
                else if (arg == "config")
                {
                    configFile = args[i+1];
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
            Con.WriteLine("                          Use zero for infinite (CTRL+C to end)");
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
            Con.WriteLine("  --poolLinear nn       Number of linear pools (default: 20)");
            Con.WriteLine("  --poolRandom nn       Number of random pools (default: 12)");
            Con.WriteLine("  -nd --nonDeterministic  Injects timing / memory based entropy");
            Con.WriteLine("  --managed             Uses managed crypto modules (default: auto)");
            Con.WriteLine("  --native              Uses native crypto modules (default: auto)");
            Con.WriteLine("  -cp --cryptoPrimitive Determines crypto primitive to use (default: AES256)");
            Con.WriteLine("  -ha --hashAlgorithm   Determines hash algorithm to use (default: SHA512)");
            Con.WriteLine("  --config              Path to config file (default: Terninger.Config.json)");
            Con.WriteLine();
            Con.WriteLine("  -q --quiet            Does not display any status messages (default: {0})", quiet ? "hide" : "show");
            Con.WriteLine("  --debug               Show debug messages");
            Con.WriteLine("  --trace               Show low level trace messages");

            Con.WriteLine("  -h -? --help          Displays this message ");
            Con.WriteLine("See {0} for more information", RandomGenerator.Website);
        }

    }
}
