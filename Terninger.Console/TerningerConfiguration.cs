using System;

namespace MurrayGrant.Terninger.Console
{
    public class TerningerConfiguration
    {
        public EntropySourceConfiguration EntropySources { get; set; }
    }

    public class EntropySourceConfiguration
    {
        public EntropySources.Local.CryptoRandomSource.Configuration CryptoRandom { get; set; }

        public EntropySources.Local.ProcessStatsSource.Configuration ProcessStats { get; set; }

        public EntropySources.Local.NetworkStatsSource.Configuration NetworkStats { get; set; }
    }
}
