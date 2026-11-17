using CommunityToolkit.Mvvm.ComponentModel;

namespace Clipper.Desktop.ViewModels;

/// <summary>Строка в списке стримов: ссылка и состояние записи.</summary>
public partial class StreamRow : ObservableObject
{
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isRecording;
}
