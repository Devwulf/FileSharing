using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
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

        private const int PORT = 1996;

        public MainPage()
        {
            InitializeComponent();
            InitializeIPAddress();
        }

        private void InitializeIPAddress()
        {
            var ipAddress = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault();
            IPAddressCurrent.Text = $"Local IP Address: {ipAddress?.ToString()}\n";
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
            if (client != null)
                return;

            SendProgress.Progress = 0;

            IPAddress address;
            if (!IPAddress.TryParse(IPAddressInput.Text, out address))
            {
                SendLog.Text += $"Cannot parse an ip address from the input: '{IPAddressInput.Text}'\n";
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
                client = null;

                SendLog.Text += "Sending file complete!\n";
            }
            else
            {
                SendLog.Text += $"File '{fileToSend.FileName}' cannot be opened.\n";
                return;
            }
        }

        private async void HandleReceiveFile(object sender, EventArgs e)
        {
            if (server != null)
                return;

            ReceiveProgress.Progress = 0;

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
                ReceiveLog.Text += "Permission to write not granted.";

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
            server = null;

            ReceiveLog.Text += "Receiving file complete!\n";
        }
    }
}
