﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MurrayGrant.Terninger.Console
{
    public class TerningerConfiguration
    {
        public EntropySourceConfiguration EntropySources { get; set; }
    }

    public class EntropySourceConfiguration
    {
        public EntropySources.Local.CryptoRandomSource.Configuration CryptoRandom { get; set; }
    }
}
