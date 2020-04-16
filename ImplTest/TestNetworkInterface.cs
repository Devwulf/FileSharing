using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace ImplTest
{
    public class TestNetworkInterface : ITest
    {
        public void RunTest()
        {
            ShowNetworkInterfaces();
        }

        private static void ShowNetworkInterfaces()
        {
            IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            Console.WriteLine("Interface information for {0}.{1}     ",
                    computerProperties.HostName, computerProperties.DomainName);
            if (nics == null || nics.Length < 1)
            {
                Console.WriteLine("  No network interfaces found.");
                return;
            }

            Console.WriteLine("  Number of interfaces .................... : {0}", nics.Length);
            foreach (NetworkInterface adapter in nics)
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                Console.WriteLine();
                Console.WriteLine($"{adapter.Description} ({adapter.Name})");
                Console.WriteLine(String.Empty.PadLeft(adapter.Description.Length, '='));
                Console.WriteLine("  Interface type .......................... : {0}", adapter.NetworkInterfaceType);
                Console.WriteLine("  Physical Address ........................ : {0}",
                           adapter.GetPhysicalAddress().ToString());
                Console.WriteLine("  Operational status ...................... : {0}",
                    adapter.OperationalStatus);
                string versions = "";

                // Create a display string for the supported IP versions.
                if (adapter.Supports(NetworkInterfaceComponent.IPv4))
                {
                    versions = "IPv4";
                }
                if (adapter.Supports(NetworkInterfaceComponent.IPv6))
                {
                    if (versions.Length > 0)
                    {
                        versions += " ";
                    }
                    versions += "IPv6";
                }
                Console.WriteLine("  IP version .............................. : {0}", versions);

                int counter = 0;
                Console.WriteLine("  DHCP Server Addresses:");
                foreach (var ipAddress in properties.DhcpServerAddresses)
                {
                    if (counter++ == 0)
                        Console.WriteLine($"    IP Address ............................ : {ipAddress.ToString()}");
                    else
                        Console.WriteLine($"               ............................ : {ipAddress.ToString()}");
                }

                Console.WriteLine("  DNS Addresses:");
                counter = 0;
                foreach (var ipAddress in properties.DnsAddresses)
                {
                    if (counter++ == 0)
                        Console.WriteLine($"    IP Address ............................ : {ipAddress.ToString()}");
                    else
                        Console.WriteLine($"               ............................ : {ipAddress.ToString()}");
                }

                Console.WriteLine("  Gateway Addresses:");
                counter = 0;
                foreach (var ipAddress in properties.GatewayAddresses)
                {
                    if (counter++ == 0)
                        Console.WriteLine($"    IP Address ............................ : {ipAddress.Address.ToString()}");
                    else
                        Console.WriteLine($"               ............................ : {ipAddress.Address.ToString()}");
                }

                Console.WriteLine("  Unicast Addresses:");
                foreach (var ipAddress in properties.UnicastAddresses)
                {
                    Console.WriteLine($"    Family ................................ : {ipAddress.Address.AddressFamily.ToString()}");
                    Console.WriteLine($"    IP Address ............................ : {ipAddress.Address.ToString()}");
                    Console.WriteLine($"    Subnet Mask ........................... : {ipAddress.IPv4Mask.ToString()}");
                }

                // The following information is not useful for loopback adapters.
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                Console.WriteLine("  DNS suffix .............................. : {0}",
                    properties.DnsSuffix);

                if (adapter.Supports(NetworkInterfaceComponent.IPv4))
                {
                    IPv4InterfaceProperties ipv4 = properties.GetIPv4Properties();
                    Console.WriteLine("  MTU...................................... : {0}", ipv4.Mtu);
                    if (ipv4.UsesWins)
                    {

                        IPAddressCollection winsServers = properties.WinsServersAddresses;
                        foreach (var ipAddress in winsServers)
                        {
                            Console.WriteLine("  WINS Servers ............................ : {0}", ipAddress.ToString());
                        }
                    }
                }

                Console.WriteLine("  DNS enabled ............................. : {0}",
                    properties.IsDnsEnabled);
                Console.WriteLine("  Dynamically configured DNS .............. : {0}",
                    properties.IsDynamicDnsEnabled);
                Console.WriteLine("  Receive Only ............................ : {0}",
                    adapter.IsReceiveOnly);
                Console.WriteLine("  Multicast ............................... : {0}",
                    adapter.SupportsMulticast);

                Console.WriteLine();
            }
        }
    }
}
