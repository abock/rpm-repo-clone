// 
// RepoTransferStatistics.cs
//  
// Author:
//   Aaron Bockover <abockover@novell.com>
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

using Banshee.IO;

namespace RpmRepoClone
{
    public class RepoTransferStatistics : TransferStatistics
    {
        private string label;
        public string Label {
            get { return label; }
            set {
                label = value;
                            
                DisplayLabel = value.Length > 20 ? value.Substring (0, 20) : value;
                DisplayLabel = DisplayLabel.PadRight (20);
            }
        }
        
        public int Count { get; set; }
        public int Index { get; set; }
        
        public string DisplayLabel { get; private set; }
    
        protected override void OnUpdated ()
        {
            base.OnUpdated ();
            
            var progress_bar = String.Empty.PadLeft ((int)Math.Ceiling (
                    PercentComplete * 16), '=').PadRight (16);
            
            var status_message = Finished
                ? String.Format ("\r{0} | OK |", DisplayLabel)
                : String.Format ("\r{0} |{1}| {2:0.0}%  {3:0.0} KB/s  {4} {5}", 
                    DisplayLabel,
                    progress_bar,
                    PercentComplete * 100.0,
                    TransferRate / (double)(1 << 10),
                    TransferStatistics.FormatTime (TimeRemaining),
                    Count > 0 
                        ? String.Format ("({0}/{1})", Index, Count)
                        : String.Empty);
        
            Console.Write (status_message.PadRight (80));
            if (Finished) {
                Console.WriteLine ();
            }
        }   
    }
}
