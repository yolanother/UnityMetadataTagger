using System;
using System.Collections.Generic;
using System.Text;

namespace UPHLib.GzipLib {
    public enum OsType : byte {
        Fat = 0x00,
        Amiga = 0x01,
        VMS = 0x02,
        Unix = 0x03,
        VMCMS = 0x04,
        AtariTOS = 0x05,
        HPFS = 0x06,
        Macintosh = 0x07,
        ZSystem = 0x08,
        CPM = 0x09,
        TOPS20 = 0x0a,
        NTFS = 0x0b,
        QDOS = 0x0c,
        AcornRISCOS = 0x0d,
        Unknown = 0xff
    }
}
