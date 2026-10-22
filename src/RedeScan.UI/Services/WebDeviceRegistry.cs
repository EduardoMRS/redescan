using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using RedeScan.Core.Models;

namespace RedeScan.UI.Services;

/// <summary>
/// Implementação em memória de <see cref="IWebDeviceRegistry"/>. Mantém a ordem
/// com roteadores primeiro e garante mutação na thread de UI (a descoberta roda
/// em background).
/// </summary>
public sealed class WebDeviceRegistry : IWebDeviceRegistry
{
    private readonly ObservableCollection<WebDevice> _devices = new();

    public WebDeviceRegistry()
    {
        Devices = new ReadOnlyObservableCollection<WebDevice>(_devices);
    }

    public ReadOnlyObservableCollection<WebDevice> Devices { get; }

    public void AddOrUpdate(WebDevice device)
    {
        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            return;
        }

        Post(() =>
        {
            var existing = _devices.FirstOrDefault(d => d.IpAddress == device.IpAddress);
            if (existing is not null)
            {
                var idx = _devices.IndexOf(existing);
                _devices[idx] = device;
            }
            else if (device.IsLikelyRouter)
            {
                _devices.Insert(0, device);
            }
            else
            {
                _devices.Add(device);
            }
        });
    }

    public void Replace(IEnumerable<WebDevice> devices)
    {
        var ordered = devices
            .OrderByDescending(d => d.IsLikelyRouter)
            .ThenBy(d => d.IpAddress)
            .ToList();

        Post(() =>
        {
            _devices.Clear();
            foreach (var d in ordered)
            {
                _devices.Add(d);
            }
        });
    }

    private static void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
