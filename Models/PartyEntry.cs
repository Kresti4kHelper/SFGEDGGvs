namespace Kresti4kHelper.Models;

using System.ComponentModel;

public sealed class PartyEntry : INotifyPropertyChanged
{
    private int _count;

    public PartyEntry(string nickname, int initialCount)
    {
        Nickname = nickname;
        InitialCount = initialCount;
        _count = initialCount;
    }

    public string Nickname { get; }

    public int InitialCount { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (_count == value)
            {
                return;
            }

            _count = value;
            OnPropertyChanged(nameof(Count));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reset() => Count = InitialCount;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}