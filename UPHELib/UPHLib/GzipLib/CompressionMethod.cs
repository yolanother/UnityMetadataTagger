using System;
using System.Collections.Generic;
using System.Text;

namespace UPHLib.GzipLib {
    [Flags]
    public enum CompressionMethod : byte {
        Store = 0x00,
        Compress = 0x01,
        Pack = 0x02,
        Lzh = 0x04,
        deflate = 0x08
    }
}
