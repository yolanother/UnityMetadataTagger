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
using SimpleJSON;
using UPHLib.GzipLib;

namespace UPHLib {
    public class UnityPackageHeader {
        private JSONNode package;

        private UnityPackageHeader() { }

        public static UnityPackageHeader OpenPackage(GzipHeader gzipHeader) {
            JSONNode package = JSON.Parse(gzipHeader.ExtraFieldAsString);
            if (package == null) {
                package = new JSONObject();
            }
            UnityPackageHeader uph = new UnityPackageHeader();
            uph.package = package;
            return uph;
        }

        public static UnityPackageHeader OpenPackage(string file) {
            return OpenPackage(new GzipHeader(file));
        }

        private JSONNode GetNode(string path) {
            JSONNode node = package;
            do {
                int index = path.IndexOf('.');
                index = index > 0 ? index : path.Length;
                string key = path.Substring(0, index);
                path = path.Substring(Math.Min(path.Length, index + 1));
                node = node[key];
            } while (path.Length > 0 && node != null);

            return node;
        }

        private JSONNode SetNodeValue(string path, string value) {
            JSONNode node = package;
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

            return node;
        }

        public void RemoveValue(string path) {
            JSONNode node = package;
            do {
                int index = path.IndexOf('.');
                index = index > 0 ? index : path.Length;
                string key = path.Substring(0, index);
                path = path.Substring(Math.Min(path.Length, index + 1));
                if (path.Length == 0) {
                    node.Remove(key);
                } else {
                    node = node[key];
                }
            } while (path.Length > 0 && node != null);
        }

        public string this[string path] {
            get {
                JSONNode node = GetNode(path);
                return node?.Value ?? "";
            } set {
                SetNodeValue(path, value);
            }
        }

        public override string ToString() {
            return package.ToString();
        }

        public void UpdatePackageFile(string sourceFile, string outFile) {
            GzipHeader header = new GzipHeader(sourceFile);
            header.ExtraFieldAsString = ToString();
            using (Stream infile = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                infile.Position = header.headerLength;
                if (File.Exists(outFile)) {
                    File.Delete(outFile);
                }

                using (Stream of = File.OpenWrite(outFile)) {
                    header.Write(of);
                    byte[] buffer = new byte[4096];
                    int read;
                    while ((read = infile.Read(buffer, 0, buffer.Length)) > 0) {
                        of.Write(buffer, 0, read);
                    }
                }
            }
        }
    }
}
