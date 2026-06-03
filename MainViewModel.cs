using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CanvasDrawing
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private double _circleRadius = 50;
        public double CircleRadius
        {
            get => _circleRadius;
            set
            {
                if (_circleRadius != value)
                {
                    _circleRadius = value;
                    OnPropertyChanged();
                }
            }
        }

        public double CenterX { get; } = 200;
        public double CenterY { get; } = 200;

        private string _statusText = "Наведите мышь на круг";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ActivityRecord> Activities { get; } = new();

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set { if (_totalCount != value) { _totalCount = value; OnPropertyChanged(); } }
        }

        private double _totalTime;
        public double TotalTime
        {
            get => _totalTime;
            set { if (_totalTime != value) { _totalTime = value; OnPropertyChanged(); } }
        }

        private readonly DatabaseService _db;
        private DateTime? _hoverStart;
        private CancellationTokenSource? _cts;

        public ICommand MouseEnterCommand { get; }
        public ICommand MouseLeaveCommand { get; }
        public ICommand ClearDbCommand { get; }

        public MainViewModel()
        {
            _db = new DatabaseService();
            MouseEnterCommand = new RelayCommand(async _ => await OnMouseEnter());
            MouseLeaveCommand = new RelayCommand(async _ => await OnMouseLeave());
            ClearDbCommand = new RelayCommand(async _ => await ClearDb());

            _ = LoadDataAsync();
        }

        private async Task OnMouseEnter()
        {
            _hoverStart = DateTime.Now;
            StatusText = "Засчитываем время...";
            await Animate(150);
        }

        private async Task OnMouseLeave()
        {
            if (_hoverStart.HasValue)
            {
                var sec = (DateTime.Now - _hoverStart.Value).TotalSeconds;

                int newId = await _db.SaveActivityAsync(sec);

                var newRecord = new ActivityRecord
                {
                    Id = newId,
                    Time = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy"),
                    Duration = Math.Round(sec, 2)
                };

                Activities.Insert(0, newRecord);
                TotalCount++;
                TotalTime += newRecord.Duration;

                _hoverStart = null;
            }

            StatusText = "Наведите мышь на круг";
            await Animate(50);
        }

        private async Task Animate(double target)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            double start = CircleRadius;
            var t0 = DateTime.Now;

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    double progress = Math.Min((DateTime.Now - t0).TotalMilliseconds / 300.0, 1.0);
                    CircleRadius = start + (target - start) * progress;

                    if (progress >= 1.0) break;

                    await Task.Delay(16, _cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                
            }
        }

        private async Task LoadDataAsync()
        {
            var list = await _db.GetActivitiesAsync();
            Activities.Clear();
            foreach (var r in list)
            {
                Activities.Add(r);
            }

            var sum = await _db.GetSummaryAsync();
            TotalCount = sum.Count;
            TotalTime = sum.TotalTime;
        }

        private async Task ClearDb()
        {
            await _db.ClearAllActivitiesAsync();
            Activities.Clear();
            TotalCount = 0;
            TotalTime = 0;
            StatusText = "База очищена";

            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                if (StatusText == "База очищена")
                    StatusText = "Наведите мышь на круг";
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _db?.Dispose();
        }
    }
}