using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Banshee.IO
{
    public class TransferStatistics
    {
        private DateTime last_update = DateTime.Now;
        private DateTime transfer_start_time = DateTime.MinValue;
        
        private HashAlgorithm hash;
        
        public TimeSpan UpdateFrequency { get; set; }
        public bool Finished { get; private set; }
        public bool ComputeHash { get; set; }

        private Type hash_type = typeof (MD5CryptoServiceProvider);
        public Type HashAlgorithmType {
            get { return hash_type; }
            set {
                if (!value.IsSubclassOf (typeof (HashAlgorithm))) {
                    throw new ArgumentException (
                        "Type must derive from System.Security.Cryptography.HashAlgorithm", 
                        "HashAlgorithmType");
                }

                hash_type = value;
            }
        }

        public event EventHandler Updated;

        public TransferStatistics ()
        {
            UpdateFrequency = TimeSpan.FromSeconds (0.25);
            Update ();
        }

        public TransferStatistics (string sourcePath) : this (new FileInfo (sourcePath))
        {
        }

        public TransferStatistics (FileInfo sourceFileInfo) : this ()
        {
            total_size = sourceFileInfo.Length;
        }

        public void CommitNewBlock (byte [] block, int size, bool final)
        {
            if (!ComputeHash || block == null) {
                if (final) {
                    Finished = true;
                }

                CompletedSize += size;
                return;
            }

            if (hash == null) {
                hash = (HashAlgorithm)Activator.CreateInstance (HashAlgorithmType);
            }
            
            if (final) {
                Finished = true;
                hash.TransformFinalBlock (block, 0, size);
            } else {
                hash.TransformBlock (block, 0, size, null, 0);
            }

            CompletedSize += size;
        }

        public void Finish ()
        {
            Finished = true;
            Update ();
        }

        private void Update ()
        {
            if (!Finished && DateTime.Now - last_update < UpdateFrequency) {
                return;
            }

            PercentComplete = TotalSize <= 0 ? 0 : CompletedSize / (double)TotalSize;
            last_update = DateTime.Now;
            OnUpdated ();
        }

        protected virtual void OnUpdated ()
        {
            var handler = Updated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
        
#region Transfer Properties
        
        private long total_size;
        public long TotalSize { 
            get { return total_size; }
            set { total_size = value; Update (); }
        }
        
        private long completed_size;
        public long CompletedSize {
            get { return completed_size; }
            set { 
                if (transfer_start_time == DateTime.MinValue) {
                    transfer_start_time = DateTime.Now;
                }
                
                completed_size = value;
                Update ();
            }
        }

        public long RemainingSize {
            get { return TotalSize - CompletedSize; }
        }
        
        public TimeSpan TimeEllapsed {
            get { return DateTime.Now - transfer_start_time; }
        }
        
        public TimeSpan TimeRemaining {
            get {
                return TimeSpan.FromMilliseconds (RemainingSize *
                    (TimeEllapsed.TotalMilliseconds / (double)CompletedSize));
            }
        }
        
        public double TransferRate {
            get { return CompletedSize / TimeEllapsed.TotalSeconds; }
        }
        
        public double PercentComplete { get; private set; }

        public byte [] Hash {
            get { return Finished && hash != null ? hash.Hash : null; }
        }

        public string HashString {
            get {
                if (!Finished || hash == null) {
                    return null;
                }
                
                var builder = new StringBuilder ();
             
                // Find the type that derives immediately from HashAlgorithm
                // since the type might be a provider or something - the
                // goal here is to get the raw algorithm type (e.g. SHA1 or MD5)
                // to use as a hash identifier (e.g. sha256:e9028fc...).
                // Warning: this might be crack
                //
                var type = hash.GetType ();
                while (type.BaseType != typeof (HashAlgorithm)) {
                    type = type.BaseType;
                }
                builder.Append (type.Name.ToLower ());
                builder.Append (':');

                for (int i = 0; i < hash.Hash.Length; i++) {
                    builder.Append (hash.Hash[i].ToString ("x2"));
                }

                return builder.ToString ();
            }
        }
        
#endregion

#region Format Helpers

        public static string FormatTime (TimeSpan span)
        {
            var builder = new System.Text.StringBuilder ();
            
            if (span.Days > 0) {
                builder.AppendFormat ("{0}:{1:00}:", span.Days, span.Hours);
            } else if (span.Hours > 0) {
                builder.AppendFormat ("{0}:", span.Hours);
            }
            
            if (span.TotalHours < 1 || span.TotalMinutes < 1) {
                builder.AppendFormat ("{0}:{1:00}", span.Minutes, span.Seconds);
            } else {
                builder.AppendFormat ("{0:00}:{1:00}", span.Minutes, span.Seconds);
            }
            
            return builder.ToString ();
        }

#endregion

    }
}
