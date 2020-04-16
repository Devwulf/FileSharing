using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace FileSharing.Simulator
{
    class UdpSimulator
    {
        IPAddress address = IPAddress.Parse("192.168.1.186");
        List<IPAddress> discoveredDevices = new List<IPAddress>();
        bool messageReceived = false;

        public void SendBroadcast()
        {
            Console.WriteLine("Sending...");
            var Client = new UdpClient();
            var RequestData = Encoding.ASCII.GetBytes("GIMMEHYOURADDRESS");

            Client.EnableBroadcast = true;
            Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 8888));

            Client.BeginReceive(new AsyncCallback((IAsyncResult res) =>
            {
                var client = (UdpClient)res.AsyncState;
                var ServerEp = new IPEndPoint(IPAddress.Any, 0);
                var ServerResponseData = client.EndReceive(res, ref ServerEp);
                var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);

                IPAddress ip;
                if (!IPAddress.TryParse(ServerResponse, out ip))
                {
                    Console.WriteLine($"Discovered {ServerEp.Address.ToString()} with message: {ServerResponse}\n");
                    discoveredDevices.Add(ip);
                }
                else
                {
                    Console.WriteLine($"Could not parse the ip address sent back: {ServerResponse}\n");
                }

                client.Close();
            }), Client);

            while (!messageReceived)
                Thread.Sleep(100);
        }

        public void ListenForBroadcast()
        {
            Console.WriteLine("Listening...");
            var Server = new UdpClient(8888);
            Server.BeginReceive(new AsyncCallback(Receive), Server);

            while (true)
                Thread.Sleep(100);
        }

        private void Receive(IAsyncResult res)
        {
            Console.WriteLine("Received something");
            var Server = (UdpClient)res.AsyncState;
            var ResponseData = Encoding.ASCII.GetBytes($"{address.ToString()}");
            var ClientEp = new IPEndPoint(IPAddress.Any, 0);
            var ClientRequestData = Server.EndReceive(res, ref ClientEp);

            Server.BeginReceive(new AsyncCallback(Receive), Server);

            var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);

            if (ClientRequest.Equals("GIMMEHYOURADDRESS"))
            {
                Console.WriteLine($"Received broadcast from {ClientEp.Address.ToString()}\n");
                Server.Send(ResponseData, ResponseData.Length, ClientEp);
            }
        }
    }
}
