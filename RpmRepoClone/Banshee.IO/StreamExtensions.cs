// 
// StreamExtensions.cs
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

namespace Banshee.IO
{
    public static class StreamExtensions
    {
        public delegate void StreamBlockCopiedHandler (long totalBytesRead,
            bool isLastBlock, byte [] block, int blockSize);
            
        public static void TransferTo (this Stream from, Stream to)
        {
            TransferTo (from, to, 8192, null);
        }
        
        public static void TransferTo (this Stream from, Stream to, StreamBlockCopiedHandler blockCopiedHandler)
        {
            TransferTo (from, to, 8192, blockCopiedHandler);
        }

        public static void TransferTo (this Stream from, Stream to, int bufferSize,
            StreamBlockCopiedHandler blockCopiedHandler)
        {
            long bytes_read = 0;
            byte [] buffer = new byte[bufferSize];
            int chunk_bytes_read = 0;

            while (true) {
                bool final = !((chunk_bytes_read = from.Read (buffer, 0, buffer.Length)) > 0);
                to.Write (buffer, 0, chunk_bytes_read);
                bytes_read += chunk_bytes_read;

                if (blockCopiedHandler != null) {
                    blockCopiedHandler (bytes_read, final, buffer, chunk_bytes_read);
                }

                if (final) {
                    break;
                }
            }
        }
    }
}
