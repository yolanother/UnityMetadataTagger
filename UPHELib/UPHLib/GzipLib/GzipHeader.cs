using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UPHLib.GzipLib {
    /**
     *
     * Offset   Length   Contents
     * 0      2 bytes  magic header  0x1f, 0x8b (\037 \213)
  2      1 byte   compression method
                     0: store (copied)
                     1: compress
                     2: pack
                     3: lzh
                     4..7: reserved
                     8: deflate
  3      1 byte   flags
                     bit 0 set: file probably ascii text
                     bit 1 set: continuation of multi-part gzip file, part number present
                     bit 2 set: extra field present
                     bit 3 set: original file name present
                     bit 4 set: file comment present
                     bit 5 set: file is encrypted, encryption header present
                     bit 6,7:   reserved
  4      4 bytes  file modification time in Unix format
  8      1 byte   extra flags (depend on compression method)
  9      1 byte   OS type
[
         2 bytes  optional part number (second part=1)
]?
[
         2 bytes  optional extra field length (e)
        (e)bytes  optional extra field
]?
[
           bytes  optional original file name, zero terminated
]?
[
           bytes  optional file comment, zero terminated
]?
[
        12 bytes  optional encryption header
]?
           bytes  compressed data
         4 bytes  crc32
         4 bytes  uncompressed input size modulo 2^32
         **/
    public class GzipHeader {
        byte[] magic = new byte[2];
        public CompressionMethod compressionMethod;
        public GzipHeaderFlags flags;
        byte[] fileModification = new byte[4];
        byte extraFlags;
        public OsType osType;
        byte[] optionalPartNumber = new byte[2];
        byte[] optionalExtraFieldLength = new byte[2];
        byte[] optionalExtraField = new byte[0];
        string originalFileName = "";
        string fileComment = "";
        byte[] encryptionHeader = new byte[12];
        byte[] crc32 = new byte[4];
        byte[] uncompressedInputSize = new byte[4];
        public long headerLength;
        short maybeEncoding;

        public short OptionalExtraFieldLength {
            get {
                if ((flags & GzipHeaderFlags.ExtraFieldPresent) > 0) {
                    return BitConverter.ToInt16(optionalExtraFieldLength, 0);
                }
                return 0;
            }
            set {
                flags |= GzipHeaderFlags.ExtraFieldPresent;
                optionalExtraFieldLength = BitConverter.GetBytes(value);
            }
        }

        public string FileComment {
            get {
                return fileComment;
            }
            set {
                fileComment = value;
            }
        }

        public string ExtraFieldAsString {
            get {
                if (optionalExtraField.Length == 0 || optionalExtraField.Length < 6) return "";
                return Encoding.UTF8.GetString(optionalExtraField, 4, optionalExtraField.Length - 4);
            }
            set {
                byte[] encoded = Encoding.UTF8.GetBytes(value);
                optionalExtraField = new byte[encoded.Length + 4];
                Array.Copy(encoded, 0, optionalExtraField, 4, encoded.Length);
                // Two magic bytes that I'm not sure of the meaning of. I'm assuming character encoding?
                // They appear to be thesame for all packages I've looked at so far.
                optionalExtraField[0] = 0x41;
                optionalExtraField[1] = 0x24;
                Array.Copy(BitConverter.GetBytes(optionalExtraField.Length - 4), 0, optionalExtraField, 2, 2);
            }
        }

        private void writeShort(Stream stream, short s) {
            stream.Write(BitConverter.GetBytes(s));
        }

        private short readShort(Stream stream) {
            byte[] buf = new byte[2];
            stream.Read(buf, 0, 2);
            return BitConverter.ToInt16(buf, 0);
        }

        private string readString(Stream stream, int length) {
            byte[] buf = new byte[length];
            stream.Read(buf, 0, length);
            return Encoding.UTF8.GetString(buf);
        }

        public GzipHeader(string file) {
            using (Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                byte[] buffer = new byte[2048];
                int read;
                read = stream.Read(magic, 0, 2);
                compressionMethod = (CompressionMethod)stream.ReadByte();
                flags = (GzipHeaderFlags)stream.ReadByte();
                stream.Read(fileModification, 0, 4);
                extraFlags = (byte)stream.ReadByte();
                osType = (OsType)stream.ReadByte();
                if ((flags & GzipHeaderFlags.Crc) > 0) {
                    stream.Read(optionalPartNumber, 0, 2);
                }
                if ((flags & GzipHeaderFlags.ExtraFieldPresent) > 0) {
                    stream.Read(optionalExtraFieldLength, 0, 2);
                    optionalExtraField = new byte[OptionalExtraFieldLength];
                    stream.Read(optionalExtraField, 0, OptionalExtraFieldLength);
                }
                if ((flags & GzipHeaderFlags.OriginalFileNamePresent) > 0) {
                    originalFileName = ReadNullTerminatedString(stream);
                }
                if ((flags & GzipHeaderFlags.FileCommentPresent) > 0) {
                    short fullCommentSectionLen = readShort(stream);
                    maybeEncoding = readShort(stream);
                    short commentSectionLength = readShort(stream);
                    fileComment = readString(stream, commentSectionLength);
                }
                if ((flags & GzipHeaderFlags.FileEncrypted) > 0) {
                    stream.Read(encryptionHeader, 0, 12);
                }
                headerLength = stream.Position;
            }
        }

        private byte FlagByte {
            get {
                byte flagByte = (byte)flags;
                flagByte |= (byte)(FileComment.Length > 0 ? flags | GzipHeaderFlags.FileCommentPresent : flags & ~GzipHeaderFlags.FileCommentPresent);
                flagByte |= (byte)(optionalExtraField.Length > 0 ? flags | GzipHeaderFlags.ExtraFieldPresent : flags & ~GzipHeaderFlags.ExtraFieldPresent);
                return flagByte;
            }
        }

        private string ReadNullTerminatedString(Stream stream) {
            List<byte> list = new List<byte>();
            int b;
            while ((b = stream.ReadByte()) > 0) {
                list.Add((byte)b);
            }
            return Encoding.UTF8.GetString(list.ToArray());
        }

        public override string ToString() {
            string text = "";

            return text;
        }

        internal void Write(Stream stream) {
            stream.Write(magic, 0, 2);
            stream.WriteByte((byte)compressionMethod);
            stream.WriteByte(FlagByte);
            stream.Write(fileModification, 0, 4);
            stream.WriteByte((byte)extraFlags);
            stream.WriteByte((byte)osType);
            if ((flags & GzipHeaderFlags.Crc) > 0) {
                stream.Write(optionalPartNumber, 0, 2);
            }
            if ((flags & GzipHeaderFlags.ExtraFieldPresent) > 0) {
                stream.Write(BitConverter.GetBytes(optionalExtraField.Length), 0, 2);
                stream.Write(optionalExtraField);
            }
            if ((flags & GzipHeaderFlags.OriginalFileNamePresent) > 0) {
                stream.Write(Encoding.UTF8.GetBytes(originalFileName));
                stream.WriteByte(0);
            }
            if (FileComment.Length > 0) {
                byte[] comments = Encoding.UTF8.GetBytes(FileComment);
                writeShort(stream, (short)(comments.Length + 4));
                writeShort(stream, 9281);
                writeShort(stream, (short)comments.Length);
                stream.Write(comments);
            }
            if ((flags & GzipHeaderFlags.FileEncrypted) > 0) {
                stream.Read(encryptionHeader, 0, 12);
            }
        }
    }
}
