using System;
using System.Collections.Generic;
using WorldTree.Core.Common;

namespace WorldTree.Core.Story
{
    /// <summary>A short, skippable dialogue scene of 2–6 lines (GDD §8.4).</summary>
    public sealed class DialogueScene
    {
        public DialogueScene(string id, IReadOnlyList<DialogueLine> lines)
        {
            Ids.Require(id, nameof(id));
            if (lines == null || lines.Count == 0) throw new ArgumentException("A scene needs at least one line.", nameof(lines));
            Id = id;
            Lines = lines;
        }

        public string Id { get; }
        public IReadOnlyList<DialogueLine> Lines { get; }
    }
}
