using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger
{
    public class TerningerCPRNG
    {
        // TODO: a way to select your entropy sources & state
        public static Task<TerningerCPRNG> CreateStrictFortuna() { throw new NotImplementedException(); }
        // TODO: a way to select your entropy sources & state & other config
        public static Task<TerningerCPRNG> CreateRelaxedTerninger() { throw new NotImplementedException(); }
        // TODO: a way to select your entropy sources & state & other config
        public static Task<TerningerCPRNG> CreateEnhancedTerninger() { throw new NotImplementedException(); }


        public byte[] GetBytes(int count) { throw new NotImplementedException(); }
        public void GetBytes(byte[] toFill) { throw new NotImplementedException(); }
    }
}
