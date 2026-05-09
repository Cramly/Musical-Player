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
        private readonly Random _random = new Random();
        private Track _selectedTrack;
        private TimeSpan _currentPosition;
        private TimeSpan _duration;
        private double _sliderPosition;
        private double _volume;
        private bool _isUpdatingFromPlayer;
        private string _searchText = string.Empty;
        private bool _isShuffleEnabled;
        private bool _isRepeatTrackEnabled;
        private bool _isRepeatPlaylistEnabled;

        public MainViewModel()
        {
            TracksView = CollectionViewSource.GetDefaultView(Tracks);
            TracksView.Filter = FilterTrack;

            LoadFolderCommand = new RelayCommand(LoadFolder);
            _playCommand = new RelayCommand(Play, () => SelectedTrack != null);
            _pauseCommand = new RelayCommand(Pause);
            _stopCommand = new RelayCommand(Stop);
            _nextCommand = new RelayCommand(Next, () => Tracks.Count > 0 && SelectedTrack != null);
            _previousCommand = new RelayCommand(Previous, () => Tracks.Count > 0 && SelectedTrack != null);

            _playerService.PositionChanged += OnPlayerPositionChanged;
            _playerService.TrackEnded += OnTrackEnded;
            Volume = _playerService.Volume;
        }

        public ObservableCollection<Track> Tracks { get; } = new ObservableCollection<Track>();

        public ICollectionView TracksView { get; }

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
                TracksView.Refresh();
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

        public string SelectedTrackTitle => SelectedTrack?.Title ?? "Трек не выбран";

        public string CurrentPositionText => FormatTime(CurrentPosition);

        public string DurationText => FormatTime(Duration);

        public ICommand LoadFolderCommand { get; }

        public ICommand PlayCommand => _playCommand;

        public ICommand PauseCommand => _pauseCommand;

        public ICommand StopCommand => _stopCommand;

        public ICommand NextCommand => _nextCommand;

        public ICommand PreviousCommand => _previousCommand;

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

            TracksView.Refresh();
            SelectedTrack = TracksView.Cast<Track>().FirstOrDefault();
            Stop();
            _nextCommand.RaiseCanExecuteChanged();
            _previousCommand.RaiseCanExecuteChanged();
        }

        private void Play()
        {
            if (SelectedTrack == null || !File.Exists(SelectedTrack.Path))
            {
                return;
            }

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
            if (Tracks.Count == 0 || SelectedTrack == null)
            {
                return;
            }

            if (IsShuffleEnabled)
            {
                SelectedTrack = GetRandomTrack();
                Play();
                return;
            }

            var currentIndex = Tracks.IndexOf(SelectedTrack);

            if (currentIndex < 0)
            {
                return;
            }

            var nextIndex = (currentIndex + 1) % Tracks.Count;
            SelectedTrack = Tracks[nextIndex];
            Play();
        }

        private void Previous()
        {
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
            SelectedTrack = Tracks[previousIndex];
            Play();
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
            if (SelectedTrack == null || Tracks.Count == 0)
            {
                return;
            }

            if (IsRepeatTrackEnabled)
            {
                Play();
                return;
            }

            if (IsShuffleEnabled)
            {
                SelectedTrack = GetRandomTrack();
                Play();
                return;
            }

            var currentIndex = Tracks.IndexOf(SelectedTrack);

            if (currentIndex < 0)
            {
                return;
            }

            if (currentIndex < Tracks.Count - 1)
            {
                SelectedTrack = Tracks[currentIndex + 1];
                Play();
                return;
            }

            if (IsRepeatPlaylistEnabled)
            {
                SelectedTrack = Tracks[0];
                Play();
                return;
            }

            Stop();
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
