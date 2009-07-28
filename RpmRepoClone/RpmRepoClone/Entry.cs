using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Banshee.IO;

namespace RpmRepoClone
{
    public static class Entry
    {
        public static void Main ()
        {
            Console.WriteLine ("Loading remote repository data...");
            var remote_packages = new RpmMetadataDocument () {
                BasePackageUri = "http://dist.suse.de/ibs/Devel://Moblin://Factory/SUSE_SLE-11_GA/"
            };
            remote_packages.Load ();
    
            var architectures = new List<string> () {
                "i586"
            };
            
            foreach (var arch in architectures) {
                Console.WriteLine ("Updating packages for {0}...", arch);
            
                var query =
                    from remote in remote_packages
                    where remote.Arch == arch
                    select remote;
                    
                var files_to_remove = new List<string> (Directory.GetFiles (arch));
                
                foreach (var remote_package in query) {
                    var file = new FileInfo (remote_package.RelativeLocation);
                    
                    files_to_remove.Remove (remote_package.RelativeLocation);
                    
                    if (file.Exists && 
                        file.Length == remote_package.FileSize &&
                        file.LastWriteTime == remote_package.FileTime) {
                        continue;
                    }
                    
                    DownloadPackage (remote_package);
                }
                
                if (files_to_remove.Count > 0) {
                    Console.WriteLine ("Removing obsolete packages...");
                    foreach (var file in files_to_remove) {
                        Console.WriteLine ("  {0}", file);
                        File.Delete (file);
                    }
                }
            }
        }
        
        private static void DownloadPackage (Package package)
        {
            var http_response = package.Download ();
            if (http_response == null) {
                throw new IOException ();
            }
            
            Directory.CreateDirectory (Path.GetDirectoryName (package.RelativeLocation));
            File.Delete (package.RelativeLocation);
            
            var xfer_stats = new TransferStatistics () {
                TotalSize = http_response.ContentLength
            };
            
            var display_name = package.Name.Length > 20 ? package.Name.Substring (0, 20) : package.Name;
            display_name = display_name.PadRight (20);
            
            xfer_stats.Updated += (o, e) => {
                var progress_bar = String.Empty.PadLeft ((int)Math.Ceiling (
                    xfer_stats.PercentComplete * 20), '=').PadRight (20);
            
                if (xfer_stats.Finished) {
                    Console.WriteLine (String.Format ("\r{0} |{1}| OK", 
                        display_name, progress_bar).PadRight (80));
                } else {
                    Console.Write ("\r{0} |{1}| {2} {3} KB/s {4} ETA ",
                        display_name,
                        progress_bar,
                        String.Concat ((xfer_stats.PercentComplete * 100.0).ToString ("0.0"), "%").PadRight (6),
                        (xfer_stats.TransferRate / (double)(1 << 10)).ToString ("0.0").PadLeft (6),
                        TransferStatistics.FormatTime (xfer_stats.TimeRemaining).PadLeft (6));
                }
            };
            
            using (var http_stream = http_response.GetResponseStream ()) {
                using (var file_stream = File.OpenWrite (package.RelativeLocation)) {
                    http_stream.TransferTo (file_stream, 
                        (total_read, final_block, block, block_size) =>
                            xfer_stats.CommitNewBlock (block, block_size, final_block));
                }
                
                File.SetCreationTime (package.RelativeLocation, package.FileTime);
                File.SetLastWriteTime (package.RelativeLocation, package.FileTime);
                File.SetLastAccessTime (package.RelativeLocation, package.FileTime);
            }
        }
    }
}