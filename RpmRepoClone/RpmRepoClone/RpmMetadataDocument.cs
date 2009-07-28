// 
// RpmMetadataDocument.cs
//  
// Author:
//     Aaron Bockover <abockover@novell.com>
// 
// Copyright 2009 Novell, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

using Banshee.IO;

namespace RpmRepoClone
{
    public class RpmMetadataDocument : List<Package>
    {
        public string BasePackageUri { get; set; }
        public string DocumentUri { get; set; }
    
        public void Load ()
        {
            XNamespace md_xmlns = "http://linux.duke.edu/metadata/common";
            var resource = DocumentUri ?? BasePackageUri + "/repodata/primary.xml.gz";
            AddRange (
                from p in LoadXDocument (resource)
                    .Descendants (md_xmlns + "package")
                where p.Attribute ("type").Value == "rpm"
                select new Package (BasePackageUri) {
                    Name = p.Element (md_xmlns + "name").Value,
                    Arch = p.Element (md_xmlns + "arch").Value,
                    LocationXElement = p.Element (md_xmlns + "location"),
                    FileSize = (long?)p.Element (md_xmlns + "size").Attribute ("package"),
                    FileTime = ToDateTime ((long?)p.Element (md_xmlns + "time").Attribute ("file"))
                }
            );
        }

        private static XDocument LoadXDocument (string resource)
        {
            bool remove_resource = false;
    
            if (resource.StartsWith ("http://") || resource.StartsWith ("https://")) {
                var request = (HttpWebRequest)WebRequest.Create (new Uri (resource));
                request.AllowAutoRedirect = true;
                resource = Path.GetTempFileName ();
                remove_resource = true;
                using (var s = File.OpenWrite (resource)) {
                    using (var stream = ((HttpWebResponse)request.GetResponse ()).GetResponseStream ()) {
                        stream.TransferTo (s);
                    }
                }
            } 
            
            try {
                using (var stream = File.OpenRead (resource)) {
                    return LoadXDocument (stream);
                }
            } finally {
                if (remove_resource) {
                    File.Delete (resource);
                }
            }
        }

        private static XDocument LoadXDocument (Stream stream)
        {
            using (var gzip_stream = new GZipStream (stream, CompressionMode.Decompress)) {
                return XDocument.Load (XmlReader.Create (gzip_stream));
            }
        }

        private static readonly DateTime LocalUnixEpoch = new DateTime (1970, 1, 1).ToLocalTime ();
        private static readonly long super_ugly_min_hack = -15768000000; // 500 yrs before epoch...ewww
        
        private static DateTime ToDateTime (long? time)
        {
            return time == null 
                ? DateTime.MinValue 
                : ((time.Value <= super_ugly_min_hack)
                    ? DateTime.MinValue
                    : LocalUnixEpoch.AddSeconds (time.Value));
        }
    }
}
