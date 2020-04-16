using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace FileSharing.Simulator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please give only 1 arg: send or listen");
                return;
            }

            string arg1 = args[0];
            if (string.IsNullOrWhiteSpace(arg1))
            {
                Console.WriteLine("Please give only 1 arg: send or listen");
                return;
            }

            var simulator = new UdpSimulator();
            if (arg1.Equals("send"))
            {
                simulator.SendBroadcast();
            }
            else if (arg1.Equals("listen"))
            {
                simulator.ListenForBroadcast();
            }
        }
    }
}
