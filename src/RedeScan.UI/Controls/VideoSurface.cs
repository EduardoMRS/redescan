using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using RedeScan.Core.Services.Media;

namespace RedeScan.UI.Controls;

/// <summary>
/// Superfície de vídeo ao vivo. Basta definir <see cref="Source"/> com a URL
/// (RTSP/HTTP): o controle inicia o decodificador quando está na tela e o
/// encerra ao sair da tela ou quando <see cref="Source"/> volta a ser nulo.
/// Renderiza num <see cref="WriteableBitmap"/>, então funciona dentro de
/// qualquer layout (grades, listas, scroll) sem janela nativa embutida.
/// </summary>
public sealed class VideoSurface : Border
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<VideoSurface, string?>(nameof(Source));

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<VideoSurface, string>(nameof(StatusText), string.Empty);

    /// <summary>Fábrica do serviço de vídeo (definida no startup via DI).</summary>
    public static IVideoService? VideoService { get; set; }

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _overlay = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(10),
        Opacity = 0.8
    };

    private IVideoStreamReader? _reader;
    private WriteableBitmap? _bitmap;
    private byte[]? _pending;
    private int _pendingWidth;
    private int _pendingHeight;
    private int _renderQueued;
    private readonly object _frameGate = new();

    public VideoSurface()
    {
        ClipToBounds = true;
        Background = Brushes.Black;
        Child = new Panel { Children = { _image, _overlay } };
    }

    /// <summary>URL do stream. Nulo/vazio para parar.</summary>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Mensagem de estado atual (conectando, erro…), somente leitura para a UI.</summary>
    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        private set => SetValue(StatusTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Restart();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Restart();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopReader();
    }

    private void Restart()
    {
        StopReader();
        _image.Source = null;
        _bitmap = null;

        var url = Source;
        if (string.IsNullOrWhiteSpace(url) || VisualRoot is null)
        {
            SetOverlay(string.Empty);
            return;
        }

        if (VideoService is not { IsAvailable: true } service)
        {
            SetOverlay("ffmpeg não encontrado.\nUse \"Instalar ffmpeg\" no topo da tela de câmeras.");
            return;
        }

        _reader = service.CreateReader();
        _reader.FrameReady += OnFrame;
        _reader.StateChanged += OnState;
        _reader.Start(url);
    }

    private void StopReader()
    {
        var reader = _reader;
        _reader = null;
        if (reader is null)
        {
            return;
        }

        reader.FrameReady -= OnFrame;
        reader.StateChanged -= OnState;
        // Encerrar o processo pode levar alguns ms; não bloqueia a UI.
        Task.Run(reader.Dispose);
    }

    private void OnState(VideoStreamState state, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = message;
            SetOverlay(state == VideoStreamState.Playing ? string.Empty : message);
        });
    }

    /// <summary>
    /// Chamado na thread do decodificador. Copia o quadro e agenda UM render
    /// por vez na UI: se a UI atrasar, quadros intermediários são descartados
    /// (sempre mostra o mais recente, sem acumular fila/memória).
    /// </summary>
    private void OnFrame(VideoFrame frame)
    {
        lock (_frameGate)
        {
            if (_pending is null || _pending.Length != frame.Data.Length)
            {
                _pending = new byte[frame.Data.Length];
            }

            Buffer.BlockCopy(frame.Data, 0, _pending, 0, frame.Data.Length);
            _pendingWidth = frame.Width;
            _pendingHeight = frame.Height;
        }

        if (Interlocked.Exchange(ref _renderQueued, 1) == 0)
        {
            Dispatcher.UIThread.Post(RenderPending, DispatcherPriority.Render);
        }
    }

    private void RenderPending()
    {
        Interlocked.Exchange(ref _renderQueued, 0);
        if (_reader is null)
        {
            return;
        }

        lock (_frameGate)
        {
            if (_pending is null)
            {
                return;
            }

            if (_bitmap is null || _bitmap.PixelSize.Width != _pendingWidth || _bitmap.PixelSize.Height != _pendingHeight)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize(_pendingWidth, _pendingHeight),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);
            }

            using (var fb = _bitmap.Lock())
            {
                var rowBytes = _pendingWidth * 4;
                if (fb.RowBytes == rowBytes)
                {
                    Marshal.Copy(_pending, 0, fb.Address, rowBytes * _pendingHeight);
                }
                else
                {
                    for (var y = 0; y < _pendingHeight; y++)
                    {
                        Marshal.Copy(_pending, y * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
                    }
                }
            }
        }

        // Reatribuir força o Image a redesenhar o bitmap atualizado.
        _image.Source = null;
        _image.Source = _bitmap;
    }

    private void SetOverlay(string text)
    {
        _overlay.Text = text;
        _overlay.IsVisible = !string.IsNullOrEmpty(text);
    }
}
