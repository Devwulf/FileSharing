﻿using FileSharing.Utils;
using Newtonsoft.Json;
using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using Sockets.Plugin;
using Sockets.Plugin.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        public class DeviceDetails
        {
            public string Name { get; set; }
            public IPAddress Address { get; set; }
        }

        public enum ValueType
        {
            RequestIP,
            IPResponse
        }

        public class SharingResult
        {
            public string Name { get; set; }
            public ValueType Type { get; set; }
            public string Value { get; set; }
            public bool IsDiscoverable { get; set; } = false;
        }

        IPlatformPath platformPath = DependencyService.Get<IPlatformPath>();
        private FileData fileToSend;

        private TcpClient client;
        private TcpListener server;
        private bool isSending = false, isReceiving = false;

        private UdpSocketReceiver broadcaster = new UdpSocketReceiver();
        private UdpSocketReceiver listener = new UdpSocketReceiver();
        private bool isDiscoverable = false, isBroadcasting = false, isListening = false; 
        private CancellationTokenSource sendCancelSource;

        private IPAddress address, subnetMask;
        private IPAddress broadcast;
        private const int PORT = 1996;
        private const int BROADCAST_PORT = 8888;

        private ObservableCollection<DeviceDetails> discoveredDevices = new ObservableCollection<DeviceDetails>();

        public MainPage()
        {
            InitializeComponent();
            Initialize();

            DevicesList.ItemsSource = discoveredDevices;
            broadcaster.MessageReceived += BroadcastReplied;
            listener.MessageReceived += BroadcastReceived;
        }

        public void Initialize()
        {
            InitializeIPAddress();
        }

        private void InitializeIPAddress()
        {
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
                    IPAddressCurrent.Text = $"Name: {DeviceInfo.Name}\nIP: {address.ToString()}";
                }
            }
        }

        private async Task SendBroadcast()
        {
            if (isSending || isBroadcasting)
                return; // don't want to keep sending broadcast when sending files

            isBroadcasting = true;
            RefreshDevices.IsEnabled = false;

            sendCancelSource = new CancellationTokenSource();
            discoveredDevices.Clear();

            RefreshDevices.Text = "Stop Refresh";
            SendLog.Text += "Now sending...\n";

            // convert our greeting message into a byte array
            var Response = JsonConvert.SerializeObject(new SharingResult() { Name = DeviceInfo.Name, Type = ValueType.RequestIP, Value = "GIMMEHYOURADDRESS", IsDiscoverable = isDiscoverable });
            var msgBytes = Encoding.UTF8.GetBytes(Response);

            // send to address:port, 
            // no guarantee that anyone is there 
            // or that the message is delivered.
            await broadcaster.StartListeningAsync(BROADCAST_PORT);

            RefreshDevices.IsEnabled = true;
            await PeriodicTask.RunAsync(broadcaster.SendToAsync(msgBytes, broadcast.ToString(), BROADCAST_PORT), TimeSpan.FromSeconds(2), sendCancelSource.Token);
        }

        private async Task CancelBroadcast()
        {
            if (!isBroadcasting)
                return;

            RefreshDevices.IsEnabled = false;
            RefreshDevices.Text = "Refresh Devices";
            SendLog.Text += "Stopping broadcast...\n";

            sendCancelSource.Cancel();
            await broadcaster.StopListeningAsync();

            isBroadcasting = false;
            RefreshDevices.IsEnabled = true;
        }

        private void BroadcastReplied(object sender, UdpSocketMessageReceivedEventArgs args)
        {
            if (args.RemoteAddress.Equals(address.ToString()))
                return;

            var ServerResponse = Encoding.UTF8.GetString(args.ByteData, 0, args.ByteData.Length);
            SharingResult Result;
            try
            {
                Result = JsonConvert.DeserializeObject<SharingResult>(ServerResponse);
            }
            catch (Exception ex)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    SendLog.Text += "Error when deserializing the response.\n";
                });
                return;
            }

            if (Result.Type != ValueType.IPResponse)
                return;

            IPAddress ip;
            if (IPAddress.TryParse(Result.Value, out ip))
            {
                if (!discoveredDevices.Any(item => item.Address.ToString().Equals(Result.Value)))
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        SendLog.Text += $"Discovered device \"{Result.Name}\" ({ip.ToString()})\n";
                    });
                    discoveredDevices.Add(new DeviceDetails() { Name = Result.Name, Address = ip });
                }
            }
            else
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    SendLog.Text += $"Could not parse the ip address sent back: {Result.Value}\n";
                });
            }
        }

        private async Task ListenForBroadcast()
        {
            if (isReceiving)
                return;

            ReceiveLog.Text += "Now listening...\n";
            Discoverable.IsEnabled = false;
            await listener.StartListeningAsync(BROADCAST_PORT);
            Discoverable.IsEnabled = true;
        }

        private void BroadcastReceived(object sender, UdpSocketMessageReceivedEventArgs args)
        {
            if (isReceiving)
            {
                Task.WaitAny(listener.StopListeningAsync());
                return;
            }

            if (args.RemoteAddress.Equals(address.ToString()))
                return;

            var ClientRequest = Encoding.UTF8.GetString(args.ByteData, 0, args.ByteData.Length);
            SharingResult Result;
            try
            {
                Result = JsonConvert.DeserializeObject<SharingResult>(ClientRequest);
            }
            catch (Exception ex)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ReceiveLog.Text += "Error when deserializing the response.\n";
                });
                return;
            }

            if (Result.Type == ValueType.RequestIP && Result.Value.Equals("GIMMEHYOURADDRESS"))
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ReceiveLog.Text += $"Received broadcast from {args.RemoteAddress}\n";
                });

                var Response = JsonConvert.SerializeObject(new SharingResult() { Name = DeviceInfo.Name, Type = ValueType.IPResponse, Value = address.ToString(), IsDiscoverable = isDiscoverable });
                var ResponseData = Encoding.UTF8.GetBytes(Response);
                if (Result.IsDiscoverable)
                {
                    IPAddress ip;
                    if (IPAddress.TryParse(args.RemoteAddress, out ip))
                    {
                        if (!discoveredDevices.Any(item => item.Address.ToString().Equals(ip.ToString())))
                        {
                            Device.BeginInvokeOnMainThread(() =>
                            {
                                ReceiveLog.Text += $"Discovered device \"{Result.Name}\" ({ip.ToString()})\n";
                            });
                            discoveredDevices.Add(new DeviceDetails() { Name = Result.Name, Address = ip });
                        }
                    }
                }
                Task.WaitAny(listener.SendToAsync(ResponseData, args.RemoteAddress, BROADCAST_PORT));
            }
        }

        private async void HandleRefreshDevices(object sender, EventArgs e)
        {
            if (isBroadcasting)
                await CancelBroadcast();
            else
                await SendBroadcast();
        }

        private void HandleSelectDevice(object sender, SelectedItemChangedEventArgs e)
        {
            IPAddressInput.Text = ((DeviceDetails)e.SelectedItem).Address.ToString();
        }

        private async void HandleDiscoverableToggle(object sender, ToggledEventArgs e)
        {
            if (e.Value == true)
            {
                isDiscoverable = true;
                await ListenForBroadcast();
            }
            else
            {
                Discoverable.IsEnabled = false;
                await listener.StopListeningAsync();
                isDiscoverable = false;
                Discoverable.IsEnabled = true;
            }
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
            ReceiveLog.Text += $"Path to file is '{path}'\n";

            var perm = await Permissions.RequestAsync<Permissions.StorageWrite>();
            if (perm != PermissionStatus.Granted)
            {
                ReceiveLog.Text += "Permission to write not granted.\n";
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
