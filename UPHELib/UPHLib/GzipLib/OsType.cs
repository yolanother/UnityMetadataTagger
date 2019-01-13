/*
 * The MIT License(MIT)
 *
 * Copyright(c) 2012-2017 Aaron Jackson
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

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
