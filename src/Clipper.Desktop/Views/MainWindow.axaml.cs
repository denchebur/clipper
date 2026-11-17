using Avalonia.Controls;
using Clipper.Desktop.ViewModels;

namespace Clipper.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _shutdownDone;

    public MainWindow()
    {
        InitializeComponent();
        // Передаём модели способ открывать файлы и папки (Проводник / Finder).
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.Launcher = Launcher;
        };
    }

    // При закрытии окна сначала аккуратно останавливаем запись (иначе streamlink может остаться висеть).
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_shutdownDone || DataContext is not MainViewModel vm) return;

        e.Cancel = true;
        await vm.ShutdownAsync();
        _shutdownDone = true;
        Close();
    }
}
