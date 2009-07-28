// 
// Package.cs
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
using System.Net;
using System.Xml;
using System.Xml.Linq;

namespace RpmRepoClone
{
    public class Package
    {
        public Package (string basePackageUri)
        {
            BasePackageUri = basePackageUri;
        }
    
        public string BasePackageUri { get; private set; }
    
        public string Name { get; set; }
        public string Arch { get; set; }
        public string Location { get; private set; }
        public string RelativeLocation { get; private set; }
        public DateTime FileTime { get; set; }
        public long? FileSize { get; set; }
    
        public XElement LocationXElement {
            set {
                var base_uri = value.Attribute ("{http://www.w3.org/XML/1998/namespace}base");
                if (base_uri != null) {
                    Location += (string)base_uri.Value;
                }
                Location += (string)value.Attribute ("href").Value;
                RelativeLocation = Location;
                
                if (BasePackageUri != null) {
                    Location = BasePackageUri + "/" + Location;
                }
            }
        }
        
        public HttpWebResponse Download ()
        {
            var uri = new Uri (Location);
            
            if (uri.IsAbsoluteUri && (uri.Scheme == "http" || uri.Scheme == "https")) {
                var request = (HttpWebRequest)WebRequest.Create (uri.AbsoluteUri);
                request.AllowAutoRedirect = true;
                return (HttpWebResponse)request.GetResponse ();
            }
            
            return null;
        }
    }
}
