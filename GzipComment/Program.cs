using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace GzipComment {
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

    [Flags]
    public enum CompressionMethod : byte {
        Store = 0,
        Compress = 1,
        Pack = 2,
        Lzh = 3,
        deflate = 8
    }

    [Flags]
    public enum Flags: byte {
        Ascii = 0,
        PartNumberPresent = 1,
        ExtraFieldPresent = 2,
        OriginalFileNamePresent = 3,
        FileCommentPresent = 4,
        FileEncrypted = 5
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
        byte[] optionalExtraField;
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
            using (Stream stream = File.OpenRead(file)) {
                byte[] buffer = new byte[2048];
                int read;
                read = stream.Read(magic, 0, 2);
                compressionMethod = (CompressionMethod) stream.ReadByte();
                flags = (Flags)stream.ReadByte();
                stream.Read(fileModification, 0, 4);
                extraFlags = (byte)stream.ReadByte();
                osType = (OsType)stream.ReadByte();
                if ((flags & Flags.PartNumberPresent) > 0) {
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
            stream.WriteByte((byte)(FileComment.Length > 0 ? flags | Flags.FileCommentPresent : flags & ~Flags.FileCommentPresent));
            stream.Write(fileModification, 0, 4);
            stream.WriteByte(extraFlags);
            stream.WriteByte((byte)osType);
            stream.Write(optionalPartNumber, 0, 2);
            if ((flags & Flags.PartNumberPresent) > 0) {
                stream.Write(optionalPartNumber, 0, 2);
            }
            if ((flags & Flags.ExtraFieldPresent) > 0) {
                stream.Write(optionalExtraFieldLength, 0, 2);
                optionalExtraField = new byte[OptionalExtraFieldLength];
                stream.Write(optionalExtraField, 0, OptionalExtraFieldLength);
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

    class JsonData {
        public override string ToString() {
            FieldInfo[] fields = GetType().GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            string result = "";
            foreach (FieldInfo field in fields) {
                if (null != field.GetValue(this)) {
                    string value = field.GetValue(this).ToString();
                    if (value.Length > 0) {
                        if (field.FieldType.IsSubclassOf(typeof(JsonData))) {
                            if (value != "{}") {
                                if (result.Length > 0) result += ",";
                                result += '"' + field.Name + "\": " + value;
                            }
                        } else {
                            if (result.Length > 0) result += ",";
                            result += '"' + field.Name + "\": \"" + value.Replace("\\", "\\\\").Replace("\n", "\\\\n").Replace("\"", "\\\"") + "\"";
                        }
                    }
                }
            }
            return '{' + result + '}';
        }
    }

    class Link : JsonData {
        public string id;
        public string type;
    }

    class Category : JsonData {
        public string id;
        public string label;
    }

    class Publisher : JsonData {
        public string id;
        public string label;
    }

    class UnityPackageDescription : JsonData {
        public string version;
        public string version_id;
        public string title;
        public string id;
        public string unity_version;
        public string pubdate;
        public Category category = new Category();
        public Publisher publisher = new Publisher();
    }

    /*
     * {
  "link": {
    "id": "82022",
    "type": "content"
  },
  "unity_version": "5.6.4p1-OC1",
  "pubdate": "14 Sep 2018",
  "version": "1.29",
  "upload_id": "266944",
  "version_id": "379637",
  "category": {
    "id": "112",
    "label": "Scripting/Integration"
  },
  "id": "82022",
  "title": "Oculus Integration",
  "publisher": {
    "id": "25353",
    "label": "Oculus"
  }
}*/



    class Program {
        private static void parseArg(string arg, string type, ref string value) {
            if(arg.StartsWith("--" + type + "=")) {
                value = arg.Substring(arg.IndexOf('=') + 1);
            }
        }

        static void Main(string[] args) {
            /*args = new string[] {
                "--write-metadata",
                "--version=1.17",
                "--versionid=1",
                "--title=Asset Manager",
                "--category=Editor/Extensions",
                "--categoryid=100",
                "--publisher=Doubling Technologies",
                "--publisherid=101",
                "--file=test.unitypackage",
                "bin/Debug/netcoreapp2.1/AssetManager.unitypackage"
            };*/

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
                Console.WriteLine("    --file=xyz           Set the path where the updated data will be written.");
                return;
            }
            string file = args[args.Length - 1];
            string outfile = null;
            GzipHeader header = new GzipHeader(file);
            UnityPackageDescription package = new UnityPackageDescription();
            bool writeMetadata = false;
            foreach(string arg in args) {
                if (arg == "--write-metadata") writeMetadata = true;
                parseArg(arg, "version", ref package.version);
                parseArg(arg, "versionid", ref package.version_id);
                parseArg(arg, "title", ref package.title);
                parseArg(arg, "id", ref package.id);
                parseArg(arg, "pubdate", ref package.pubdate);
                parseArg(arg, "unityversion", ref package.unity_version);
                parseArg(arg, "categoryid", ref package.category.id);
                parseArg(arg, "category", ref package.category.label);
                parseArg(arg, "publisherid", ref package.publisher.id);
                parseArg(arg, "publisher", ref package.publisher.label);
                parseArg(arg, "file", ref outfile);
            }

            if(args[0] == "-p") {
                Console.WriteLine(header.FileComment);
            } else if(null != file && file != outfile) {

                if(writeMetadata) {
                    Console.WriteLine("Adding the following metadata: ");
                    Console.WriteLine(package.ToString());
                    header.FileComment = package.ToString();
                }
                using(Stream infile = File.OpenRead(file)) {
                    infile.Position = header.headerLength;
                    using(Stream of = File.OpenWrite(outfile)) {
                        header.Write(of);
                        byte[] buffer = new byte[4096];
                        int read;
                        while((read = infile.Read(buffer, 0, buffer.Length)) > 0) {
                            of.Write(buffer, 0, read);
                        }
                    }
                }
            }
        }
    }
}
