using System.Collections.Generic;

namespace WorldTree.Core.Items
{
    /// <summary>Read access to all item definitions.</summary>
    public interface ICatalog
    {
        IReadOnlyList<ChainDef> Chains { get; }
        bool TryGetItem(string id, out ItemDef item);
        ItemDef GetItem(string id);
    }
}
