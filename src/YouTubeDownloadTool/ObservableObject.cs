using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace YouTubeDownloadTool;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool Set<T>(
        [NotNullIfNotNull(nameof(value))] ref T location,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        var shouldDetectChange = !EqualityComparer<T>.Default.Equals(location, value);
        location = value;
        if (!shouldDetectChange) return false;
        OnPropertyChanged(propertyName);
        return true;
    }
}
