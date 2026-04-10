using System;

namespace ПлеерОганян
{
    public class Track
    {
        public string Title { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string Album { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; }

        public string DurationText => Duration.ToString(@"mm\:ss");
    }
}
