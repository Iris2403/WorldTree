using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTree.Data
{
    /// <summary>An area and its quest line (GDD §6.3, §7.2). Tasks are restored in list order.</summary>
    [CreateAssetMenu(menuName = "WorldTree/Area", fileName = "Area_New")]
    public sealed class AreaDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Task
        {
            public string id;
            public string name;
            [Min(1)] public int lifebloomCost = 10;
            [Tooltip("Optional dialogue scene ID shown after the task.")]
            public string dialogueId;
        }

        [SerializeField] private string areaId;
        [SerializeField] private string displayName;
        [SerializeField] private List<Task> tasks = new List<Task>();
        [SerializeField] private string introDialogueId;
        [SerializeField] private string completionDialogueId;
        [SerializeField] private List<string> rewardItemIds = new List<string>();
        [Tooltip("Story flag set when the area is restored (a Memory Leaf). Leave empty for none.")]
        [SerializeField] private string memoryLeafId;

        public string AreaId => areaId;
        public string DisplayName => displayName;
        public IReadOnlyList<Task> Tasks => tasks;
        public string IntroDialogueId => introDialogueId;
        public string CompletionDialogueId => completionDialogueId;
        public IReadOnlyList<string> RewardItemIds => rewardItemIds;
        public string MemoryLeafId => memoryLeafId;

        public void Configure(string id, string name, List<Task> areaTasks, string intro, string completion, List<string> rewards, string memoryLeaf)
        {
            areaId = id;
            displayName = name;
            tasks = areaTasks;
            introDialogueId = intro;
            completionDialogueId = completion;
            rewardItemIds = rewards;
            memoryLeafId = memoryLeaf;
        }
    }
}
