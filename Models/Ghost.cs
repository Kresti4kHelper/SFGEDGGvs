using System;
using System.ComponentModel;

namespace Kresti4kHelper.Models;

public sealed class Ghost : INotifyPropertyChanged
{
    private GhostStatus _status = GhostStatus.Unknown;
    private int _identifiedCount;
    private int _rejectedCount;

    public Ghost(string name, string? imagePath = null)
    {
        Name = name;
        ImagePath = imagePath;
    }

    public string Name { get; }

    public string? ImagePath { get; }

    public GhostStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public int IdentifiedCount
    {
        get => _identifiedCount;
        private set
        {
            if (_identifiedCount == value)
            {
                return;
            }

            _identifiedCount = value;
            OnPropertyChanged(nameof(IdentifiedCount));
            OnPropertyChanged(nameof(TotalCount));
        }
    }

    public int RejectedCount
    {
        get => _rejectedCount;
        private set
        {
            if (_rejectedCount == value)
            {
                return;
            }

            _rejectedCount = value;
            OnPropertyChanged(nameof(RejectedCount));
            OnPropertyChanged(nameof(TotalCount));
        }
    }

    public int TotalCount => IdentifiedCount + RejectedCount;

    public void ApplyState(int identified, int rejected, GhostStatus status)
    {
        IdentifiedCount = Math.Max(0, identified);
        RejectedCount = Math.Max(0, rejected);
        Status = status;
    }

    public void ResetState()
    {
        IdentifiedCount = 0;
        RejectedCount = 0;
        Status = GhostStatus.Unknown;
    }

    public void RegisterSelection(GhostStatus status)
    {
        switch (status)
        {
            case GhostStatus.Identified:
                IdentifiedCount += 1;
                break;
            case GhostStatus.Rejected:
                RejectedCount += 1;
                break;
        }

        Status = status;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}