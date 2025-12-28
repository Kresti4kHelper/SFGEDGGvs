using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Kresti4kHelper.Models;

namespace Kresti4kHelper;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string LatestVersionUrl = "https://raw.githubusercontent.com/Kresti4k/Kresti4kHelper-Remake/main/version.json";
    private const string LatestReleasePage = "https://github.com/Kresti4k/Kresti4kHelper-Remake/releases/latest";

    private static readonly Dictionary<Button, CancellationTokenSource> _buttonFlashTokens = new();
    private static readonly Dictionary<Button, (Brush Background, Brush Foreground)> _buttonOriginalBrushes = new();
    private readonly Dictionary<string, ExtendedGhostState> _extendedStats = new();
    private readonly Random _random = new();
    private readonly Stack<Action> _extendedUndoStack = new();
    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static readonly IReadOnlyList<string> GhostNames = new[]
    {
        "БАНШИ", "ДАЙАН", "ДЕОГЕН", "ДЕМОН", "ГАЛЛУ", "ГОРЁ", "ХАНТУ", "ДЖИНН", "МАРА",
        "МОРОЙ", "МЮЛИНГ", "ОБАКЭ", "ОБАМБО", "ОНИ", "ОНРЁ", "ФАНТОМ", "ПОЛТЕРГЕЙСТ",
        "РАЙДЗЮ", "РЕВЕНАНТ", "ТЕНЬ", "ДУХ", "ТАЙЭ", "МИМИК", "БЛИЗНЕЦЫ", "МИРАЖ",
        "ЁКАЙ", "ЮРЭЙ"
    };

    private readonly Stack<Action> _undoStack = new();
    private readonly Stack<Action> _partyUndoStack = new();
    private readonly Stack<Action> _remainingUndoStack = new();
    private bool _isApplyingRemainingChange;
    private bool _isDarkTheme;
    private readonly string _addTaskText = "Добавить";
    private readonly string _saveTaskText = "Сохранить";

    private string _identifiedText = string.Empty;
    private string _rejectedText = string.Empty;
    private string _topGhostText = string.Empty;
    private string _topGhostPercentText = string.Empty;
    private string _extendedIdentifiedText = string.Empty;
    private string _extendedRejectedText = string.Empty;
    private string _extendedTopGhostPercentText = string.Empty;
    private string _randomSelectionText = string.Empty;
    private string? _selectedTaskName;
    private int _totalIdentifiedCount;
    private int _totalRejectedCount;
    private double _totalIdentifiedPercent;
    private double _totalRejectedPercent;
    private int _extendedTotalIdentifiedCount;
    private int _extendedTotalRejectedCount;
    private int _maxGhostCount;
    private string? _topGhostLeaderName;
    private int _topGhostLeaderCount;
    private IReadOnlyList<Ghost> _rankedGhosts = Array.Empty<Ghost>();
    private IReadOnlyList<GhostChartEntry> _chartGhosts = Array.Empty<GhostChartEntry>();
    private IReadOnlyList<GhostChartEntry> _extendedChartGhosts = Array.Empty<GhostChartEntry>();
    private string? _editingTaskName;
    private string _ghostSearchText = string.Empty;
    private string _taskSearchText = string.Empty;

    public MainWindow()
    {
        _isDarkTheme = LoadThemePreference();
        ApplyTheme();

        InitializeComponent();
        TaskNameBox.IsReadOnly = false;
        TaskNameBox.IsHitTestVisible = true;
        TaskNameBox.IsEnabled = true;
        AddTaskButton.IsEnabled = true;
        RandomTaskButton.IsEnabled = true;
        Ghosts = new ObservableCollection<Ghost>(GhostNames.Select((name, index) => new Ghost(name, GetGhostImagePath(index + 1))));
        GhostsView = CollectionViewSource.GetDefaultView(Ghosts);
        GhostsView.Filter = FilterGhosts;
        RemainingGhosts = new ObservableCollection<RemainingGhostEntry>(GhostNames.Select(ToDisplayName).Select(name => new RemainingGhostEntry(name)));
        Tasks = new ObservableCollection<string>(LoadTasks());
        TasksView = CollectionViewSource.GetDefaultView(Tasks);
        TasksView.Filter = FilterTasks;
        PartyEntries = new ObservableCollection<PartyEntry>(LoadPartyEntries());
        SelectedTaskName = LoadSelectedTask();

        foreach (var name in GhostNames)
        {
            _extendedStats[name] = new ExtendedGhostState(name, 0, 0);
        }

        LoadGhostData();
        LoadRemainingStates();
        LoadExtendedStats();

        DataContext = this;
        UpdateStatistics();
        UpdateExtendedStatistics();
        UpdateRemainingGhostsFile();

        Loaded += async (_, _) => await CheckForUpdatesAsync();
    }

    public ObservableCollection<Ghost> Ghosts { get; }

    public ICollectionView GhostsView { get; }

    public ObservableCollection<RemainingGhostEntry> RemainingGhosts { get; }

    public ObservableCollection<PartyEntry> PartyEntries { get; }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (_isDarkTheme == value)
            {
                return;
            }

            _isDarkTheme = value;
            OnPropertyChanged(nameof(IsDarkTheme));
            ApplyTheme();
            SaveThemePreference();
        }
    }

    public ObservableCollection<string> Tasks { get; }
    public ICollectionView TasksView { get; }

    public IReadOnlyList<Ghost> RankedGhosts
    {
        get => _rankedGhosts;
        private set
        {
            if (_rankedGhosts == value)
            {
                return;
            }

            _rankedGhosts = value;
            OnPropertyChanged(nameof(RankedGhosts));
        }
    }

    public IReadOnlyList<GhostChartEntry> ChartGhosts
    {
        get => _chartGhosts;
        private set
        {
            if (_chartGhosts == value)
            {
                return;
            }

            _chartGhosts = value;
            OnPropertyChanged(nameof(ChartGhosts));
        }
    }

    public int MaxGhostCount
    {
        get => _maxGhostCount;
        private set
        {
            if (_maxGhostCount == value)
            {
                return;
            }

            _maxGhostCount = value;
            OnPropertyChanged(nameof(MaxGhostCount));
        }
    }

    public int TotalIdentifiedCount
    {
        get => _totalIdentifiedCount;
        private set
        {
            if (_totalIdentifiedCount == value)
            {
                return;
            }

            _totalIdentifiedCount = value;
            OnPropertyChanged(nameof(TotalIdentifiedCount));
        }
    }

    public int TotalRejectedCount
    {
        get => _totalRejectedCount;
        private set
        {
            if (_totalRejectedCount == value)
            {
                return;
            }

            _totalRejectedCount = value;
            OnPropertyChanged(nameof(TotalRejectedCount));
        }
    }

    public double TotalIdentifiedPercent
    {
        get => _totalIdentifiedPercent;
        private set
        {
            if (Math.Abs(_totalIdentifiedPercent - value) < 0.001)
            {
                return;
            }

            _totalIdentifiedPercent = value;
            OnPropertyChanged(nameof(TotalIdentifiedPercent));
        }
    }

    public double TotalRejectedPercent
    {
        get => _totalRejectedPercent;
        private set
        {
            if (Math.Abs(_totalRejectedPercent - value) < 0.001)
            {
                return;
            }

            _totalRejectedPercent = value;
            OnPropertyChanged(nameof(TotalRejectedPercent));
        }
    }

    public string IdentifiedText
    {
        get => _identifiedText;
        private set
        {
            if (_identifiedText == value)
            {
                return;
            }

            _identifiedText = value;
            OnPropertyChanged(nameof(IdentifiedText));
        }
    }

    public string RejectedText
    {
        get => _rejectedText;
        private set
        {
            if (_rejectedText == value)
            {
                return;
            }

            _rejectedText = value;
            OnPropertyChanged(nameof(RejectedText));
        }
    }

    public string TopGhostText
    {
        get => _topGhostText;
        private set
        {
            if (_topGhostText == value)
            {
                return;
            }

            _topGhostText = value;
            OnPropertyChanged(nameof(TopGhostText));
        }
    }

    public string TopGhostPercentText
    {
        get => _topGhostPercentText;
        private set
        {
            if (_topGhostPercentText == value)
            {
                return;
            }

            _topGhostPercentText = value;
            OnPropertyChanged(nameof(TopGhostPercentText));
        }
    }

    public string ExtendedIdentifiedText
    {
        get => _extendedIdentifiedText;
        private set
        {
            if (_extendedIdentifiedText == value)
            {
                return;
            }

            _extendedIdentifiedText = value;
            OnPropertyChanged(nameof(ExtendedIdentifiedText));
        }
    }

    public string ExtendedRejectedText
    {
        get => _extendedRejectedText;
        private set
        {
            if (_extendedRejectedText == value)
            {
                return;
            }

            _extendedRejectedText = value;
            OnPropertyChanged(nameof(ExtendedRejectedText));
        }
    }

    public string ExtendedTopGhostPercentText
    {
        get => _extendedTopGhostPercentText;
        private set
        {
            if (_extendedTopGhostPercentText == value)
            {
                return;
            }

            _extendedTopGhostPercentText = value;
            OnPropertyChanged(nameof(ExtendedTopGhostPercentText));
        }
    }

    public string AppVersion { get; } = $"Made with ❤️ by Xinoki · Version {GetAppVersion()}";

    public string GhostSearchText
    {
        get => _ghostSearchText;
        set
        {
            if (_ghostSearchText == value)
            {
                return;
            }

            _ghostSearchText = value;
            OnPropertyChanged(nameof(GhostSearchText));
            GhostsView.Refresh();
        }
    }

    public string TaskSearchText
    {
        get => _taskSearchText;
        set
        {
            if (_taskSearchText == value)
            {
                return;
            }

            _taskSearchText = value;
            OnPropertyChanged(nameof(TaskSearchText));
            TasksView.Refresh();
        }
    }

    public string? SelectedTaskName
    {
        get => _selectedTaskName;
        private set
        {
            if (_selectedTaskName == value)
            {
                return;
            }

            _selectedTaskName = value;
            OnPropertyChanged(nameof(SelectedTaskName));
        }
    }

    public string RandomSelectionText
    {
        get => _randomSelectionText;
        private set
        {
            if (_randomSelectionText == value)
            {
                return;
            }

            _randomSelectionText = value;
            OnPropertyChanged(nameof(RandomSelectionText));
        }
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanUndoRemaining => _remainingUndoStack.Count > 0;

    public bool CanUndoExtended => _extendedUndoStack.Count > 0;

    public bool CanUndoParty => _partyUndoStack.Count > 0;

    public int ExtendedTotalIdentifiedCount
    {
        get => _extendedTotalIdentifiedCount;
        private set
        {
            if (_extendedTotalIdentifiedCount == value)
            {
                return;
            }

            _extendedTotalIdentifiedCount = value;
            OnPropertyChanged(nameof(ExtendedTotalIdentifiedCount));
        }
    }

    public int ExtendedTotalRejectedCount
    {
        get => _extendedTotalRejectedCount;
        private set
        {
            if (_extendedTotalRejectedCount == value)
            {
                return;
            }

            _extendedTotalRejectedCount = value;
            OnPropertyChanged(nameof(ExtendedTotalRejectedCount));
        }
    }

    public IReadOnlyList<GhostChartEntry> ExtendedChartGhosts
    {
        get => _extendedChartGhosts;
        private set
        {
            if (_extendedChartGhosts == value)
            {
                return;
            }

            _extendedChartGhosts = value;
            OnPropertyChanged(nameof(ExtendedChartGhosts));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void OnIdentifyGhost(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Ghost ghost)
        {
            return;
        }

        SetGhostStatus(ghost, GhostStatus.Identified);
        await FlashButtonAsync(sender as Button, Brushes.MediumSeaGreen);
    }

    private async void OnRejectGhost(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Ghost ghost)
        {
            return;
        }

        SetGhostStatus(ghost, GhostStatus.Rejected);
        await FlashButtonAsync(sender as Button, Brushes.IndianRed);
    }

    private void OnResetData(object sender, RoutedEventArgs e)
    {
        var snapshot = Ghosts.Select(g => new GhostState(g.Name, g.IdentifiedCount, g.RejectedCount, g.Status)).ToList();

        foreach (var ghost in Ghosts)
        {
            ghost.ResetState();
        }

        PushUndoAction(() => RestoreGhostStates(snapshot));
        UpdateStatistics();
        SaveGhostData();
    }

    private void OnResetExtendedStatistics(object sender, RoutedEventArgs e)
    {
        var snapshot = _extendedStats.Values.Select(state => new ExtendedGhostState(state.Name, state.IdentifiedCount, state.RejectedCount)).ToList();

        foreach (var key in _extendedStats.Keys.ToList())
        {
            _extendedStats[key] = new ExtendedGhostState(key, 0, 0);
        }

        PushExtendedUndoAction(() => RestoreExtendedStates(snapshot));
        UpdateExtendedStatistics();
        SaveExtendedStats();
    }

    private void OnUndoExtendedStatistics(object sender, RoutedEventArgs e)
    {
        if (_extendedUndoStack.Count == 0)
        {
            return;
        }

        var undo = _extendedUndoStack.Pop();
        undo();
        UpdateExtendedStatistics();
        SaveExtendedStats();
        OnPropertyChanged(nameof(CanUndoExtended));
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var undo = _undoStack.Pop();
        undo();
        UpdateStatistics();
        SaveGhostData();
        OnPropertyChanged(nameof(CanUndo));
    }

    private void OnRemainingGhostToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingRemainingChange)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not RemainingGhostEntry entry)
        {
            return;
        }

        var previous = !entry.IsRemoved;
        PushRemainingUndoAction(() => SetRemainingGhostState(entry, previous));
        UpdateRemainingGhostsFile();
    }

    private void OnResetRemainingGhosts(object sender, RoutedEventArgs e)
    {
        var snapshot = RemainingGhosts.Select(entry => new RemainingGhostState(entry, entry.IsRemoved)).ToList();

        SetRemainingGhostStates(RemainingGhosts, false);

        PushRemainingUndoAction(() => RestoreRemainingGhostStates(snapshot));
        UpdateRemainingGhostsFile();
    }

    private void OnUndoRemaining(object sender, RoutedEventArgs e)
    {
        if (_remainingUndoStack.Count == 0)
        {
            return;
        }

        var undo = _remainingUndoStack.Pop();
        ApplyRemainingChange(undo);
        UpdateRemainingGhostsFile();
        OnPropertyChanged(nameof(CanUndoRemaining));
    }

    private void OnAddPartyEntry(object sender, RoutedEventArgs e)
    {
        var nickname = PartyNicknameBox.Text.Trim();
        var initialText = PartyInitialCountBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            return;
        }

        if (!int.TryParse(initialText, out var initialCount))
        {
            initialCount = 0;
        }

        var entry = new PartyEntry(nickname, initialCount);
        PartyEntries.Add(entry);
        SavePartyEntries();

        PushPartyUndoAction(() =>
        {
            PartyEntries.Remove(entry);
            SavePartyEntries();
        });

        PartyNicknameBox.Clear();
        PartyInitialCountBox.Text = "0";
    }

    private void OnIncrementPartyEntry(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PartyEntry entry)
        {
            return;
        }

        var previous = entry.Count;
        entry.Count++;
        SavePartyEntries();
        PushPartyUndoAction(() =>
        {
            entry.Count = previous;
            SavePartyEntries();
        });
    }

    private void OnDecrementPartyEntry(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PartyEntry entry)
        {
            return;
        }

        var previous = entry.Count;
        entry.Count--;
        SavePartyEntries();
        PushPartyUndoAction(() =>
        {
            entry.Count = previous;
            SavePartyEntries();
        });
    }

    private void OnDeletePartyEntry(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PartyEntry entry)
        {
            return;
        }

        var index = PartyEntries.IndexOf(entry);

        if (index < 0)
        {
            return;
        }

        PartyEntries.RemoveAt(index);
        SavePartyEntries();

        PushPartyUndoAction(() =>
        {
            PartyEntries.Insert(index, entry);
            SavePartyEntries();
        });
    }

    private void OnResetPartyEntries(object sender, RoutedEventArgs e)
    {
        var snapshot = PartyEntries.Select(p => new PartyEntryState(p, p.Count)).ToList();

        foreach (var entry in PartyEntries)
        {
            entry.Reset();
        }

        SavePartyEntries();

        PushPartyUndoAction(() =>
        {
            RestorePartyCounts(snapshot);
            SavePartyEntries();
        });
    }

    private void OnUndoParty(object sender, RoutedEventArgs e)
    {
        if (_partyUndoStack.Count == 0)
        {
            return;
        }

        var undo = _partyUndoStack.Pop();
        undo();
        OnPropertyChanged(nameof(CanUndoParty));
    }

    private void OnAddTask(object sender, RoutedEventArgs e)
    {
        var taskName = TaskNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        var isEditing = _editingTaskName != null;

        if (Tasks.Any(t => string.Equals(t, taskName, StringComparison.OrdinalIgnoreCase) && !string.Equals(t, _editingTaskName, StringComparison.OrdinalIgnoreCase)))
        {
            TaskNameBox.Clear();
            return;
        }

        if (isEditing)
        {
            ApplyTaskEdit(taskName);
        }
        else
        {
            Tasks.Add(taskName);
            TaskNameBox.Clear();
        }

        SaveTasks();
    }

    private void ApplyTaskEdit(string newName)
    {
        var index = Tasks
            .Select((name, idx) => (name, idx))
            .FirstOrDefault(pair => string.Equals(pair.name, _editingTaskName, StringComparison.OrdinalIgnoreCase)).idx;

        if (index >= 0)
        {
            Tasks[index] = newName;

            if (string.Equals(SelectedTaskName, _editingTaskName, StringComparison.OrdinalIgnoreCase))
            {
                WriteSelectedTask(newName);
            }
        }

        ResetTaskEditor();
    }

    private void OnDeleteTask(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string taskName)
        {
            return;
        }

        var index = Tasks.ToList().FindIndex(name => string.Equals(name, taskName, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return;
        }

        Tasks.RemoveAt(index);

        if (string.Equals(SelectedTaskName, taskName, StringComparison.OrdinalIgnoreCase))
        {
            {
                SelectedTaskName = null;
                WriteSelectedTask(string.Empty);
            }

            if (string.Equals(_editingTaskName, taskName, StringComparison.OrdinalIgnoreCase))
            {
                ResetTaskEditor();
            }

            SaveTasks();
        }
    }

    private void OnSelectTask(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string taskName)
        {
            return;
        }

        WriteSelectedTask(taskName);
    }

    private async void OnSelectRandomTask(object sender, RoutedEventArgs e)
    {
        if (Tasks.Count == 0)
        {
            RandomSelectionText = "Нет заданий для выбора";
            return;
        }

        if (sender is Button button)
        {
            button.IsEnabled = false;
        }

        var iterations = Math.Min(Tasks.Count * 2, 12);

        for (var i = 0; i < iterations; i++)
        {
            var preview = Tasks[_random.Next(Tasks.Count)];
            RandomSelectionText = $"Случайный выбор: {preview}";
            await Task.Delay(TimeSpan.FromMilliseconds(120));
        }

        var taskName = Tasks[_random.Next(Tasks.Count)];
        WriteSelectedTask(taskName);
        RandomSelectionText = $"Выбрано: {taskName}";

        if (sender is Button finalButton)
        {
            await FlashButtonAsync(finalButton, Brushes.MediumPurple);
            finalButton.IsEnabled = true;
        }
    }

    private void OnEditTask(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string taskName)
        {
            return;
        }

        _editingTaskName = taskName;
        TaskNameBox.Text = taskName;
        TaskNameBox.Focus();
        TaskNameBox.SelectAll();
        AddTaskButton.Content = _saveTaskText;
    }

    private void ResetTaskEditor()
    {
        _editingTaskName = null;
        TaskNameBox.Clear();
        AddTaskButton.Content = _addTaskText;
    }

    private void SetGhostStatus(Ghost ghost, GhostStatus status)
    {
        var previousState = new GhostState(ghost.Name, ghost.IdentifiedCount, ghost.RejectedCount, ghost.Status);
        ghost.RegisterSelection(status);
        RegisterExtendedSelection(ghost.Name, status);
        PushUndoAction(() => ApplyGhostState(ghost, previousState));
        UpdateStatistics();
        UpdateExtendedStatistics();
        SaveGhostData();
        SaveExtendedStats();
    }

    private void ApplyGhostState(Ghost ghost, GhostState state)
    {
        ghost.ApplyState(state.IdentifiedCount, state.RejectedCount, state.Status);
    }

    private void RestoreGhostStates(IEnumerable<GhostState> states)
    {
        foreach (var state in states)
        {
            var ghost = Ghosts.FirstOrDefault(g => g.Name == state.Name);

            if (ghost != null)
            {
                ApplyGhostState(ghost, state);
            }
        }
    }

    private void UpdateStatistics()
    {
        var identified = Ghosts.Sum(g => g.IdentifiedCount);
        var rejected = Ghosts.Sum(g => g.RejectedCount);
        var topGhost = GetTopGhost();
        var totalSelections = identified + rejected;
        var identifiedPercent = totalSelections > 0
            ? Math.Round(identified / (double)totalSelections * 100, 1)
            : 0;
        var rejectedPercent = totalSelections > 0
            ? Math.Round(rejected / (double)totalSelections * 100, 1)
            : 0;

        TotalIdentifiedCount = identified;
        TotalRejectedCount = rejected;
        TotalIdentifiedPercent = identifiedPercent;
        TotalRejectedPercent = rejectedPercent;

        IdentifiedText = $"ОПРЕДЕЛЕНО: {identified} ({identifiedPercent}%)";
        RejectedText = $"НЕ ОПРЕДЕЛЕНО: {rejected} ({rejectedPercent}%)";
        TopGhostText = topGhost is { TotalCount: > 0 }
            ? $"ЛИДЕР: {topGhost.Name} ({topGhost.TotalCount})"
            : "ЛИДЕР: нет данных";

        if (topGhost is { TotalCount: > 0 } && totalSelections > 0)
        {
            var percent = Math.Round(topGhost.TotalCount / (double)totalSelections * 100, 1);
            TopGhostPercentText = $"Самый частый призрак встречается в {percent}% матчей";
        }
        else
        {
            TopGhostPercentText = "Недостаточно данных для подсчёта";
        }

        MaxGhostCount = Math.Max(1, Ghosts.Select(g => g.TotalCount).DefaultIfEmpty(0).Max());
        RankedGhosts = Ghosts
            .Where(g => g.TotalCount > 0)
            .OrderByDescending(g => g.TotalCount)
            .ThenBy(g => g.Name)
            .ToList();
        ChartGhosts = RankedGhosts
            .Select(g => new GhostChartEntry(g.Name, g.IdentifiedCount, g.RejectedCount, totalSelections))
            .ToList();

        WriteStatisticsForObs(identified, rejected);
        WriteTopGhostFile(topGhost);

        OnPropertyChanged(nameof(CanUndo));
    }

    private void UpdateExtendedStatistics()
    {
        var identified = _extendedStats.Values.Sum(s => s.IdentifiedCount);
        var rejected = _extendedStats.Values.Sum(s => s.RejectedCount);
        var topGhost = GetExtendedTopGhost();
        var totalSelections = identified + rejected;

        ExtendedTotalIdentifiedCount = identified;
        ExtendedTotalRejectedCount = rejected;

        var identifiedPercent = totalSelections > 0
            ? Math.Round(identified / (double)totalSelections * 100, 1)
            : 0;
        var rejectedPercent = totalSelections > 0
            ? Math.Round(rejected / (double)totalSelections * 100, 1)
            : 0;

        ExtendedIdentifiedText = $"ОПРЕДЕЛЕНО: {identified} ({identifiedPercent}%)";
        ExtendedRejectedText = $"НЕ ОПРЕДЕЛЕНО: {rejected} ({rejectedPercent}%)";

        if (topGhost is { TotalCount: > 0 } && totalSelections > 0)
        {
            var percent = Math.Round(topGhost.TotalCount / (double)totalSelections * 100, 1);
            ExtendedTopGhostPercentText = $"Самый частый призрак встречается в {percent}% матчей";
        }
        else
        {
            ExtendedTopGhostPercentText = "Недостаточно данных для подсчёта";
        }

        ExtendedChartGhosts = _extendedStats.Values
            .Where(s => s.TotalCount > 0)
            .OrderByDescending(s => s.TotalCount)
            .ThenBy(s => s.Name)
            .Select(s => new GhostChartEntry(s.Name, s.IdentifiedCount, s.RejectedCount, totalSelections))
            .ToList();
    }

    private Ghost? GetTopGhost()
    {
        var maxCount = Ghosts.Select(g => g.TotalCount).DefaultIfEmpty(0).Max();

        if (maxCount <= 0)
        {
            _topGhostLeaderName = null;
            _topGhostLeaderCount = 0;
            return null;
        }

        var currentLeader = _topGhostLeaderName is null
            ? null
            : Ghosts.FirstOrDefault(g => g.Name == _topGhostLeaderName);

        if (currentLeader?.TotalCount == maxCount)
        {
            _topGhostLeaderCount = maxCount;
            return currentLeader;
        }

        var newLeader = Ghosts.First(g => g.TotalCount == maxCount);

        _topGhostLeaderName = newLeader.Name;
        _topGhostLeaderCount = maxCount;

        return newLeader;
    }

    private ExtendedGhostState? GetExtendedTopGhost()
    {
        return _extendedStats.Values
            .OrderByDescending(s => s.TotalCount)
            .ThenBy(s => s.Name)
            .FirstOrDefault();
    }

    private bool FilterGhosts(object? item)
    {
        if (item is not Ghost ghost)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(GhostSearchText))
        {
            return true;
        }

        return ghost.Name.Contains(GhostSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterTasks(object? item)
    {
        if (item is not string taskName)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(TaskSearchText))
        {
            return true;
        }

        return taskName.Contains(TaskSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetGhostImagePath(int ghostNumber)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", $"{ghostNumber}.jpg");
        return File.Exists(path) ? path : null;
    }

    private static async Task FlashButtonAsync(Button? button, Brush highlight)
    {
        if (button is null)
        {
            return;
        }

        if (_buttonFlashTokens.TryGetValue(button, out var existingToken))
        {
            existingToken.Cancel();
            existingToken.Dispose();
        }

        if (!_buttonOriginalBrushes.ContainsKey(button))
        {
            _buttonOriginalBrushes[button] = (button.Background, button.Foreground);
        }

        var cts = new CancellationTokenSource();
        _buttonFlashTokens[button] = cts;

        button.Background = highlight;
        button.Foreground = Brushes.White;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        finally
        {
            if (_buttonFlashTokens.TryGetValue(button, out var token) && token == cts)
            {
                _buttonFlashTokens.Remove(button);
            }

            cts.Dispose();
        }

        if (_buttonOriginalBrushes.TryGetValue(button, out var brushes))
        {
            button.Background = brushes.Background;
            button.Foreground = brushes.Foreground;
        }
    }

    private void ApplyTheme()
    {
        var resources = Application.Current.Resources;
        resources.MergedDictionaries.Clear();

        var themePath = IsDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) });
    }

    private void SaveGhostData()
    {
        var data = Ghosts
            .Select(g => new GhostState(g.Name, g.IdentifiedCount, g.RejectedCount, g.Status))
            .ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ghosts_data.json"), json);
    }

    private void SaveExtendedStats()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var data = _extendedStats.Values.ToList();
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extended_ghosts_data.json"), json);
    }

    private void RegisterExtendedSelection(string ghostName, GhostStatus status)
    {
        if (!_extendedStats.TryGetValue(ghostName, out var state))
        {
            state = new ExtendedGhostState(ghostName, 0, 0);
        }

        var previousState = state;
        var identified = state.IdentifiedCount + (status == GhostStatus.Identified ? 1 : 0);
        var rejected = state.RejectedCount + (status == GhostStatus.Rejected ? 1 : 0);

        _extendedStats[ghostName] = new ExtendedGhostState(ghostName, identified, rejected);
        PushExtendedUndoAction(() => _extendedStats[ghostName] = previousState);
    }

    private void LoadGhostData()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ghosts_data.json");

            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<List<GhostState>>(json);

            if (data == null)
            {
                return;
            }

            foreach (var state in data)
            {
                var ghost = Ghosts.FirstOrDefault(g => g.Name == state.Name);
                ghost?.ApplyState(state.IdentifiedCount, state.RejectedCount, state.Status);
            }
        }
        catch
        {
        }
    }

    private void LoadExtendedStats()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extended_ghosts_data.json");

            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<List<ExtendedGhostState>>(json);

            if (data == null)
            {
                return;
            }

            foreach (var state in data)
            {
                _extendedStats[state.Name] = state;
            }
        }
        catch
        {
        }
    }

    private void SavePartyEntries()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var payload = PartyEntries.Select(p => new PartyEntryPersisted(p.Nickname, p.Count, p.InitialCount)).ToList();
        var json = JsonSerializer.Serialize(payload, options);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "party_entries.json"), json);
    }

    private List<PartyEntry> LoadPartyEntries()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "party_entries.json");

            if (!File.Exists(path))
            {
                return new List<PartyEntry>();
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<List<PartyEntryPersisted>>(json);

            if (data == null)
            {
                return new List<PartyEntry>();
            }

            return data.Select(persisted =>
            {
                var entry = new PartyEntry(persisted.Nickname, persisted.InitialCount);
                entry.Count = persisted.Count;
                return entry;
            }).ToList();
        }
        catch
        {
            return new List<PartyEntry>();
        }
    }

    private void SaveTasks()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(Tasks.ToList(), options);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasks.json"), json);
    }

    private List<string> LoadTasks()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasks.json");

            if (!File.Exists(path))
            {
                return new List<string>();
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<List<string>>(json);
            return data ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private bool LoadThemePreference()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.txt");

            if (!File.Exists(path))
            {
                return false;
            }

            var content = File.ReadAllText(path).Trim();
            return string.Equals(content, "dark", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void SaveThemePreference()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.txt");
        File.WriteAllText(path, IsDarkTheme ? "dark" : "light");
    }

    private string? LoadSelectedTask()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selected_task.txt");

            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private void WriteSelectedTask(string taskName)
    {
        SelectedTaskName = string.IsNullOrWhiteSpace(taskName) ? null : taskName;
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selected_task.txt"), taskName);
        OnPropertyChanged(nameof(SelectedTaskName));
    }

    private void WriteStatisticsForObs(int identified, int rejected)
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        File.WriteAllText(Path.Combine(basePath, "identified.txt"), identified.ToString());
        File.WriteAllText(Path.Combine(basePath, "rejected.txt"), rejected.ToString());
    }

    private void WriteTopGhostFile(Ghost? topGhost)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "top_ghost.txt");

        if (topGhost is null || topGhost.TotalCount == 0)
        {
            File.WriteAllText(path, string.Empty);
            return;
        }

        File.WriteAllText(path, $"{topGhost.Name} ({topGhost.TotalCount})");
    }

    private void UpdateRemainingGhostsFile()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var content = FormatRemainingGhosts();
        File.WriteAllText(Path.Combine(basePath, "remaining_ghosts.txt"), content);

        SaveRemainingState();

        var columnContents = FormatRemainingGhostColumns();

        for (var index = 0; index < columnContents.Count; index++)
        {
            var path = Path.Combine(basePath, $"remaining_ghosts_{index + 1}.txt");
            File.WriteAllText(path, columnContents[index]);
        }
    }

    private void SaveRemainingState()
    {
        var state = RemainingGhosts
            .Select(entry => new RemainingGhostPersisted(entry.DisplayName, entry.IsRemoved))
            .ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(state, options);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remaining_ghosts_state.json"), json);
    }

    private string FormatRemainingGhosts()
    {
        const int columns = 3;
        var entries = RemainingGhosts.ToList();
        var cellWidth = entries.Select(e => e.DisplayName.Length).DefaultIfEmpty(0).Max() + 2;
        var lines = new List<string>();

        for (var i = 0; i < entries.Count; i += columns)
        {
            var row = entries.Skip(i)
                .Take(columns)
                .Select(e => (e.IsRemoved ? string.Empty : e.DisplayName).PadRight(cellWidth));

            lines.Add(string.Join(string.Empty, row));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IReadOnlyList<string> FormatRemainingGhostColumns()
    {
        const int columns = 3;
        var entries = RemainingGhosts.ToList();
        var rows = (int)Math.Ceiling(entries.Count / (double)columns);

        var columnEntries = Enumerable.Range(0, columns)
            .Select(_ => new List<string>(rows))
            .ToList();

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;

                if (index >= entries.Count)
                {
                    columnEntries[column].Add(string.Empty);
                    continue;
                }

                var entry = entries[index];
                columnEntries[column].Add(entry.IsRemoved ? string.Empty : entry.DisplayName);
            }
        }

        return columnEntries
            .Select(column => string.Join(Environment.NewLine, column))
            .ToList();
    }

    private void LoadRemainingStates()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remaining_ghosts_state.json");

            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<List<RemainingGhostPersisted>>(json);

            if (data == null)
            {
                return;
            }

            foreach (var state in data)
            {
                var entry = RemainingGhosts.FirstOrDefault(e => string.Equals(e.DisplayName, state.Name, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    entry.IsRemoved = state.IsRemoved;
                }
            }
        }
        catch
        {
        }
    }

    private void PushUndoAction(Action undo)
    {
        _undoStack.Push(undo);
        OnPropertyChanged(nameof(CanUndo));
    }

    private void PushRemainingUndoAction(Action undo)
    {
        _remainingUndoStack.Push(undo);
        OnPropertyChanged(nameof(CanUndoRemaining));
    }

    private void PushExtendedUndoAction(Action undo)
    {
        _extendedUndoStack.Push(undo);
        OnPropertyChanged(nameof(CanUndoExtended));
    }

    private void PushPartyUndoAction(Action undo)
    {
        _partyUndoStack.Push(undo);
        OnPropertyChanged(nameof(CanUndoParty));
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(LatestVersionUrl).ConfigureAwait(true);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (manifest?.Version is null)
            {
                return;
            }

            var currentVersion = GetCurrentVersion();
            if (Version.TryParse(manifest.Version, out var latestVersion) && latestVersion > currentVersion)
            {
                var result = MessageBox.Show(
                    $"Доступна новая версия ({latestVersion}). Открыть страницу загрузки?",
                    "Доступно обновление",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = LatestReleasePage,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            MessageBox.Show(
                "Не удалось проверить наличие обновления. Проверьте подключение к интернету. Приложение продолжит работу.",
                "Ошибка проверки обновления",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        var currentVersion = GetCurrentVersion();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Kresti4kHelper/{currentVersion}");
        return client;
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
    }

    private static string GetAppVersion()
    {
        var version = GetCurrentVersion();
        return version.ToString(3);
    }

    private void RestoreRemainingGhostStates(IEnumerable<RemainingGhostState> states)
    {
        SetRemainingGhostStates(states.Select(s => s.Entry), states.Select(s => s.IsRemoved));
    }

    private void RestoreExtendedStates(IEnumerable<ExtendedGhostState> states)
    {
        foreach (var state in states)
        {
            _extendedStats[state.Name] = state;
        }
    }

    private void RestorePartyCounts(IEnumerable<PartyEntryState> states)
    {
        foreach (var state in states)
        {
            state.Entry.Count = state.Count;
        }
    }

    private static string ToDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var lowered = name.ToLower();
        return char.ToUpper(lowered[0]) + lowered[1..];
    }

    private sealed record UpdateManifest(string Version);

    private sealed record GhostState(string Name, int IdentifiedCount, int RejectedCount, GhostStatus Status);

    private sealed record RemainingGhostState(RemainingGhostEntry Entry, bool IsRemoved);

    private sealed record RemainingGhostPersisted(string Name, bool IsRemoved);

    private sealed record ExtendedGhostState(string Name, int IdentifiedCount, int RejectedCount)
    {
        public int TotalCount => IdentifiedCount + RejectedCount;
    }

    private sealed record PartyEntryState(PartyEntry Entry, int Count);

    private sealed record PartyEntryPersisted(string Nickname, int Count, int InitialCount);

    private void SetRemainingGhostStates(IEnumerable<RemainingGhostEntry> entries, IEnumerable<bool> values)
    {
        ApplyRemainingChange(() =>
        {
            foreach (var pair in entries.Zip(values))
            {
                pair.First.IsRemoved = pair.Second;
            }
        });
    }

    private void SetRemainingGhostStates(IEnumerable<RemainingGhostEntry> entries, bool value)
    {
        ApplyRemainingChange(() =>
        {
            foreach (var entry in entries)
            {
                entry.IsRemoved = value;
            }
        });
    }

    private void SetRemainingGhostState(RemainingGhostEntry entry, bool isRemoved)
    {
        ApplyRemainingChange(() => entry.IsRemoved = isRemoved);
    }

    private void ApplyRemainingChange(Action action)
    {
        _isApplyingRemainingChange = true;

        try
        {
            action();
        }
        finally
        {
            _isApplyingRemainingChange = false;
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}