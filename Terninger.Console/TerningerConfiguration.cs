using System;

namespace MurrayGrant.Terninger.Console
{
    public class TerningerConfiguration
    {
        public string NetworkUserAgentIdentifier { get; set; }

        public PersistentStateConfiguration PersistentState { get; set; }

        public EntropySourceConfiguration EntropySources { get; set; }

        public Random.PooledEntropyCprngGenerator.PooledGeneratorConfig TerningerPooledGeneratorConfig { get; set; }

        public TimeSpan TimeToWaitForFirstSeed { get; set; }
    }

    public class PersistentStateConfiguration
    {
        public bool Enabled { get; set; } = true;
        public string Path { get; set; }
    }

    public class EntropySourceConfiguration
    {
        public EntropySources.Local.CryptoRandomSource.Configuration CryptoRandom { get; set; }

        public EntropySources.Local.ProcessStatsSource.Configuration ProcessStats { get; set; }

        public EntropySources.Local.NetworkStatsSource.Configuration NetworkStats { get; set; }

        public EntropySources.Network.ExternalWebContentSource.Configuration ExternalWebContent { get; set; }

        public EntropySources.Network.PingStatsSource.Configuration PingStats { get; set; }

        public EntropySources.Network.AnuExternalRandomSource.Configuration AnuExternal { get; set; }

        public EntropySources.Network.BeaconNistExternalRandomSource.Configuration BeaconNistExternal { get; set; }

        public EntropySources.Network.DrandExternalRandomSource.Configuration DrandExternal { get; set; }

        public EntropySources.Network.HotbitsExternalRandomSource.Configuration HotbitsExternal { get; set; }

        public EntropySources.Network.QrngEthzChExternalRandomSource.Configuration QrngEthzChExternal { get; set; }

        public EntropySources.Network.RandomNumbersInfoExternalRandomSource.Configuration RandomNumbersInfoExternal { get; set; }

        public EntropySources.Network.RandomOrgExternalRandomSource.Configuration RandomOrgExternal { get; set; }
    }
}
