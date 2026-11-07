using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTree.Core.Save;
using WorldTree.Tests.Support;

namespace WorldTree.Tests
{
    /// <summary>Architecture §6.4–6.7: atomic protocol and newest-valid recovery after a process kill.</summary>
    public class SaveStoreTests
    {
        private const string Dir = "saves";
        private static readonly string MainPath = Path.Combine(Dir, AtomicSaveStore.MainFileName);
        private static readonly string TempPath = Path.Combine(Dir, AtomicSaveStore.TempFileName);
        private static readonly string BackupPath = Path.Combine(Dir, AtomicSaveStore.BackupFileName);
        private static readonly string CorruptPath = Path.Combine(Dir, AtomicSaveStore.CorruptFileName);
        private static readonly DateTime Now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>A save whose Sap field identifies which write it came from.</summary>
        private static SaveData Data(int marker) => new SaveData { Width = 7, Height = 9, Sap = marker, RandomState = 1 };

        private static int OperationsPerWrite()
        {
            var fs = new FaultInjectingFileSystem();
            var store = new AtomicSaveStore(fs, Dir);
            store.Save(Data(0), Now, false);
            int before = fs.Operations;
            store.Save(Data(1), Now, false);
            return fs.Operations - before;
        }

        [Test]
        public void ExhaustiveInterruptionAlwaysLeavesTheOldOrTheNewSave([Values(CrashMode.AfterOperation, CrashMode.DuringOperation)] CrashMode mode)
        {
            int opsPerWrite = OperationsPerWrite();
            Assert.Greater(opsPerWrite, 0);

            for (int k = 1; k <= opsPerWrite * 2; k++)
            {
                var fs = new FaultInjectingFileSystem();
                var store = new AtomicSaveStore(fs, Dir);
                store.Save(Data(0), Now, false);
                fs.Mode = mode;
                fs.CrashAt = fs.Operations + k;

                int completed = 0;
                try
                {
                    store.Save(Data(1), Now, false);
                    completed = 1;
                    store.Save(Data(2), Now, false);
                    completed = 2;
                }
                catch (SimulatedCrashException)
                {
                }
                fs.Disarm();

                // Also kill the process during the recovery's own normalization, at every step.
                for (int k2 = 0; k2 <= 4; k2++)
                {
                    var copy = Clone(fs);
                    if (k2 > 0)
                    {
                        copy.Mode = mode;
                        copy.CrashAt = copy.Operations + k2;
                        try { new AtomicSaveStore(copy, Dir).Load(Now); }
                        catch (SimulatedCrashException) { }
                        copy.Disarm();
                    }

                    var loaded = new AtomicSaveStore(copy, Dir).Load(Now);
                    string context = "k=" + k + " k2=" + k2 + " mode=" + mode;
                    Assert.IsNotNull(loaded.Data, context);
                    Assert.That(loaded.Data.Sap == completed || loaded.Data.Sap == completed + 1,
                        "loaded write " + loaded.Data.Sap + " after " + completed + " completed writes; " + context);
                    Assert.IsFalse(loaded.RestoreFailed, context);

                    // Recovery leaves a working store: the next write succeeds and wins.
                    var again = new AtomicSaveStore(copy, Dir);
                    var before = again.Load(Now);
                    again.Save(Data(9), Now, false);
                    var after = new AtomicSaveStore(copy, Dir).Load(Now);
                    Assert.AreEqual(9, after.Data.Sap, context);
                    Assert.Greater(after.Sequence, before.Sequence, context);
                }
            }
        }

        [Test]
        public void SequenceIncreasesAndContinuesAcrossRestarts()
        {
            var fs = new InMemoryFileSystem();
            var store = new AtomicSaveStore(fs, Dir);
            store.Save(Data(1), Now, false);
            store.Save(Data(2), Now, false);
            store.Save(Data(3), Now, false);
            Assert.AreEqual(3, store.LastSequence);

            var restarted = new AtomicSaveStore(fs, Dir);
            var loaded = restarted.Load(Now);
            Assert.AreEqual(3, loaded.Sequence);
            restarted.Save(Data(4), Now, false);
            Assert.IsTrue(SaveSerializer.TryParse(fs.Files[MainPath], out var env, out _));
            Assert.AreEqual(4, env.Sequence);
        }

        [Test]
        public void NormalWriteKeepsPreviousSaveAsBackup()
        {
            var fs = new InMemoryFileSystem();
            var store = new AtomicSaveStore(fs, Dir);
            store.Save(Data(1), Now, false);
            store.Save(Data(2), Now, false);
            Assert.IsTrue(fs.Exists(MainPath));
            Assert.IsTrue(fs.Exists(BackupPath));
            Assert.IsFalse(fs.Exists(TempPath));
            SaveSerializer.TryParse(fs.Files[BackupPath], out _, out var bak);
            Assert.AreEqual(1, bak.Sap);
        }

