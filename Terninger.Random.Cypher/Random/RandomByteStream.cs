using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MurrayGrant.Terninger.Random
{
    /// <summary>
    /// Produces a stream of random bytes of arbitrary length.
    /// </summary>
    public class RandomByteStream : Stream
    {
        private readonly IRandomNumberGenerator _Source;

        public RandomByteStream(IRandomNumberGenerator source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _Source = source;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => Int64.MaxValue;      // No effective limit to size.

        public override long Position
        {
            get => 0L;
            set => Seek(value, SeekOrigin.Begin);       // Will throw.
        }

        public override void Flush()
        {
            // Cannot write, so No Op.
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // TODO: Handle the case where we read more than the maximum number of bytes in one call.
            _Source.FillWithRandomBytes(buffer, offset, count);
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Seek() is not valid on a RandomByteStream.");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("SetLength() is not valid on a RandomByteStream.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Write() is not valid on a RandomByteStream.");
        }
    }
}
