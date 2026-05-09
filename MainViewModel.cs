using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

namespace ПлеерОганян
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PlayerService _playerService = new PlayerService();
        private readonly RelayCommand _playCommand;
        private readonly RelayCommand _pauseCommand;
        private readonly RelayCommand _stopCommand;
        private readonly RelayCommand _nextCommand;
        private readonly RelayCommand _previousCommand;
        private readonly RelayCommand _createPlaylistCommand;
        private readonly RelayCommand _deletePlaylistCommand;
        private readonly RelayCommand _addToPlaylistCommand;
        private readonly RelayCommand _addToQueueAndPlayCommand;
        private readonly RelayCommand _removeQueueTrackCommand;
        private readonly RelayCommand _clearQueueCommand;
        private readonly RelayCommand _openLibraryCommand;
        private readonly Random _random = new Random();

        private Track _selectedTrack;
        private Playlist _selectedPlaylist;
        private Playlist _selectedPlaylistForAdd;
        private Track _selectedQueueTrack;
        private Track _currentQueueTrack;
        private TimeSpan _currentPosition;
        private TimeSpan _duration;
        private double _sliderPosition;
        private double _volume;
        private bool _isUpdatingFromPlayer;
        private string _searchText = string.Empty;
        private bool _isShuffleEnabled;
        private bool _isRepeatTrackEnabled;
        private bool _isRepeatPlaylistEnabled;
        private int _playlistCounter = 1;
        private ICollectionView _displayedTracksView;

        public MainViewModel()
        {
            UpdateDisplayedTracksView();

            LoadFolderCommand = new RelayCommand(LoadFolder);
            _playCommand = new RelayCommand(Play, () => SelectedTrack != null);
            _pauseCommand = new RelayCommand(Pause);
            _stopCommand = new RelayCommand(Stop);
            _nextCommand = new RelayCommand(Next, () => SelectedTrack != null);
            _previousCommand = new RelayCommand(Previous, () => SelectedTrack != null);
            _createPlaylistCommand = new RelayCommand(CreatePlaylist);
            _deletePlaylistCommand = new RelayCommand(DeletePlaylist, () => SelectedPlaylist != null);
            _addToPlaylistCommand = new RelayCommand(AddSelectedTrackToPlaylist, () => SelectedTrack != null && SelectedPlaylistForAdd != null);
            _addToQueueAndPlayCommand = new RelayCommand(AddSelectedTrackToQueueAndPlay, () => SelectedTrack != null);
            _removeQueueTrackCommand = new RelayCommand(RemoveSelectedQueueTrack, () => SelectedQueueTrack != null);
            _clearQueueCommand = new RelayCommand(ClearQueue, () => QueueTracks.Count > 0);
            _openLibraryCommand = new RelayCommand(OpenLibrary);

            _playerService.PositionChanged += OnPlayerPositionChanged;
            _playerService.TrackEnded += OnTrackEnded;
            Volume = _playerService.Volume;
        }

        public ObservableCollection<Track> Tracks { get; } = new ObservableCollection<Track>();

        public ObservableCollection<Playlist> Playlists { get; } = new ObservableCollection<Playlist>();

        public ObservableCollection<Track> QueueTracks { get; } = new ObservableCollection<Track>();

        public ICollectionView DisplayedTracksView
        {
            get => _displayedTracksView;
            private set
            {
                _displayedTracksView = value;
                OnPropertyChanged();
            }
        }

        public Track SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                if (_selectedTrack == value)
                {
                    return;
                }

                _selectedTrack = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTrackTitle));
                _playCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
                _previousCommand.RaiseCanExecuteChanged();
                _addToPlaylistCommand.RaiseCanExecuteChanged();
                _addToQueueAndPlayCommand.RaiseCanExecuteChanged();
            }
        }

        public Playlist SelectedPlaylist
        {
            get => _selectedPlaylist;
            set
            {
                if (_selectedPlaylist == value)
                {
                    return;
                }

                _selectedPlaylist = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLibrarySelected));
                _deletePlaylistCommand.RaiseCanExecuteChanged();
                UpdateDisplayedTracksView();
            }
        }

        public Playlist SelectedPlaylistForAdd
        {
            get => _selectedPlaylistForAdd;
            set
            {
                if (_selectedPlaylistForAdd == value)
                {
                    return;
                }

                _selectedPlaylistForAdd = value;
                OnPropertyChanged();
                _addToPlaylistCommand.RaiseCanExecuteChanged();
            }
        }

        public Track SelectedQueueTrack
        {
            get => _selectedQueueTrack;
            set
            {
                if (_selectedQueueTrack == value)
                {
                    return;
                }

                _selectedQueueTrack = value;
                OnPropertyChanged();
                _removeQueueTrackCommand.RaiseCanExecuteChanged();
            }
        }

        public Track CurrentQueueTrack
        {
            get => _currentQueueTrack;
            set
            {
                if (_currentQueueTrack == value)
                {
                    return;
                }

                _currentQueueTrack = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                DisplayedTracksView?.Refresh();
            }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (_currentPosition == value)
                {
                    return;
                }

                _currentPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPositionText));
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration == value)
                {
                    return;
                }

                _duration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationText));
            }
        }

        public double SliderPosition
        {
            get => _sliderPosition;
            set
            {
                if (Math.Abs(_sliderPosition - value) < 0.1)
                {
                    return;
                }

                _sliderPosition = value;
                OnPropertyChanged();

                if (_isUpdatingFromPlayer || Duration == TimeSpan.Zero)
                {
                    return;
                }

                var position = TimeSpan.FromSeconds(Duration.TotalSeconds * (_sliderPosition / 100.0));
                _playerService.Seek(position);
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                var normalized = value;

                if (normalized < 0)
                {
                    normalized = 0;
                }

                if (normalized > 1)
                {
                    normalized = 1;
                }

                if (Math.Abs(_volume - normalized) < 0.01)
                {
                    return;
                }

                _volume = normalized;
                OnPropertyChanged();
                _playerService.SetVolume(_volume);
            }
        }

        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                if (_isShuffleEnabled == value)
                {
                    return;
                }

                _isShuffleEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsRepeatTrackEnabled
        {
            get => _isRepeatTrackEnabled;
            set
            {
                if (_isRepeatTrackEnabled == value)
                {
                    return;
                }

                _isRepeatTrackEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsRepeatPlaylistEnabled
        {
            get => _isRepeatPlaylistEnabled;
            set
            {
                if (_isRepeatPlaylistEnabled == value)
                {
                    return;
                }

                _isRepeatPlaylistEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsLibrarySelected => SelectedPlaylist == null;

        public string SelectedTrackTitle => SelectedTrack?.Title ?? "Трек не выбран";

        public string CurrentPositionText => FormatTime(CurrentPosition);

        public string DurationText => FormatTime(Duration);

        public ICommand LoadFolderCommand { get; }

        public ICommand PlayCommand => _playCommand;

        public ICommand PauseCommand => _pauseCommand;

        public ICommand StopCommand => _stopCommand;

        public ICommand NextCommand => _nextCommand;

        public ICommand PreviousCommand => _previousCommand;

        public ICommand CreatePlaylistCommand => _createPlaylistCommand;

        public ICommand DeletePlaylistCommand => _deletePlaylistCommand;

        public ICommand AddToPlaylistCommand => _addToPlaylistCommand;

        public ICommand AddToQueueAndPlayCommand => _addToQueueAndPlayCommand;

        public ICommand RemoveQueueTrackCommand => _removeQueueTrackCommand;

        public ICommand ClearQueueCommand => _clearQueueCommand;

        public ICommand OpenLibraryCommand => _openLibraryCommand;

        public event PropertyChangedEventHandler PropertyChanged;

        private void LoadFolder()
        {
            var dialog = new OpenFolderDialog();

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Tracks.Clear();

            var files = Directory.GetFiles(dialog.FolderName, "*.mp3")
                .OrderBy(path => path);

            foreach (var filePath in files)
            {
                Tracks.Add(CreateTrack(filePath));
            }

            DisplayedTracksView?.Refresh();
            SelectedTrack = DisplayedTracksView?.Cast<Track>().FirstOrDefault();
        }

        private void Play()
        {
            if (SelectedTrack == null || !File.Exists(SelectedTrack.Path))
            {
                return;
            }

            CurrentQueueTrack = QueueTracks.Contains(SelectedTrack) ? SelectedTrack : null;
            _playerService.Play(SelectedTrack.Path);
        }

        private void Pause()
        {
            _playerService.Pause();
        }

        private void Stop()
        {
            _playerService.Stop();
        }

        private void Next()
        {
            if (TryPlayNextFromQueue())
            {
                return;
            }

            if (Tracks.Count == 0 || SelectedTrack == null)
            {
                return;
            }

            if (IsShuffleEnabled)
            {
                PlayTrack(GetRandomTrack(), false);
                return;
            }

            var currentIndex = Tracks.IndexOf(SelectedTrack);

            if (currentIndex < 0)
            {
                return;
            }

            var nextIndex = (currentIndex + 1) % Tracks.Count;
            PlayTrack(Tracks[nextIndex], false);
        }

        private void Previous()
        {
            if (TryPlayPreviousFromQueue())
            {
                return;
            }

            if (Tracks.Count == 0 || SelectedTrack == null)
            {
                return;
            }

            var currentIndex = Tracks.IndexOf(SelectedTrack);

            if (currentIndex < 0)
            {
                return;
            }

            var previousIndex = (currentIndex - 1 + Tracks.Count) % Tracks.Count;
            PlayTrack(Tracks[previousIndex], false);
        }

        private void CreatePlaylist()
        {
            var playlist = new Playlist
            {
                Name = $"Новый плейлист {_playlistCounter++}"
            };

            Playlists.Add(playlist);
            SelectedPlaylist = playlist;

            if (SelectedPlaylistForAdd == null)
            {
                SelectedPlaylistForAdd = playlist;
            }
        }

        private void DeletePlaylist()
        {
            if (SelectedPlaylist == null)
            {
                return;
            }

            if (SelectedPlaylistForAdd == SelectedPlaylist)
            {
                SelectedPlaylistForAdd = null;
            }

            Playlists.Remove(SelectedPlaylist);
            SelectedPlaylist = null;

            if (SelectedPlaylistForAdd == null)
            {
                SelectedPlaylistForAdd = Playlists.FirstOrDefault();
            }
        }

        private void AddSelectedTrackToPlaylist()
        {
            if (SelectedTrack == null || SelectedPlaylistForAdd == null)
            {
                return;
            }

            SelectedPlaylistForAdd.Tracks.Add(SelectedTrack);
        }

        private void AddSelectedTrackToQueueAndPlay()
        {
            if (SelectedTrack == null)
            {
                return;
            }

            QueueTracks.Add(SelectedTrack);
            _clearQueueCommand.RaiseCanExecuteChanged();
            PlayTrack(SelectedTrack, true);
        }

        private void RemoveSelectedQueueTrack()
        {
            if (SelectedQueueTrack == null)
            {
                return;
            }

            if (CurrentQueueTrack == SelectedQueueTrack)
            {
                CurrentQueueTrack = null;
            }

            QueueTracks.Remove(SelectedQueueTrack);
            SelectedQueueTrack = null;
            _clearQueueCommand.RaiseCanExecuteChanged();
        }

        private void ClearQueue()
        {
            QueueTracks.Clear();
            CurrentQueueTrack = null;
            SelectedQueueTrack = null;
            _clearQueueCommand.RaiseCanExecuteChanged();
        }

        private void OpenLibrary()
        {
            SelectedPlaylist = null;
        }

        private void OnPlayerPositionChanged(TimeSpan currentPosition, TimeSpan duration)
        {
            CurrentPosition = currentPosition;
            Duration = duration;

            _isUpdatingFromPlayer = true;
            SliderPosition = duration.TotalSeconds > 0
                ? currentPosition.TotalSeconds / duration.TotalSeconds * 100.0
                : 0;
            _isUpdatingFromPlayer = false;
        }

        private void OnTrackEnded()
        {
            if (SelectedTrack == null)
            {
                return;
            }

            if (IsRepeatTrackEnabled)
            {
                Play();
                return;
            }

            if (TryPlayNextFromQueue())
            {
                return;
            }

            if (Tracks.Count == 0)
            {
                return;
            }

            if (IsShuffleEnabled)
            {
                PlayTrack(GetRandomTrack(), false);
                return;
            }

            var currentIndex = Tracks.IndexOf(SelectedTrack);

            if (currentIndex < 0)
            {
                return;
            }

            if (currentIndex < Tracks.Count - 1)
            {
                PlayTrack(Tracks[currentIndex + 1], false);
                return;
            }

            if (IsRepeatPlaylistEnabled)
            {
                PlayTrack(Tracks[0], false);
                return;
            }

            Stop();
        }

        private bool TryPlayNextFromQueue()
        {
            if (QueueTracks.Count == 0)
            {
                return false;
            }

            if (CurrentQueueTrack == null || !QueueTracks.Contains(CurrentQueueTrack))
            {
                PlayTrack(QueueTracks[0], true);
                return true;
            }

            var currentIndex = QueueTracks.IndexOf(CurrentQueueTrack);

            if (currentIndex >= 0 && currentIndex < QueueTracks.Count - 1)
            {
                PlayTrack(QueueTracks[currentIndex + 1], true);
                return true;
            }

            return false;
        }

        private bool TryPlayPreviousFromQueue()
        {
            if (QueueTracks.Count == 0 || CurrentQueueTrack == null || !QueueTracks.Contains(CurrentQueueTrack))
            {
                return false;
            }

            var currentIndex = QueueTracks.IndexOf(CurrentQueueTrack);

            if (currentIndex <= 0)
            {
                return false;
            }

            PlayTrack(QueueTracks[currentIndex - 1], true);
            return true;
        }

        private void PlayTrack(Track track, bool fromQueue)
        {
            if (track == null)
            {
                return;
            }

            SelectedTrack = track;
            CurrentQueueTrack = fromQueue ? track : null;
            _playerService.Play(track.Path);
        }

        private Track GetRandomTrack()
        {
            if (Tracks.Count == 1)
            {
                return Tracks[0];
            }

            Track randomTrack;

            do
            {
                randomTrack = Tracks[_random.Next(Tracks.Count)];
            }
            while (randomTrack == SelectedTrack);

            return randomTrack;
        }

        private bool FilterTrack(object item)
        {
            if (item is not Track track)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return track.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   track.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateDisplayedTracksView()
        {
            var source = SelectedPlaylist?.Tracks ?? Tracks;
            DisplayedTracksView = CollectionViewSource.GetDefaultView(source);
            DisplayedTracksView.Filter = FilterTrack;
            DisplayedTracksView.Refresh();

            var firstTrack = DisplayedTracksView.Cast<Track>().FirstOrDefault();

            if (SelectedTrack == null || !source.Contains(SelectedTrack))
            {
                SelectedTrack = firstTrack;
            }
        }

        private static Track CreateTrack(string filePath)
        {
            using var tagFile = TagLib.File.Create(filePath);

            return new Track
            {
                Title = string.IsNullOrWhiteSpace(tagFile.Tag.Title)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : tagFile.Tag.Title,
                Artist = tagFile.Tag.FirstPerformer ?? string.Empty,
                Album = tagFile.Tag.Album ?? string.Empty,
                Genre = tagFile.Tag.FirstGenre ?? string.Empty,
                Path = filePath,
                Duration = tagFile.Properties.Duration
            };
        }

        private static string FormatTime(TimeSpan time)
        {
            return time.TotalHours >= 1
                ? time.ToString(@"hh\:mm\:ss")
                : time.ToString(@"mm\:ss");
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
