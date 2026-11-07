namespace WorldTree.Core.Save
{
    /// <summary>
    /// The only file access the save store uses, so tests can inject an in-memory file system that
    /// simulates a process being killed between any two operations.
    /// </summary>
    public interface IFileSystem
    {
        void EnsureDirectory(string path);
        bool Exists(string path);
        string ReadAllText(string path);

        /// <summary>Creates or overwrites a file. <paramref name="flushToDisk"/> asks the OS to persist the data.</summary>
        void WriteAllText(string path, string contents, bool flushToDisk);

        /// <summary>Renames a file. The destination must not exist.</summary>
        void Move(string from, string to);

        void Delete(string path);
    }
}
