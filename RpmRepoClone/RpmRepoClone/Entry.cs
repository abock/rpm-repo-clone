using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using Banshee.IO;
using Mono.Options;

namespace RpmRepoClone
{
    public static class Entry
    {
        private static List<string> architectures = new List<string> ();
        private static List<Uri> repositories = new List<Uri> ();
        private static string ssh_rsync_destination;
        private static bool create_repo;
    
        public static void Main (string [] args)
        {
            try {
                Startup (args);
            } catch (OptionException e) {
                Console.Error.Write ("rpm-repo-clone: ");
                Console.Error.WriteLine (e.Message);
                Console.Error.WriteLine ("Try --help for more information");
                Environment.Exit (1);
            } catch (Exception e) {
                Console.Error.Write ("rpm-repo-clone: ");
                Console.Error.WriteLine (e.Message);
                Environment.Exit (1);
            }
            
            foreach (var repo in repositories) {
                ProcessRepository (repo);
            }
            
            if (create_repo) {
                CreateRepodata ();
            }
            
            Console.WriteLine ("Done.");
        }
        
        private static void Startup (string [] args)
        {
            bool show_help = false;
            string alternate_pwd = null;
            
            var options = new OptionSet () {
                { "a|arch=", "add an architecture (default is i586)", v => architectures.Add (v) },
                { "d|dir=", "use an alternative working/output directory", v => alternate_pwd = v },
                { "c|createrepo", "run createrepo when finished", v => create_repo = v != null },
                { "s|sync=", "run ssh rsync when finished", v => ssh_rsync_destination = v },
                { "h|help", "show this message and exit", v => show_help = v != null }
            };
            
            foreach (var uri in options.Parse (args)) {
                repositories.Add (new Uri (uri, UriKind.Absolute));
            }
            
            if (show_help) {
                Console.Error.WriteLine ("Usage: rpm-repo-clone OPTIONS+ <repository> [<repositories>]");
                Console.Error.WriteLine ();
                Console.Error.WriteLine ("Options:");
                options.WriteOptionDescriptions (Console.Error);
                Environment.Exit (1);
            }
            
            if (alternate_pwd != null) {
                if (!Directory.Exists (alternate_pwd)) {
                    throw new DirectoryNotFoundException ("Invalid working directory: " + alternate_pwd);
                }
                
                Environment.CurrentDirectory = alternate_pwd;
            }
            
            if (repositories.Count == 0) {
                throw new ApplicationException ("One or more repositories must be specified");
            }
            
            if (architectures.Count == 0) {
                architectures.Add ("i586");
            }
            
            Console.WriteLine ("Session Configuration:");
            Console.WriteLine ();
            
            Console.WriteLine ("  Repositories:");
            foreach (var repo in repositories) {
                Console.WriteLine ("    {0}", repo);
            }
            
            Console.WriteLine ();
            Console.WriteLine ("  Architectures:");
            foreach (var arch in architectures) {
                Console.WriteLine ("    {0}", arch);
            }
            
            Console.WriteLine ();
            Console.WriteLine ("  Working directory: {0}", Environment.CurrentDirectory);
            Console.WriteLine ("  Create repodata:   {0}", create_repo ? "yes" : "no");
            Console.WriteLine ("  SSH rsync to:      {0}", ssh_rsync_destination ?? "<skip>");
            Console.WriteLine ();
        }
        
        private static void ProcessRepository (Uri repository)
        {
            Console.WriteLine ("Loading repo [{0}]...", repository);
            var remote_packages = new RpmMetadataDocument () {
                BasePackageUri = repository.AbsoluteUri
            };
            remote_packages.Load ();
            
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
        
        private static void CreateRepodata ()
        {
            Console.WriteLine ("Creating repodata...");
            Directory.CreateDirectory ("repodata");
            var proc = Process.Start (new ProcessStartInfo (
                "createrepo", String.Format ("-o . -p .")));
            proc.Start ();
            proc.WaitForExit ();
        }
    }
}