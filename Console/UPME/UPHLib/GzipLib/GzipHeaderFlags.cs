using System;
using System.Collections.Generic;
using System.Text;

namespace UPHLib.GzipLib {
    [Flags]
    public enum GzipHeaderFlags : byte {
        Ascii = 0x01,
        Crc = 0x02,
        ExtraFieldPresent = 0x04,
        OriginalFileNamePresent = 0x08,
        FileCommentPresent = 0x10,
        FileEncrypted = 0x20
    }
}
