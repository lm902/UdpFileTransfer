using System.CommandLine;
using System.Net;
using UdpFileTransfer.Common;

namespace UdpFileTransfer.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Client for sending data over UDP");
            var hostOption = new Option<string>("--host", () => "localhost", "The hostname of the server to send data to") { IsRequired = true };
            var portOption = new Option<int>("--port", "The port that the server is listening on") { IsRequired = true };
            var fileOption = new Option<string>("--file", "The file to read and send");
            rootCommand.AddOption(hostOption);
            rootCommand.AddOption(portOption);
            rootCommand.AddOption(fileOption);

            rootCommand.SetHandler((host, port, file) =>
            {
                IPAddress? ipAddress;
                if (!IPAddress.TryParse(host, out ipAddress))
                {
                    var addresses = Dns.GetHostAddresses(host);
                    if (!addresses.Any())
                    {
                        throw new ArgumentOutOfRangeException(nameof(host));
                    }
                    ipAddress = addresses.First();
                }
                var endpoint = new IPEndPoint(ipAddress, port);
                var sendStream = new SendStream(endpoint);
                if (string.IsNullOrWhiteSpace(file))
                {
                    var stdin = Console.OpenStandardInput();
                    stdin.CopyTo(sendStream);
                }
                else
                {
                    var fileStream = File.OpenRead(file);
                    fileStream.CopyTo(sendStream);
                    fileStream.Close();
                }
                sendStream.WriteByte(0);
                Thread.Sleep(100);
                while (!sendStream.SendComplete())
                {
                    Console.WriteLine("Waiting for final data ACK");
                    sendStream.Flush();
                    Thread.Sleep(1000);
                }
            }, hostOption, portOption, fileOption);
            rootCommand.Invoke(args);
        }
    }
}
