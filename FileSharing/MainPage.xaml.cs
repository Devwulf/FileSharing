using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using Sockets.Plugin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.PlatformConfiguration;

namespace FileSharing
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : TabbedPage
    {
        IPlatformPath platformPath = DependencyService.Get<IPlatformPath>();
        private FileData fileToSend;

        private TcpClient client;
        private TcpListener server;
        private bool isDiscoverable = false, isSending = false, isReceiving = false;

        private IPAddress address, subnetMask;
        private IPAddress broadcast, network;
        private const int PORT = 1996;

        private List<IPAddress> discoveredDevices = new List<IPAddress>();

        public MainPage()
        {
            InitializeComponent();

            InitializeIPAddress();
        }

        private void InitializeIPAddress()
        {
            /*
            var ipAddress = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault();
            IPAddressCurrent.Text = $"Local IP Address: {ipAddress?.ToString()}\n";
            */

            foreach (var i in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!i.Name.Equals("wlan0"))
                    continue;

                var interfaceProp = i.GetIPProperties();
                foreach (var uni in interfaceProp.UnicastAddresses)
                {
                    if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    address = uni.Address;
                    subnetMask = uni.IPv4Mask;
                    broadcast = address.GetBroadcastAddress(subnetMask);
                    IPAddressCurrent.Text += $"Name: {i.Name} IP: {address.ToString()} Subnet: {subnetMask.ToString()}\n";
                }
            }
        }

        private async Task SendBroadcast()
        {
            if (isSending)
                return; // don't want to keep sending broadcast when sending files

            SendLog.Text += "Now sending...\n";

            /*
            var Client = new UdpClient();
            var RequestData = Encoding.ASCII.GetBytes("GIMMEHYOURADDRESS");

            Client.EnableBroadcast = true;
            Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 8888));

            Client.BeginReceive(new AsyncCallback((IAsyncResult res) =>
            {
                SendLog.Text += "Received something...\n";
                var client = (UdpClient)res.AsyncState;
                var ServerEp = new IPEndPoint(IPAddress.Any, 0);
                var ServerResponseData = client.EndReceive(res, ref ServerEp);
                var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);

                IPAddress ip;
                if (!IPAddress.TryParse(ServerResponse, out ip))
                {
                    SendLog.Text += $"Discovered {ServerEp.Address.ToString()} with message: {ServerResponse}\n";
                    discoveredDevices.Add(ip);
                }
                else
                {
                    SendLog.Text += $"Could not parse the ip address sent back: {ServerResponse}\n";
                }

                client.Close();
            }), Client);
            /**/

            var port = 8888;
            var address = broadcast.ToString();

            var receiver = new UdpSocketReceiver();
            receiver.MessageReceived += (sender, args) =>
            {
                if (args.RemoteAddress.Equals(this.address.ToString()))
                    return;

                var ServerResponse = Encoding.ASCII.GetString(args.ByteData, 0, args.ByteData.Length);

                IPAddress ip;
                if (IPAddress.TryParse(ServerResponse, out ip))
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        SendLog.Text += $"Discovered {args.RemoteAddress} with message: {ServerResponse}\n";
                    });
                    discoveredDevices.Add(ip);
                }
                else
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        SendLog.Text += $"Could not parse the ip address sent back: {ServerResponse}\n";
                    });
                }

                Task.WaitAny(receiver.StopListeningAsync());
            };

            // convert our greeting message into a byte array
            var msg = "GIMMEHYOURADDRESS";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            // send to address:port, 
            // no guarantee that anyone is there 
            // or that the message is delivered.
            await receiver.SendToAsync(msgBytes, address, port);
            await receiver.StartListeningAsync(port);
        }

        private async Task ListenForBroadcast()
        {
            if (isReceiving)
                return;
            ReceiveLog.Text += "Now listening...\n";

            /*
            var Server = new UdpClient(8888);
            Server.BeginReceive(new AsyncCallback(Receive), Server);
            /**/

            var port = 8888;

            var receiver = new UdpSocketReceiver();
            receiver.MessageReceived += (sender, args) =>
            {
                var ClientRequest = Encoding.ASCII.GetString(args.ByteData, 0, args.ByteData.Length);

                if (!isDiscoverable || isReceiving)
                    Task.WaitAny(receiver.StopListeningAsync());

                if (ClientRequest.Equals("GIMMEHYOURADDRESS"))
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        ReceiveLog.Text += $"Received broadcast from {args.RemoteAddress}\n";
                    });
                    var ResponseData = Encoding.ASCII.GetBytes(address.ToString());
                    Task.WaitAny(receiver.SendToAsync(ResponseData, args.RemoteAddress, port));
                }
            };

            await receiver.StartListeningAsync(port);
        }

        private void Receive(IAsyncResult res)
        {
            ReceiveLog.Text += "Received something...\n";
            var Server = (UdpClient)res.AsyncState;
            var ResponseData = Encoding.ASCII.GetBytes($"{address.ToString()}");
            var ClientEp = new IPEndPoint(IPAddress.Any, 0);
            var ClientRequestData = Server.EndReceive(res, ref ClientEp);

            if (isDiscoverable && !isReceiving)
                Server.BeginReceive(new AsyncCallback(Receive), Server);

            var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);

            if (ClientRequest.Equals("GIMMEHYOURADDRESS"))
            {
                ReceiveLog.Text += $"Received broadcast from {ClientEp.Address.ToString()}\n";
                Server.Send(ResponseData, ResponseData.Length, ClientEp);
            }
        }

        private async void HandleRefreshDevices(object sender, EventArgs e) => await SendBroadcast();

        private async void HandleDiscoverableToggle(object sender, ToggledEventArgs e)
        {
            if (e.Value == true)
            {
                isDiscoverable = true;
                await ListenForBroadcast();
            }
            else
                isDiscoverable = false;
        }

        private async void HandlePickFile(object sender, EventArgs e)
        {
            try
            {
                fileToSend = await CrossFilePicker.Current.PickFile();
                if (fileToSend == null)
                    return; // cancelled picking file

                ((Button)sender).Text = fileToSend.FileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error picking a file: {ex.Message}");
            }
        }

        private async void HandleSendFile(object sender, EventArgs e)
        {
            if (isSending)
                return;

            isSending = true;
            SendProgress.Progress = 0;
            SendButton.Text = "Sending a file...";
            SendButton.IsEnabled = false;

            IPAddress address;
            if (!IPAddress.TryParse(IPAddressInput.Text, out address))
            {
                SendLog.Text += $"Cannot parse an ip address from the input: '{IPAddressInput.Text}'\n";
                ResetSend();
                return;
            }

            Stream fileStream = fileToSend.GetStream();
            if (fileStream != null)
            {
                SendLog.Text += $"Trying to connect to {IPAddressInput.Text}...\n";
                client = new TcpClient();
                try
                {
                    await client.ConnectAsync(address, PORT);
                }
                catch
                {
                    SendLog.Text += $"Error connecting to {IPAddressInput.Text}.\n";
                    fileStream.Dispose();
                    ResetSend();
                    return;
                }

                NetworkStream netStream = client.GetStream();

                SendLog.Text += "Getting permission...\n";
                var permission = new byte[1];
                await netStream.ReadAsync(permission, 0, 1);
                if (permission[0] != 1)
                {
                    SendLog.Text += "Permission denied.\n";
                    fileStream.Dispose();
                    client.Close();
                    ResetSend();
                    return;
                }

                SendLog.Text += "Sending file info...\n";
                var fileSize = fileToSend.DataArray.LongLength;
                var fileName = Encoding.UTF8.GetBytes(fileToSend.FileName);
                var fileNameLength = BitConverter.GetBytes(fileName.Length);
                var fileLength = BitConverter.GetBytes(fileSize);
                await netStream.WriteAsync(fileLength, 0, fileLength.Length);
                await netStream.WriteAsync(fileNameLength, 0, fileNameLength.Length);
                await netStream.WriteAsync(fileName, 0, fileName.Length);

                SendLog.Text += "Sending file...\n";
                int readLength;
                int totalSent = 0;
                byte[] buffer = new byte[32 * 1024]; // 32k chunks
                while ((readLength = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await netStream.WriteAsync(buffer, 0, readLength);
                    totalSent += readLength;
                    SendProgress.Progress = (double)totalSent / fileSize;
                }

                fileStream.Dispose();
                client.Close();

                ResetSend();

                SendLog.Text += "Sending file complete!\n";
            }
            else
            {
                SendLog.Text += $"File '{fileToSend.FileName}' cannot be opened.\n";
                ResetSend();
                return;
            }
        }

        private void ResetSend()
        {
            isSending = false;
            SendButton.Text = "Send File";
            SendButton.IsEnabled = true;
        }

        private async void HandleReceiveFile(object sender, EventArgs e)
        {
            if (isReceiving)
                return;

            isReceiving = true;
            ReceiveProgress.Progress = 0;
            ReceiveButton.Text = "Receiving a file...";
            ReceiveButton.IsEnabled = false;

            server = TcpListener.Create(PORT);
            server.Start();

            ReceiveLog.Text += "Waiting for connection...\n";
            TcpClient other = await server.AcceptTcpClientAsync();
            NetworkStream netStream = other.GetStream();

            ReceiveLog.Text += "Sending permission...\n";
            netStream.WriteByte(1);

            ReceiveLog.Text += "Receiving file info...\n";
            var fileLengthBytes = new byte[8];
            var fileNameLengthBytes = new byte[4];
            await netStream.ReadAsync(fileLengthBytes, 0, 8);
            await netStream.ReadAsync(fileNameLengthBytes, 0, 4);

            var fileNameBytes = new byte[BitConverter.ToInt32(fileNameLengthBytes, 0)];
            await netStream.ReadAsync(fileNameBytes, 0, fileNameBytes.Length);

            var fileLength = BitConverter.ToInt64(fileLengthBytes, 0);
            var fileName = Encoding.UTF8.GetString(fileNameBytes);

            ReceiveLog.Text += "Receiving file...\n";
            var path = Path.Combine(platformPath.DownloadPath(), fileName);
            ReceiveLog.Text += $"Path to file is '{path}'";

            var perm = await Permissions.RequestAsync<Permissions.StorageWrite>();
            if (perm != PermissionStatus.Granted)
            {
                ReceiveLog.Text += "Permission to write not granted.";
                ResetReceive();
                return;
            }

            var fileStream = File.Create(path);

            int readLength;
            int totalReceived = 0;
            byte[] buffer = new byte[32 * 1024];
            while ((readLength = await netStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, readLength);
                totalReceived += readLength;
                ReceiveProgress.Progress = (double)totalReceived / fileLength;
            }

            fileStream.Dispose();
            other.Close();

            ResetReceive();
            ReceiveLog.Text += "Receiving file complete!\n";
        }

        private void ResetReceive()
        {
            isReceiving = false;
            ReceiveButton.Text = "Receive File";
            ReceiveButton.IsEnabled = true;
        }
    }
}
