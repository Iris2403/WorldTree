using System;
using WorldTree.Core.Common;

namespace WorldTree.Core.Story
{
    /// <summary>One step of an area's quest line (GDD §6.3, §7.2): costs Lifebloom and restores part of the area.</summary>
    public sealed class RestorationTask
    {
        public RestorationTask(string id, string name, int lifebloomCost, string dialogueId = null)
        {
            Ids.Require(id, nameof(id));
            if (lifebloomCost <= 0) throw new ArgumentOutOfRangeException(nameof(lifebloomCost));
            Id = id;
            Name = name ?? id;
            LifebloomCost = lifebloomCost;
            DialogueId = dialogueId;
        }

        public string Id { get; }
        public string Name { get; }
        public int LifebloomCost { get; }

        /// <summary>Scene shown after completing the task, or null.</summary>
        public string DialogueId { get; }
    }
}
