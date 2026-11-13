using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DnDMap.Server;

/// <summary>Адрес хоста для пульта мастера: порт и IPv4-адреса в локальной сети.</summary>
public sealed record HostInfoDto(int Port, IReadOnlyList<string> Addresses);

/// <summary>
/// Ищет IPv4-адреса, по которым к серверу можно подключиться из локальной сети.
/// Wi-Fi и Ethernet важнее мобильного интернета и виртуальных адаптеров.
/// </summary>
public static class HostAddresses
{
    public static IReadOnlyList<string> GetLanIPv4()
    {
        try
        {
            return Collect();
        }
        catch (Exception error) when (error is NetworkInformationException or PlatformNotSupportedException)
        {
            // Сеть недоступна/не поддерживается — страница ожидания покажет подсказку.
            return [];
        }
    }

    private static List<string> Collect()
    {
        var found = new List<(int Rank, string Address)>();

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var rank = Rank(adapter);

            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;

                if (address.AddressFamily != AddressFamily.InterNetwork ||
                    IPAddress.IsLoopback(address) || IsLinkLocal(address))
                    continue;

                found.Add((rank, address.ToString()));
            }
        }

        return found
            .DistinctBy(item => item.Address)
            .OrderBy(item => item.Rank)
            .Select(item => item.Address)
            .ToList();
    }

    private static int Rank(NetworkInterface adapter) => adapter.NetworkInterfaceType switch
    {
        NetworkInterfaceType.Wireless80211 => 0,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => 1,
        // На Android имена адаптеров надёжнее типа: wlan0 — Wi-Fi, rmnet* — мобильный интернет.
        _ when adapter.Name.StartsWith("wlan", StringComparison.OrdinalIgnoreCase) => 0,
        _ when adapter.Name.StartsWith("eth", StringComparison.OrdinalIgnoreCase) => 1,
        _ => 2
    };

    private static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }
}
