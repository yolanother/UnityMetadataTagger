using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using SimpleJSON;

namespace GzipComment {
    public enum OsType : byte {
        Fat         = 0x00,
        Amiga       = 0x01,
        VMS         = 0x02,
        Unix        = 0x03,
        VMCMS       = 0x04,
        AtariTOS    = 0x05,
        HPFS        = 0x06,
        Macintosh   = 0x07,
        ZSystem     = 0x08,
        CPM         = 0x09,
        TOPS20      = 0x0a,
        NTFS        = 0x0b,
        QDOS        = 0x0c,
        AcornRISCOS = 0x0d,
        Unknown     = 0xff
    }

    [Flags]
    public enum CompressionMethod : byte {
        Store       = 0x00,
        Compress    = 0x01,
        Pack        = 0x02,
        Lzh         = 0x04,
        deflate     = 0x08
    }

    [Flags]
    public enum Flags: byte {
        Ascii                   = 0x01,
        Crc                     = 0x02,
        ExtraFieldPresent       = 0x04,
        OriginalFileNamePresent = 0x08,
        FileCommentPresent      = 0x10,
        FileEncrypted           = 0x20
    }
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
        public Flags flags;
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
                if ((flags & Flags.ExtraFieldPresent) > 0) {
                    return BitConverter.ToInt16(optionalExtraFieldLength, 0);
                }
                return 0;
            } set {
                flags |= Flags.ExtraFieldPresent;
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
            } set {
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
                flags = (Flags)stream.ReadByte();
                stream.Read(fileModification, 0, 4);
                extraFlags = (byte)stream.ReadByte();
                osType = (OsType)stream.ReadByte();
                if ((flags & Flags.Crc) > 0) {
                    stream.Read(optionalPartNumber, 0, 2);
                }
                if ((flags & Flags.ExtraFieldPresent) > 0) {
                    stream.Read(optionalExtraFieldLength, 0, 2);
                    optionalExtraField = new byte[OptionalExtraFieldLength];
                    stream.Read(optionalExtraField, 0, OptionalExtraFieldLength);
                }
                if ((flags & Flags.OriginalFileNamePresent) > 0) {
                    originalFileName = ReadNullTerminatedString(stream);
                }
                if ((flags & Flags.FileCommentPresent) > 0) {
                    short fullCommentSectionLen = readShort(stream);
                    maybeEncoding = readShort(stream);
                    short commentSectionLength = readShort(stream);
                    fileComment = readString(stream, commentSectionLength);
                }
                if ((flags & Flags.FileEncrypted) > 0) {
                    stream.Read(encryptionHeader, 0, 12);
                }
                headerLength = stream.Position;
            }
        }

        private byte FlagByte {
            get {
                byte flagByte = (byte)flags;
                flagByte |= (byte)(FileComment.Length > 0 ? flags | Flags.FileCommentPresent : flags & ~Flags.FileCommentPresent);
                flagByte |= (byte)(optionalExtraField.Length > 0 ? flags | Flags.ExtraFieldPresent : flags & ~Flags.ExtraFieldPresent);
                return flagByte;
            }
        }

        private string ReadNullTerminatedString(Stream stream) {
            List<byte> list = new List<byte>();
            int b;
            while ((b = stream.ReadByte()) > 0) {
                list.Add((byte) b);
            }
            return Encoding.UTF8.GetString(list.ToArray());
        }

        public override string ToString() {
            string text = "";

            return text;
        }

        internal void Write(Stream stream) {
            stream.Write(magic, 0, 2);
            stream.WriteByte((byte) compressionMethod);
            stream.WriteByte(FlagByte);
            stream.Write(fileModification, 0, 4);
            stream.WriteByte((byte) extraFlags);
            stream.WriteByte((byte)osType);
            if ((flags & Flags.Crc) > 0) {
                stream.Write(optionalPartNumber, 0, 2);
            }
            if ((flags & Flags.ExtraFieldPresent) > 0) {
                stream.Write(BitConverter.GetBytes(optionalExtraField.Length), 0, 2);
                stream.Write(optionalExtraField);
            }
            if ((flags & Flags.OriginalFileNamePresent) > 0) {
                stream.Write(Encoding.UTF8.GetBytes(originalFileName));
                stream.WriteByte(0);
            }
            if (FileComment.Length > 0) {
                byte[] comments = Encoding.UTF8.GetBytes(FileComment);
                writeShort(stream, (short) (comments.Length + 4));
                writeShort(stream, 9281);
                writeShort(stream, (short) comments.Length);
                stream.Write(comments);
            }
            if ((flags & Flags.FileEncrypted) > 0) {
                stream.Read(encryptionHeader, 0, 12);
            }
        }
    }

    class Program {
        private static void setArg(JSONNode node, string path, string value) {
            do {
                int index = path.IndexOf('.');
                index = index > 0 ? index : path.Length;
                string key = path.Substring(0, index);
                path = path.Substring(Math.Min(path.Length, index + 1));
                if (node[key] == null) {
                    if (path.Length == 0) {
                        node.Add(key, new JSONString(value));
                    } else {
                        node.Add(key, new JSONObject());
                    }
                }
                node = node[key];

                if (path.Length == 0) {
                    node.Value = value;
                }
            } while (path.Length > 0);
        }

        static string packageArgument = "--package.";
        private static void parseArg(string arg, JSONNode package) {
            int eqIndex = arg.IndexOf("=");
            if(arg.StartsWith(packageArgument) && eqIndex >= 0) {
                string path = arg.Substring(packageArgument.Length, eqIndex - packageArgument.Length);
                if (arg.Length > eqIndex + 1) {
                    string value = arg.Substring(eqIndex + 1);
                    setArg(package, path, value);
                }
            }
        }

        private static void parseArg(string arg, string type, JSONNode package, string path) {
            if(arg.StartsWith("--" + type + "=")) {
                string value = arg.Substring(arg.IndexOf('=') + 1);
                JSONNode node = package;
                setArg(package, path, value);
            }
        }

        private static void parseArg(string arg, string type, ref string value) {
            if (arg.StartsWith("--" + type + "=")) {
                value = arg.Substring(arg.IndexOf('=') + 1);
            }
        }

        static void Main(string[] args) {
            if (args.Length == 0 || !File.Exists(args[args.Length - 1]) || args[0] == "--help" || args[0] == "-h") {
                Console.WriteLine("Usage: GzipComment (options) --file=/path/to/updated.unitypackage /path/to/file.gz");
                Console.WriteLine("  Options:");
                Console.WriteLine("    -p                   Print the file comment and quit");
                Console.WriteLine("    --version=xyz        Set the version of the package to xyz");
                Console.WriteLine("    --versionid=xyz      Set the version-id of the package to xyz");
                Console.WriteLine("    --title=\"Some App\"   Sets the title of the package");
                Console.WriteLine("    --id=xyz             Set the id of the package to xyz");
                Console.WriteLine("    --pubdate=xyz        Set the version of the package to xyz ex \"15 Sept 2018\"");
                Console.WriteLine("    --unityversion=xyz   Set the minimum unity version of the package to xyz");
                Console.WriteLine("    --categoryid=xyz     Set the category id of the package to xyz");
                Console.WriteLine("    --category=xyz       Set the version of the package to xyz ex \"Editor/Extensions\"");
                Console.WriteLine("    --publisherid=xyz    Set the publisherid of the package to xyz");
                Console.WriteLine("    --publisher=xyz      Set the version of the package to xyz");
                Console.WriteLine("    --package.(path).(to).(node)=xyz      Set the value of a custom package node.");
                Console.WriteLine("                         ex: --package.version=1.1 would match behavior of --version=1.1");
                Console.WriteLine("    --file=xyz           Set the path where the updated data will be written.");
                return;
            }
            string file = args[args.Length - 1];
            string outfile = null;
            GzipHeader header = new GzipHeader(file);
            JSONNode package = JSON.Parse(header.ExtraFieldAsString);
            if(package == null) {
                package = new JSONObject();
            }
            bool writeMetadata = false;
            bool print = false;
            foreach(string arg in args) {
                if (arg == "--write-metadata") writeMetadata = true;
                parseArg(arg, "version", package, "version");
                parseArg(arg, "versionid", package, "version.id");
                parseArg(arg, "title", package, "title");
                parseArg(arg, "id", package, "id");
                parseArg(arg, "pubdate", package, "pubdate");
                parseArg(arg, "unityversion", package, "unity_version");
                parseArg(arg, "categoryid", package, "category.id");
                parseArg(arg, "category", package, "category.label");
                parseArg(arg, "publisherid", package, "publisher.id");
                parseArg(arg, "publisher", package, "publisher.label");
                parseArg(arg, package);
                parseArg(arg, "file", ref outfile);
                if(arg == "-p") {
                    print = true;
                }
            }

            if(print) {
                Console.WriteLine("Package Header:");
                Console.WriteLine(package.ToString());
            } else if(null != file && file != outfile) {
                header.ExtraFieldAsString = package.ToString();
                if (writeMetadata) {
                    Console.WriteLine("Adding the following metadata: ");
                    Console.WriteLine(package.ToString());
                    using (Stream infile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                        infile.Position = header.headerLength;
                        if(File.Exists(outfile)) {
                            File.Delete(outfile);
                        }

                        using (Stream of = File.OpenWrite(outfile)) {
                            header.Write(of);
                            byte[] buffer = new byte[4096];
                            int read;
                            while ((read = infile.Read(buffer, 0, buffer.Length)) > 0) {
                                of.Write(buffer, 0, read);
                            }
                        }
                    }
                } else {
                    Console.WriteLine("Updated metadata preview: ");
                    Console.WriteLine(header.ExtraFieldAsString);
                }
            }
        }
    }
}
