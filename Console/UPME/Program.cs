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
using System;
using System.IO;
using UPHLib;

namespace GzipComment {
    class Program {
        private static string packageArgument = "--package.";

        private static void parseArg(string arg, UnityPackageHeader package) {
            int eqIndex = arg.IndexOf("=");
            if(arg.StartsWith(packageArgument) && eqIndex >= 0) {
                string path = arg.Substring(packageArgument.Length, eqIndex - packageArgument.Length);
                if (arg.Length > eqIndex + 1) {
                    string value = arg.Substring(eqIndex + 1);
                    package[path] = value;
                }
            }
        }

        private static void parseArg(string arg, string type, UnityPackageHeader package, string path) {
            if(arg.StartsWith("--" + type + "=")) {
                string value = arg.Substring(arg.IndexOf('=') + 1);
                package[path] = value;
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
                Console.WriteLine("    -p | --print         Print the file comment and quit");
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
                Console.WriteLine("                             ex: --package.version=1.1 would match behavior of --version=1.1");
                Console.WriteLine("    --file=xyz           Set the path where the updated data will be written.");
                Console.WriteLine("                             NOTE: the file names must not match");
                return;
            }
            string file = args[args.Length - 1];
            string outfile = null;
            bool print = false;

            UnityPackageHeader package = UnityPackageHeader.OpenPackage(file);

            foreach(string arg in args) {
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
                if(arg == "-p" || arg == "--print") {
                    print = true;
                }
            }

            if(print || outfile == null || outfile == file) {
                Console.WriteLine("Unitypackage Header Preview:");
                Console.WriteLine(package.ToString());
                if(outfile == file) {
                    Console.WriteLine("ERROR: Source and destination files must not match.");
                }
            } else {
                Console.WriteLine("Adding the following to Unitypackage Header Preview:");
                Console.WriteLine(package.ToString());
                Console.Write("Writing to " + outfile + "...");
                package.UpdatePackageFile(file, outfile);
                Console.WriteLine("done.");
            }
        }
    }
}
