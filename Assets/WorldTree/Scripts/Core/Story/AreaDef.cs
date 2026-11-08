using System;
using System.Collections.Generic;
using WorldTree.Core.Common;

namespace WorldTree.Core.Story
{
    /// <summary>
    /// An area of the World Tree (GDD §7.2): an ordered quest line of restoration tasks, an intro scene,
    /// a completion scene, rewards, and an optional Memory Leaf (GDD §8.3).
    /// </summary>
    public sealed class AreaDef
    {
        public AreaDef(string id, string name, IReadOnlyList<RestorationTask> tasks, string introDialogueId,
            string completionDialogueId, IReadOnlyList<string> rewardItemIds, string memoryLeafId)
        {
            Ids.Require(id, nameof(id));
            if (tasks == null || tasks.Count == 0) throw new ArgumentException("An area needs at least one task.", nameof(tasks));
            Id = id;
            Name = name ?? id;
            Tasks = tasks;
            IntroDialogueId = introDialogueId;
            CompletionDialogueId = completionDialogueId;
            RewardItemIds = rewardItemIds ?? Array.Empty<string>();
            MemoryLeafId = memoryLeafId;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<RestorationTask> Tasks { get; }
        public string IntroDialogueId { get; }
        public string CompletionDialogueId { get; }

        /// <summary>Items granted when the area is fully restored (for example a new generator).</summary>
        public IReadOnlyList<string> RewardItemIds { get; }

        /// <summary>Story flag set when the area is restored (a Memory Leaf), or null.</summary>
        public string MemoryLeafId { get; }

        public int TotalCost
        {
            get
            {
                int sum = 0;
                foreach (var t in Tasks) sum += t.LifebloomCost;
                return sum;
            }
        }
    }
}
