using System;

namespace Kresti4kHelper.Models;

public sealed class GhostChartEntry
{
    public GhostChartEntry(string name, int identifiedCount, int rejectedCount, int totalSelections)
    {
        Name = name;
        IdentifiedCount = identifiedCount;
        RejectedCount = rejectedCount;
        TotalCount = identifiedCount + rejectedCount;
        TotalSharePercent = totalSelections > 0
            ? Math.Round(TotalCount / (double)totalSelections * 100, 1)
            : 0;
    }

    public string Name { get; }

    public int IdentifiedCount { get; }

    public int RejectedCount { get; }

    public int TotalCount { get; }

    public double TotalSharePercent { get; }

    public double IdentifiedPercent => TotalCount > 0
        ? Math.Round(IdentifiedCount / (double)TotalCount * 100, 1)
        : 0;

    public double RejectedPercent => TotalCount > 0
        ? Math.Round(RejectedCount / (double)TotalCount * 100, 1)
        : 0;

    public string IdentifiedLabel => $"Определено {IdentifiedCount} ({IdentifiedPercent}%)";

    public string RejectedLabel => $"Не определено {RejectedCount} ({RejectedPercent}%)";

    public string TotalLabel => $"Всего: {TotalCount}";

    public string TotalShareText => TotalSharePercent > 0
        ? $"{TotalSharePercent}% от всех матчей"
        : "Нет данных";
}