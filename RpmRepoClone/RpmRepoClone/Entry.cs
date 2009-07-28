using System;
using System.IO;
using System.Linq;

using Banshee.IO;

namespace RpmRepoClone
{
    public static class Entry
    {
        public static void Main ()
        {
            Console.WriteLine ("Loading local repository data...");
            
            var local_packages = new RpmMetadataDocument () {
                DocumentUri = "repodata/primary.xml.gz"
            };
            
            try {
                local_packages.Load ();
            } catch (DirectoryNotFoundException) {
            } catch (FileNotFoundException) {
                Console.WriteLine ("Warning: local repodata/primary.xml.gz does not exist.");
            }
    
            Console.WriteLine ("Loading remote repository data...");
            var remote_packages = new RpmMetadataDocument () {
                BasePackageUri = "http://dist.suse.de/ibs/Devel://Moblin://Factory/SUSE_SLE-11_GA/"
            };
            remote_packages.Load ();
    
            var query =
                from remote in remote_packages
                where remote.Arch == "i586"
                select remote;
    
            foreach (var remote_package in query) {
                var local_package = (from local in local_packages
                    where remote_package.Name == local.Name
                    select local).FirstOrDefault ();
                if (local_package == null || 
                    local_package.FileTime != remote_package.FileTime || 
                    local_package.FileSize != remote_package.FileSize) {
                    DownloadPackage (remote_package);
                }
            }
        }
        
        private static void DownloadPackage (Package package)
        {
            try {
                var http_response = package.Download ();
                if (http_response == null) {
                    throw new IOException ();
                }
                
                Directory.CreateDirectory (Path.GetDirectoryName (package.RelativeLocation));
                File.Delete (package.RelativeLocation);
                
                var xfer_stats = new TransferStatistics () {
                    TotalSize = http_response.ContentLength
                };
                
                var display_name = package.Name.Substring (0, 20);
                display_name = display_name.PadRight (20);
                
                xfer_stats.Updated += (o, e) => {
                    Console.Write ("Fetching {0} |{1}{2}| {3:0.0}% {4:0.0} MB/s {5} ETA \r",
                        display_name,
                        String.Empty.PadRight ((int)Math.Ceiling (xfer_stats.PercentComplete * 20), '='),
                        String.Empty.PadRight ((int)Math.Floor ((1.0 - xfer_stats.PercentComplete) * 20)),
                        (xfer_stats.PercentComplete * 100.0).ToString ("0.0").PadLeft (5),
                        xfer_stats.TransferRate / (double)(1 << 20),
                        TransferStatistics.FormatTime (xfer_stats.TimeRemaining));
                    if (xfer_stats.Finished) {
                        Console.WriteLine ();
                    }
                };
                
                using (var http_stream = http_response.GetResponseStream ()) {
                    using (var file_stream = File.OpenWrite (package.RelativeLocation)) {
                        http_stream.TransferTo (file_stream, 
                            (total_read, final_block, block, block_size) =>
                                xfer_stats.CommitNewBlock (block, block_size, final_block));
                    }
                }
            } catch (Exception e) {
                Console.Error.WriteLine ("Warning: failed to download {0}", package.Name);
                Console.Error.WriteLine (e);
            }
        }
    }
}