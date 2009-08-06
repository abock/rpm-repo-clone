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
            
            bool changes_made = false;
            foreach (var repo in repositories) {
                changes_made |= ProcessRepository (repo);
            }
            
            if (changes_made) {
                if (create_repo) {
                    CreateRepodata ();
                }
    
                if (ssh_rsync_destination != null) {
                    Rsync ();
                }
            } else {
                Console.WriteLine ("Skipping createrepo and rsync (no changes made).");
            }
            
            Console.WriteLine ("Done.");
        }
        
        private static void Startup (string [] args)
        {
            bool show_help = false;
            string alternate_pwd = null;
            bool create_pwd = false;
            
            var options = new OptionSet () {
                { "a|arch=", "add an architecture (default is i586)", v => architectures.Add (v) },
                { "d|dir=", "use an alternative working/output directory", v => alternate_pwd = v },
                { "m|mkpwd", "create the alternative working/output directory if it does not exist", v => create_pwd = v != null },
                { "c|createrepo", "run createrepo when finished", v => create_repo = v != null },
                { "s|sync=", "run ssh rsync when finished", v => ssh_rsync_destination = v },
                { "h|help", "show this message and exit", v => show_help = v != null }
            };
            
            foreach (var uri in options.Parse (args)) {
                repositories.Add (new Uri (uri, UriKind.Absolute));
            }
            
            if (show_help || args.Length == 0) {
                Console.Error.WriteLine ("Usage: rpm-repo-clone [OPTIONS+] <repository> [<repositories>]");
                Console.Error.WriteLine ();
                Console.Error.WriteLine ("Copyright 2009 Novell, Inc. Written by Aaron Bockover.");
                Console.Error.WriteLine ("Distributed under the MIT/X11 license.");
                Console.Error.WriteLine ();
                Console.Error.WriteLine ("Options:");
                options.WriteOptionDescriptions (Console.Error);
                Environment.Exit (1);
            }
            
            if (alternate_pwd != null) {
                if (!Directory.Exists (alternate_pwd)) {
                    if (create_pwd) {
                        Directory.CreateDirectory (alternate_pwd);
                    } else {
                        throw new DirectoryNotFoundException ("Invalid working directory: " + alternate_pwd);
                    }
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
            Console.WriteLine ("  SSH rsync to:      {0}", ssh_rsync_destination == null ? "<skip>" : null);
            if (ssh_rsync_destination != null) {
                Console.WriteLine ("    {0}", ssh_rsync_destination);
            }
            Console.WriteLine ();
        }
        
        private static bool ProcessRepository (Uri repository)
        {
            Console.WriteLine ("Loading {0}", repository);
            var remote_packages = new RpmMetadataDocument () {
                BasePackageUri = repository.AbsoluteUri
            };
            remote_packages.Load ();
            
            bool changes_made = false;
            
            foreach (var arch in architectures) {
                Console.WriteLine ("Updating packages for {0}...", arch);
            
                var packages = new List<Package> (
                    from remote in remote_packages
                    where remote.Arch == arch
                    select remote);
                    
                var files_to_remove = new List<string> ();
                if (Directory.Exists (arch)) {
                    files_to_remove.AddRange (Directory.GetFiles (arch));
                }
                
                var packages_to_update = new List<Package> ();
                
                foreach (var remote_package in packages) {
                    var file = new FileInfo (remote_package.RelativeLocation);
                    
                    files_to_remove.Remove (remote_package.RelativeLocation);
                    
                    if (file.Exists && 
                        file.Length == remote_package.FileSize &&
                        file.LastWriteTime == remote_package.FileTime) {
                        continue;
                    }
                    
                    packages_to_update.Add (remote_package);
                }
                
                for (int i = 0; i < packages_to_update.Count; i++) {
                    var remote_package = packages_to_update[i];
                    DownloadPackage (remote_package, i + 1, packages_to_update.Count);
                    changes_made = true;
                }
                
                if (files_to_remove.Count > 0) {
                    Console.WriteLine ("Removing obsolete packages...");
                    foreach (var file in files_to_remove) {
                        Console.WriteLine ("  {0}", file);
                        File.Delete (file);
                        changes_made = true;
                    }
                }
            }
            
            return changes_made;
        }
        
        private static void DownloadPackage (Package package, int index, int count)
        {
            var http_response = package.Download ();
            if (http_response == null) {
                throw new IOException ();
            }
            
            Directory.CreateDirectory (Path.GetDirectoryName (package.RelativeLocation));
            File.Delete (package.RelativeLocation);
            
            var xfer_stats = new RepoTransferStatistics () {
                Label = package.Name,
                Index = index,
                Count = count,
                TotalSize = http_response.ContentLength
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
        
        private static bool RunProcess (string command, string args)
        {
            var proc = new Process () { 
                StartInfo = new ProcessStartInfo (command, args) {
                    UseShellExecute = true
                }
            };
            
            proc.Start ();
            proc.WaitForExit ();
            
            return proc.ExitCode == 0;
        }
        
        private static void CreateRepodata ()
        {
            Console.WriteLine ("Creating repodata...");
            if (Directory.Exists ("repodata")) {
                Directory.Delete ("repodata", true);
            }
            
            if (Directory.Exists (".repodata")) {
                Directory.Delete (".repodata", true);
            }
            
            if (!RunProcess ("createrepo", "-p .")) {
                Console.Error.WriteLine ("Failed to create repodata");
            }
        }

        private static void Rsync ()
        {
            Console.WriteLine ("Rsyncing repository...");
            
            var ssh_parts = ssh_rsync_destination.Split (new char [] { ':' }, 2);
            var ssh_login = ssh_parts[0];
            var ssh_target_path = ssh_parts[1];
        
            if (RunProcess ("ssh", ssh_login + " mkdir -p \"" + ssh_target_path + "\"")) {
                if (!RunProcess ("rsync", "-avz --delete-after -e ssh . " + ssh_rsync_destination)) {
                    Console.Error.WriteLine ("rsync failed to: " + ssh_rsync_destination);
                }
            } else {
                Console.Error.WriteLine ("rsync failed: ssh " + ssh_login + " mkdir -p \"" + ssh_target_path + "\"");
            }
        }
    }
}
