using System;

namespace WorldTree.Core.Save
{
    /// <summary>
    /// File wrapper around the payload (architecture §6.2). <see cref="Sequence"/> increases with
    /// every write so the loader can tell which file is newest; <see cref="Sha256"/> detects
    /// truncated or corrupted files (an integrity check, not anti-cheat).
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion;
        public long Sequence;
        public long SavedAtUtcTicks;
        public string Sha256;
        public string Payload;
    }
}
