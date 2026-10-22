using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

    private void OnUnloaded(object? sender, RoutedEventArgs e) => OnStopRequested();

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
#endif
}
