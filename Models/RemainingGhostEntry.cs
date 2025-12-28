using System.ComponentModel;

namespace Kresti4kHelper.Models;

public sealed class RemainingGhostEntry : INotifyPropertyChanged
{
    private bool _isRemoved;

    public RemainingGhostEntry(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public bool IsRemoved
    {
        get => _isRemoved;
        set
        {
            if (_isRemoved == value)
            {
                return;
            }

            _isRemoved = value;
            OnPropertyChanged(nameof(IsRemoved));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}