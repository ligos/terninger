using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.EntropySources;

namespace MurrayGrant.Terninger.Config
{
    /// <summary>
    /// Base class for entropy config which does not Interface for entropy sources to access configuration variables.
    /// </summary>
    public static class EntropySourceConfigExtensions
    {
        public static bool? IsTruthy(this IEntropySourceConfig config, string name)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var result = config.Get(name);
            if (String.IsNullOrWhiteSpace(result)) return null;        // Blank or null is absent, which means revert to a default behaviour.

            // Falsey values are 'N', 'No', 'False', 0.
            if ((result.Length == 1 && result.StartsWith("N", StringComparison.OrdinalIgnoreCase))
                || (result.Length == 1 && result[0] == '0')
                || (result.Length > 1 && result.Equals("No", StringComparison.OrdinalIgnoreCase))
                || (result.Length > 1 && result.Equals("False", StringComparison.OrdinalIgnoreCase))
                )
                return false;

            // Truthy values are 'Y', 'Yes', 'True', 1.
            // PERF: this is just here do we don't try to parse everything to detect true values.
            if ((result.Length == 1 && result.StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                || (result.Length == 1 && result[0] == '1')
                || (result.Length > 1 && result.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                || (result.Length > 1 && result.Equals("True", StringComparison.OrdinalIgnoreCase))
                )
                return true;

            // Any number which parses to 0 is considered falsy.
            if (Double.TryParse(result, out double d))
            {
                if (d == 0 || Double.IsNaN(d))
                    return false;
            }

            // Everything else is considered truthy.
            return true;
        }

        public static bool TryParseAndSetInt32(this IEntropySourceConfig config, string name, ref int value)
        {
            var result = config.Get(name);
            if (!String.IsNullOrWhiteSpace(result))
                return Int32.TryParse(result, out value);
            else
                return false;
        }
        public static bool TryParseAndSetInt64(this IEntropySourceConfig config, string name, ref long value)
        {
            var result = config.Get(name);
            if (!String.IsNullOrWhiteSpace(result))
                return Int64.TryParse(result, out value);
            else
                return false;
        }
        public static bool TryParseAndSetFloat(this IEntropySourceConfig config, string name, ref float value)
        {
            var result = config.Get(name);
            if (!String.IsNullOrWhiteSpace(result))
                return Single.TryParse(result, out value);
            else
                return false;
        }
        public static bool TryParseAndSetDouble(this IEntropySourceConfig config, string name, ref double value)
        {
            var result = config.Get(name);
            if (!String.IsNullOrWhiteSpace(result))
                return Double.TryParse(result, out value);
            else
                return false;
        }
        public static bool TryParseAndSetDecimal(this IEntropySourceConfig config, string name, ref decimal value)
        {
            var result = config.Get(name);
            if (!String.IsNullOrWhiteSpace(result))
                return Decimal.TryParse(result, out value);
            else
                return false;
        }
    }
}
