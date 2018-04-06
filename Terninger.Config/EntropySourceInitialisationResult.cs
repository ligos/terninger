using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Config
{
    // This assembly contains classes to assist configuring Terninger via config files and improving auto initialisation experience.

    public class EntropySourceInitialisationResult
    {
        public static EntropySourceInitialisationResult Successful() => new EntropySourceInitialisationResult(EntropySourceInitialisationReason.Successful, null);
        public static EntropySourceInitialisationResult Failed(EntropySourceInitialisationReason r) => new EntropySourceInitialisationResult(r, null);
        public static EntropySourceInitialisationResult Failed(EntropySourceInitialisationReason r, string s) => new EntropySourceInitialisationResult(r, new Exception(s));
        public static EntropySourceInitialisationResult Failed(EntropySourceInitialisationReason r, Exception e) => new EntropySourceInitialisationResult(r, e);

        private EntropySourceInitialisationResult(EntropySourceInitialisationReason r, Exception e)
        {
            this.Reason = r;
            this.Exception = e;
        }
        public readonly EntropySourceInitialisationReason Reason;
        public readonly Exception Exception;
        public bool IsSuccessful => this.Reason == EntropySourceInitialisationReason.Successful;
    }

    public enum EntropySourceInitialisationReason
    {
        Successful = 0,
        Failure = 1,

        NotSupported = 64,
        MissingHardware = 65,
        DisabledByConfig = 66,
        NoPermission = 67,
        InvalidConfig = 68,

        PendingUserPermission = 128,
        PendingUserConfig = 129,
    }
}
