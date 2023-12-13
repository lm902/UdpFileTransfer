using System.CommandLine;
using System.Net;
using UdpFileTransfer.Common;

namespace UdpFileTransfer.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Server for receiving data over UDP");
            var hostOption = new Option<string>("--host", () => "localhost", "The hostname for the server to listen") { IsRequired = true };
            var portOption = new Option<int>("--port", "The port that the server should be listening on") { IsRequired = true };
            rootCommand.AddOption(hostOption);
            rootCommand.AddOption(portOption);

            rootCommand.SetHandler((host, port) =>
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
                var receiveStream = new ReceiveStream(endpoint);
                var stdout = Console.OpenStandardOutput();
                receiveStream.CopyTo(stdout);
                Thread.Sleep(100);
                Environment.Exit(0);
            }, hostOption, portOption);
            rootCommand.Invoke(args);
        }
    }
}