        [TestCase("truncated")]
        [TestCase("empty")]
        [TestCase("garbage")]
        [TestCase("checksum")]
        public void InvalidMainFallsBackToBackupAndIsQuarantined(string corruption)
        {
            var fs = new InMemoryFileSystem();
            var store = new AtomicSaveStore(fs, Dir);
            store.Save(Data(1), Now, false);
            store.Save(Data(2), Now, false);
            string good = fs.Files[MainPath];
            string bad;
            switch (corruption)
            {
                case "truncated": bad = good.Substring(0, good.Length / 2); break;
                case "empty": bad = string.Empty; break;
                case "garbage": bad = "\u0000\u0001not json at all"; break;
                default: bad = good.Replace("\"Sap\\\":2", "\"Sap\\\":7"); break;
            }
            Assert.AreNotEqual(good, bad, "test setup must corrupt the file");
            fs.Files[MainPath] = bad;

            var loaded = new AtomicSaveStore(fs, Dir).Load(Now);
            Assert.AreEqual(LoadSource.Backup, loaded.Source);
            Assert.AreEqual(1, loaded.Data.Sap);
            Assert.IsTrue(loaded.Quarantined);
            Assert.AreEqual(bad, fs.Files[CorruptPath]);
            // Normalized: a valid save.json exists again.
            Assert.IsTrue(SaveSerializer.TryParse(fs.Files[MainPath], out _, out var main));
            Assert.AreEqual(1, main.Sap);
        }

        [Test]
        public void OnlyOneCorruptFileIsKept()
        {
            var fs = new InMemoryFileSystem();
            new AtomicSaveStore(fs, Dir).Save(Data(1), Now, false);
            fs.Files[MainPath] = "first bad";
            new AtomicSaveStore(fs, Dir).Load(Now);
            fs.Files[MainPath] = "second bad";
            fs.Files[BackupPath] = "also bad";
            new AtomicSaveStore(fs, Dir).Load(Now);
            Assert.AreEqual("second bad", fs.Files[CorruptPath]);
            Assert.AreEqual(1, fs.Files.Keys.Count(k => k.Contains("corrupt")));
        }

        [Test]
        public void AllFilesInvalidStartsNewGameWithRestoreFailedFlag()
        {
            var fs = new InMemoryFileSystem();
            fs.Files[MainPath] = "bad";
            fs.Files[BackupPath] = "bad";
            fs.Files[TempPath] = "bad";
            var loaded = new AtomicSaveStore(fs, Dir).Load(Now);
            Assert.IsNull(loaded.Data);
            Assert.IsTrue(loaded.RestoreFailed);
        }

        [Test]
        public void NoFilesIsANewGameWithoutRestoreFailure()
        {
            var loaded = new AtomicSaveStore(new InMemoryFileSystem(), Dir).Load(Now);
            Assert.IsNull(loaded.Data);
            Assert.IsFalse(loaded.RestoreFailed);
            Assert.AreEqual(LoadSource.None, loaded.Source);
        }

        [Test]
        public void OnlyTempPresentLoadsItAndNormalizesLayout()
        {
            var fs = new InMemoryFileSystem();
            fs.Files[TempPath] = SaveSerializer.Serialize(Data(5), 5, Now);
            var loaded = new AtomicSaveStore(fs, Dir).Load(Now);
            Assert.AreEqual(LoadSource.Temp, loaded.Source);
            Assert.AreEqual(5, loaded.Data.Sap);
            Assert.IsTrue(fs.Exists(MainPath));
            Assert.IsFalse(fs.Exists(TempPath));
        }

        [Test]
        public void BackupWithHigherSequenceThanMainWins()
        {
            var fs = new InMemoryFileSystem();
            fs.Files[MainPath] = SaveSerializer.Serialize(Data(3), 3, Now);
            fs.Files[BackupPath] = SaveSerializer.Serialize(Data(8), 8, Now);
            var loaded = new AtomicSaveStore(fs, Dir).Load(Now);
            Assert.AreEqual(LoadSource.Backup, loaded.Source);
            Assert.AreEqual(8, loaded.Data.Sap);
        }

        [Test]
        public void IOExceptionOnWriteLeavesPreviousSaveLoadable()
        {
            var fs = new FaultInjectingFileSystem();
            var store = new AtomicSaveStore(fs, Dir);
            store.Save(Data(1), Now, false);
            fs.FailWritesWithIOException = true;
            Assert.Throws<IOException>(() => store.Save(Data(2), Now, false));
            fs.Disarm();
            var loaded = new AtomicSaveStore(fs, Dir).Load(Now);
            Assert.AreEqual(1, loaded.Data.Sap);
            // The store keeps working after the failure.
            store.Save(Data(3), Now, false);
            Assert.AreEqual(3, new AtomicSaveStore(fs, Dir).Load(Now).Data.Sap);
        }

        [Test]
        public void DiskFlushOnlyWhenRequested()
        {
            var fs = new InMemoryFileSystem();
            var store = new AtomicSaveStore(fs, Dir);
            store.Save(Data(1), Now, flushToDisk: false);
            store.Save(Data(2), Now, flushToDisk: false);
            Assert.AreEqual(0, fs.FlushedWrites);
            store.Save(Data(3), Now, flushToDisk: true);
            Assert.AreEqual(1, fs.FlushedWrites);
        }

        private static FaultInjectingFileSystem Clone(FaultInjectingFileSystem source)
        {
            var copy = new FaultInjectingFileSystem();
            foreach (var pair in source.Files) copy.Files[pair.Key] = pair.Value;
            return copy;
        }
    }
}
