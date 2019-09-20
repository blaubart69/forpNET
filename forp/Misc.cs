using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace forp
{
    public static class Misc
    {
        [ThreadStatic]
        static StringBuilder StrFormatByteSizeBuilder;
        const int StrFormatByteSizeBufferLen = 64;
        public static string StrFormatByteSize(long Filesize)
        {
            if (StrFormatByteSizeBuilder == null)
            {
                StrFormatByteSizeBuilder = new StringBuilder(StrFormatByteSizeBufferLen);
            }

            Spi.Native.StrFormatByteSize(Filesize, StrFormatByteSizeBuilder, StrFormatByteSizeBufferLen);
            return StrFormatByteSizeBuilder.ToString();
        }
        public static async Task ReadLinesAsync(TextReader input, Action<string> onLine)
        {
            string line;
            while ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                onLine(line);
            }
        }
    }
}
