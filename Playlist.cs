using System.Collections.ObjectModel;

namespace ПлеерОганян
{
    public class Playlist
    {
        public string Name { get; set; } = string.Empty;

        public ObservableCollection<Track> Tracks { get; } = new ObservableCollection<Track>();
    }
}
