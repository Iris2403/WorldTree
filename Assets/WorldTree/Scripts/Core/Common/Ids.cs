using System;

namespace WorldTree.Core.Common
{
    /// <summary>
    /// Rules for stable string IDs such as <c>item.nature.03</c> or <c>gen.seed_pouch.1</c>.
    /// IDs are stored in data and saves and must never be renamed after release (architecture rule 5).
    /// </summary>
    public static class Ids
    {
        /// <summary>Lowercase letters, digits, '.', '_' only; must start with a letter.</summary>
        public static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id[0] < 'a' || id[0] > 'z') return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        public static void Require(string id, string paramName)
        {
            if (!IsValid(id)) throw new ArgumentException("Invalid stable ID: '" + id + "'", paramName);
        }
    }
}
