using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
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
            _vm.PlaySlotRequested -= OnPlaySlotRequested;
            _vm.StopSlotRequested -= OnStopSlotRequested;
            _vm.StopAllRequested -= OnStopAllRequested;
            _vm.SlotsChanged -= OnSlotsChanged;
        }

        _vm = DataContext as CamerasViewModel;

        if (_vm is not null)
        {
            _vm.PlaySlotRequested += OnPlaySlotRequested;
            _vm.StopSlotRequested += OnStopSlotRequested;
            _vm.StopAllRequested += OnStopAllRequested;
            _vm.SlotsChanged += OnSlotsChanged;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
#if ENABLE_NATIVE_MEDIA
        StopAllNative();
#endif
    }

    private void OnSlotsChanged()
    {
#if ENABLE_NATIVE_MEDIA
        // O layout mudou: para tudo e deixa a View recriar as superfícies ao
        // reproduzir de novo. Evita VideoViews órfãos de slots removidos.
        StopAllNative();
#endif
    }

    private void OnPlaySlotRequested(CameraSlot slot, RtspCameraConfig camera)
    {
#if ENABLE_NATIVE_MEDIA
        try
        {
            PlayNative(slot, camera);
        }
        catch (Exception ex)
        {
            if (_vm is not null)
            {
                _vm.Status = "Não foi possível iniciar o vídeo nativo (libvlc ausente?). " +
                             "Use o player externo. Detalhe: " + ex.Message;
            }
        }
#endif
    }

    private void OnStopSlotRequested(CameraSlot slot)
    {
#if ENABLE_NATIVE_MEDIA
        StopNative(slot);
#endif
    }

    private void OnStopAllRequested()
    {
#if ENABLE_NATIVE_MEDIA
        StopAllNative();
#endif
    }

#if ENABLE_NATIVE_MEDIA
    private sealed class SlotPlayer
    {
        public LibVLCSharp.Shared.LibVLC LibVlc { get; init; } = null!;
        public LibVLCSharp.Shared.MediaPlayer Player { get; init; } = null!;
        public LibVLCSharp.Avalonia.VideoView View { get; init; } = null!;
        public Panel Host { get; init; } = null!;
    }

    private readonly Dictionary<CameraSlot, SlotPlayer> _players = new();

    private void PlayNative(CameraSlot slot, RtspCameraConfig camera)
    {
        var url = camera.GetAuthenticatedUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        // Localiza o Panel "SlotHost" do container visual deste slot.
        var host = FindSlotHost(slot);
        if (host is null)
        {
            return;
        }

        StopNative(slot);

        LibVLCSharp.Shared.Core.Initialize();
        var libVlc = new LibVLCSharp.Shared.LibVLC();
        var player = new LibVLCSharp.Shared.MediaPlayer(libVlc);
        var view = new LibVLCSharp.Avalonia.VideoView { MediaPlayer = player };

        host.Children.Insert(0, view);
        // Esconde o rótulo placeholder (demais filhos) enquanto reproduz.
        for (var i = 1; i < host.Children.Count; i++)
        {
            host.Children[i].IsVisible = false;
        }

        _players[slot] = new SlotPlayer
        {
            LibVlc = libVlc,
            Player = player,
            View = view,
            Host = host
        };

        using var media = new LibVLCSharp.Shared.Media(libVlc, new Uri(url));
        player.Play(media);
    }

    private Panel? FindSlotHost(CameraSlot slot)
    {
        var grid = this.FindControl<ItemsControl>("VideoGrid");
        if (grid is null)
        {
            return null;
        }

        var container = grid.ContainerFromItem(slot);
        return container?.GetVisualDescendants()
            .OfType<Panel>()
            .FirstOrDefault(p => p.Name == "SlotHost");
    }

    private void StopNative(CameraSlot slot)
    {
        if (!_players.TryGetValue(slot, out var sp))
        {
            return;
        }

        if (sp.Player.IsPlaying)
        {
            sp.Player.Stop();
        }

        sp.Host.Children.Remove(sp.View);
        for (var i = 0; i < sp.Host.Children.Count; i++)
        {
            sp.Host.Children[i].IsVisible = true;
        }

        sp.View.MediaPlayer = null;
        sp.Player.Dispose();
        sp.LibVlc.Dispose();
        _players.Remove(slot);
    }

    private void StopAllNative()
    {
        foreach (var slot in _players.Keys.ToList())
        {
            StopNative(slot);
        }
    }
#endif
}
