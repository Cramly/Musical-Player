using System;
using System.Windows.Media;
using System.Windows.Threading;

namespace ПлеерОганян
{
    public class PlayerService
    {
        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private readonly DispatcherTimer _timer;
        private string _currentPath;

        public PlayerService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            _mediaPlayer.Volume = 0.5;
            _timer.Tick += (_, _) => RaisePositionChanged();
            _mediaPlayer.MediaOpened += (_, _) => RaisePositionChanged();
        }

        public TimeSpan CurrentPosition => _mediaPlayer.Position;

        public TimeSpan Duration =>
            _mediaPlayer.NaturalDuration.HasTimeSpan
                ? _mediaPlayer.NaturalDuration.TimeSpan
                : TimeSpan.Zero;

        public double Volume => _mediaPlayer.Volume;

        public event Action<TimeSpan, TimeSpan> PositionChanged;

        public void Play(string path)
        {
            if (_currentPath != path)
            {
                _mediaPlayer.Open(new Uri(path));
                _currentPath = path;
            }

            _mediaPlayer.Play();
            _timer.Start();
            RaisePositionChanged();
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            RaisePositionChanged();
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            _timer.Stop();
            RaisePositionChanged();
        }

        public void Seek(TimeSpan position)
        {
            if (Duration == TimeSpan.Zero)
            {
                return;
            }

            if (position < TimeSpan.Zero)
            {
                position = TimeSpan.Zero;
            }

            if (position > Duration)
            {
                position = Duration;
            }

            _mediaPlayer.Position = position;
            RaisePositionChanged();
        }

        public void SetVolume(double volume)
        {
            if (volume < 0)
            {
                volume = 0;
            }

            if (volume > 1)
            {
                volume = 1;
            }

            _mediaPlayer.Volume = volume;
        }

        private void RaisePositionChanged()
        {
            PositionChanged?.Invoke(CurrentPosition, Duration);
        }
    }
}
