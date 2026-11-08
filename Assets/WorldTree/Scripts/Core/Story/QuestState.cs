using System;
using System.Collections.Generic;

namespace WorldTree.Core.Story
{
    /// <summary>
    /// Story progress (GDD §7.2, §8): the current area, completed tasks, story flags (Memory Leaves and
    /// others) and dialogue scenes waiting to be shown.
    /// </summary>
    public sealed class QuestState
    {
        private readonly HashSet<string> _completedTasks = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _pendingDialogues = new List<string>();

        public QuestState(int areaIndex)
        {
            AreaIndex = Math.Max(0, areaIndex);
        }

        /// <summary>Index of the area being restored. Equal to the number of areas once everything is restored.</summary>
        public int AreaIndex { get; internal set; }

        public IEnumerable<string> CompletedTasks => _completedTasks;
        public IEnumerable<string> Flags => _flags;

        /// <summary>Scenes waiting to be shown, oldest first.</summary>
        public IReadOnlyList<string> PendingDialogues => _pendingDialogues;

        public bool IsTaskCompleted(string taskId) => _completedTasks.Contains(taskId);
        public bool HasFlag(string flag) => _flags.Contains(flag);

        internal void CompleteTask(string taskId) => _completedTasks.Add(taskId);
        internal void SetFlag(string flag) => _flags.Add(flag);

        internal void QueueDialogue(string sceneId)
        {
            if (sceneId != null) _pendingDialogues.Add(sceneId);
        }

        internal bool DismissDialogue(string sceneId) => _pendingDialogues.Remove(sceneId);
    }
}
