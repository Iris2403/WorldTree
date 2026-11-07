using System;
using System.IO;

namespace WorldTree.Core.Save
{
    /// <summary>
    /// Writes and loads saves with the atomic protocol of architecture §6.4 and the newest-valid
    /// recovery of §6.6. Single-threaded by design: call it from the main thread only.
    /// Guarantee: a process kill at any point never leaves the device without a valid save.
    /// No power-loss guarantee is made (§6.5).
    /// </summary>
    public sealed class AtomicSaveStore
    {
        public const string MainFileName = "save.json";
        public const string TempFileName = "save.tmp";
        public const string BackupFileName = "save.bak";
        public const string CorruptFileName = "save.corrupt.json";

        private readonly IFileSystem _fs;
        private readonly string _main;
        private readonly string _temp;
        private readonly string _backup;
        private readonly string _corrupt;

        public AtomicSaveStore(IFileSystem fileSystem, string directory)
        {
            _fs = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            _fs.EnsureDirectory(directory);
            _main = Path.Combine(directory, MainFileName);
            _temp = Path.Combine(directory, TempFileName);
            _backup = Path.Combine(directory, BackupFileName);
            _corrupt = Path.Combine(directory, CorruptFileName);
        }

        /// <summary>Sequence of the last save written or loaded. The next write uses this + 1.</summary>
        public long LastSequence { get; private set; }

        /// <summary>
        /// Writes a new save: delete leftover tmp → write tmp → (delete bak, rename json → bak) →
        /// rename tmp → json. Throws <see cref="IOException"/> on failure; the previous save then
        /// still loads.
        /// </summary>
        public void Save(SaveData data, DateTime nowUtc, bool flushToDisk)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            long sequence = LastSequence + 1;
            string text = SaveSerializer.Serialize(data, sequence, nowUtc);

            if (_fs.Exists(_temp)) _fs.Delete(_temp);
            _fs.WriteAllText(_temp, text, flushToDisk);
            if (_fs.Exists(_main))
            {
                if (_fs.Exists(_backup)) _fs.Delete(_backup);
                _fs.Move(_main, _backup);
            }
            _fs.Move(_temp, _main);
            LastSequence = sequence;
        }

        /// <summary>
        /// Loads the valid file with the highest sequence among json, tmp and bak, then normalizes the
        /// file layout if the newest copy wasn't <c>save.json</c>. Never throws for bad file contents.
        /// </summary>
        public LoadResult Load(DateTime nowUtc)
        {
            var main = Read(_main);
            var temp = Read(_temp);
            var backup = Read(_backup);

            Candidate best = null;
            foreach (var c in new[] { main, temp, backup })
            {
                if (c.Valid && (best == null || c.Envelope.Sequence > best.Envelope.Sequence)) best = c;
            }

            long maxSequence = 0;
            foreach (var c in new[] { main, temp, backup })
            {
                if (c.Valid && c.Envelope.Sequence > maxSequence) maxSequence = c.Envelope.Sequence;
            }
            LastSequence = maxSequence;

            bool quarantined = Quarantine(main) || Quarantine(backup) || Quarantine(temp);
            bool anyExisted = main.Exists || temp.Exists || backup.Exists;

            if (best == null) return new LoadResult(null, LoadSource.None, 0, anyExisted, quarantined);

            LoadSource source = best == main ? LoadSource.Main : best == temp ? LoadSource.Temp : LoadSource.Backup;
            try
            {
                if (source == LoadSource.Temp) FinishInterruptedWrite(main);
                else if (source == LoadSource.Backup) Save(best.Data, nowUtc, flushToDisk: false);
            }
            catch (IOException)
            {
                // The data is loaded; normalization will happen with the next successful save.
            }
            return new LoadResult(best.Data, source, best.Envelope.Sequence, false, quarantined);
        }

        /// <summary>
        /// The newest copy is a complete tmp file: finish the protocol with renames only, so there is
        /// no window in which the newest data exists nowhere.
        /// </summary>
        private void FinishInterruptedWrite(Candidate main)
        {
            if (main.Exists)
            {
                if (main.Valid)
                {
                    if (_fs.Exists(_backup)) _fs.Delete(_backup);
                    _fs.Move(_main, _backup);
                }
                else
                {
                    _fs.Delete(_main); // already quarantined; keep the older valid backup
                }
            }
            _fs.Move(_temp, _main);
        }

        private bool Quarantine(Candidate c)
        {
            if (!c.Exists || c.Valid) return false;
            // An unfinished tmp from an interrupted write is expected, not corruption.
            if (c.Path == _temp) return false;
            try
            {
                _fs.WriteAllText(_corrupt, c.Text ?? string.Empty, flushToDisk: false);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private Candidate Read(string path)
        {
            var c = new Candidate { Path = path };
            if (!_fs.Exists(path)) return c;
            c.Exists = true;
            try
            {
                c.Text = _fs.ReadAllText(path);
            }
            catch (IOException)
            {
                return c;
            }
            c.Valid = SaveSerializer.TryParse(c.Text, out c.Envelope, out c.Data);
            return c;
        }

        private sealed class Candidate
        {
            public string Path;
            public bool Exists;
            public bool Valid;
            public string Text;
            public SaveEnvelope Envelope;
            public SaveData Data;
        }
    }
}
