using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using RedeScan.Core.Models;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class CamerasView : UserControl
{
    private CamerasViewModel? _vm;

    public CamerasView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ---- Importar/Exportar rtspList.json (formato CentralMonitor) ----
    private static readonly FilePickerFileType JsonType = new("Arquivos JSON")
    {
        Patterns = new[] { "*.json" }
    };

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar rtspList.json",
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonType }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path) && _vm.ImportRtspListCommand.CanExecute(path))
        {
            _vm.ImportRtspListCommand.Execute(path);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar rtspList.json",
            SuggestedFileName = "rtspList.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonType }
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path) && _vm.ExportRtspListCommand.CanExecute(path))
        {
            _vm.ExportRtspListCommand.Execute(path);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CamerasViewModel vm && vm.LoadCommand.CanExecute(null))
        {
            vm.LoadCommand.Execute(null);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PlayRequested -= OnPlayRequested;
            _vm.StopRequested -= OnStopRequested;
        }

        _vm = DataContext as CamerasViewModel;

        if (_vm is not null)
        {
            _vm.PlayRequested += OnPlayRequested;
            _vm.StopRequested += OnStopRequested;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
#if ENABLE_NATIVE_MEDIA
        DisposeNative();
#else
        OnStopRequested();
#endif
    }

    private void OnPlayRequested(RtspCameraConfig camera)
    {
#if ENABLE_NATIVE_MEDIA
        try
        {
            PlayNative(camera);
        }
        catch (Exception ex)
        {
            // libvlc ausente/indisponível (comum no Linux sem o pacote vlc):
            // informa no status em vez de derrubar a aplicação.
            if (_vm is not null)
            {
                _vm.Status = "Não foi possível iniciar o vídeo nativo (libvlc ausente?). " +
                             "Use 'Abrir no player externo'. Detalhe: " + ex.Message;
            }
        }
#endif
    }

    private void OnStopRequested()
    {
#if ENABLE_NATIVE_MEDIA
        StopNative();
#endif
    }

#if ENABLE_NATIVE_MEDIA
    private LibVLCSharp.Shared.LibVLC? _libVlc;
    private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
    private LibVLCSharp.Avalonia.VideoView? _videoView;

    private void EnsureVideoView()
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc ??= new LibVLCSharp.Shared.LibVLC();
        _mediaPlayer ??= new LibVLCSharp.Shared.MediaPlayer(_libVlc);

        if (_videoView is null)
        {
            _videoView = new LibVLCSharp.Avalonia.VideoView { MediaPlayer = _mediaPlayer };
            var host = this.FindControl<Panel>("PreviewHost");
            var placeholder = this.FindControl<TextBlock>("PreviewPlaceholder");
            host?.Children.Insert(0, _videoView);
            if (placeholder is not null)
            {
                placeholder.IsVisible = false;
            }
        }
    }

    private void PlayNative(RtspCameraConfig camera)
    {
        EnsureVideoView();
        var url = camera.GetAuthenticatedUrl();
        if (string.IsNullOrWhiteSpace(url) || _libVlc is null || _mediaPlayer is null)
        {
            return;
        }

        using var media = new LibVLCSharp.Shared.Media(_libVlc, new Uri(url));
        _mediaPlayer.Play(media);
    }

    private void StopNative()
    {
        if (_mediaPlayer is { IsPlaying: true })
        {
            _mediaPlayer.Stop();
        }
    }

    // Libera os recursos nativos do LibVLC ao sair da tela, evitando vazamento
    // de handles do MediaPlayer/engine entre navegações.
    private void DisposeNative()
    {
        StopNative();

        if (_videoView is not null)
        {
            _videoView.MediaPlayer = null;
        }

        _mediaPlayer?.Dispose();
        _mediaPlayer = null;

        _libVlc?.Dispose();
        _libVlc = null;
    }
#endif
}
