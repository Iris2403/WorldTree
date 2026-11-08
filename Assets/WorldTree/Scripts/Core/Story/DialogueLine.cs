namespace WorldTree.Core.Story
{
    /// <summary>One line of a dialogue scene (GDD §8.4). Speaker IDs match the customers and characters of GDD §8.2.</summary>
    public readonly struct DialogueLine
    {
        public DialogueLine(string speakerId, string text)
        {
            SpeakerId = speakerId;
            Text = text;
        }

        public string SpeakerId { get; }

        /// <summary>English text until the Localization package arrives (Phase 7).</summary>
        public string Text { get; }
    }
}
