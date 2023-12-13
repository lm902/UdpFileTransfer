using System.CommandLine;
using System.Configuration;
using System.Net;
using System.Net.Sockets;

namespace UdpFileTransfer.Proxy
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("A very simple UDP proxy program that can reduce reliability");
            var listenHostOption = new Option<string>("--listen-host", () => "localhost", "The hostname to listen for incoming UDP packets") { IsRequired = true };
            var listenPortOption = new Option<int>("--listen-port", "The port that the proxy is listening on") { IsRequired = true };
            var connectHostOption = new Option<string>("--connect-host", () => "localhost", "The hostname to forward the received UDP packets") { IsRequired = true };
            var connectPortOption = new Option<int>("--connect-port", "The port to forward the received UDP packets") { IsRequired = true };
            var clientToServerDelayOption = new Option<double>("--client-delay", () => 0, "Ratio of client to server packets to insert random delays");
            var serverToClientDelayOption = new Option<double>("--server-delay", () => 0, "Ratio of server to client packets to insert random delays");
            var clientToServerDropOption = new Option<double>("--client-drop", () => 0, "Ratio of client to server packets to drop");
            var serverToClientDropOption = new Option<double>("--server-drop", () => 0, "Ratio of server to client packets to drop");
            rootCommand.AddOption(listenHostOption);
            rootCommand.AddOption(listenPortOption);
            rootCommand.AddOption(connectHostOption);
            rootCommand.AddOption(connectPortOption);
            rootCommand.AddOption(clientToServerDelayOption);
            rootCommand.AddOption(serverToClientDelayOption);
            rootCommand.AddOption(clientToServerDropOption);
            rootCommand.AddOption(serverToClientDropOption);

            rootCommand.SetHandler((listenHost, listenPort, connectHost, connectPort, clientToServerDelay, serverToClientDelay, clientToServerDrop, serverToClientDrop) =>
            {
                var random = new Random();

                IPAddress? listenIpAddress;
                if (!IPAddress.TryParse(listenHost, out listenIpAddress))
                {
                    var addresses = Dns.GetHostAddresses(listenHost);
                    if (!addresses.Any())
                    {
                        throw new ArgumentOutOfRangeException(nameof(listenHost));
                    }
                    listenIpAddress = addresses.First();
                }
                var listenEndpoint = new IPEndPoint(listenIpAddress, listenPort);

                IPAddress? connectIpAddress;
                if (!IPAddress.TryParse(connectHost, out connectIpAddress))
                {
                    var addresses = Dns.GetHostAddresses(connectHost);
                    if (!addresses.Any())
                    {
                        throw new ArgumentOutOfRangeException(nameof(connectHost));
                    }
                    connectIpAddress = addresses.First();
                }
                var connectEndpoint = new IPEndPoint(connectIpAddress, connectPort);

                var listenClient = new UdpClient(listenEndpoint);
                IPEndPoint? clientEP = null;
                var forwardClient = new UdpClient(connectEndpoint.AddressFamily);
                forwardClient.Connect(connectEndpoint);
                Console.WriteLine($"Source\tDatagram Size\tReceive Time\tDrop\tProcess Time");
                var c2sTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        var data = listenClient.Receive(ref clientEP);
                        var receiveTime = DateTime.Now;
                        var chance = random.NextDouble();
                        if (chance < clientToServerDrop)
                        {
                            Console.WriteLine($"{clientEP}\t{data.Length}\t{receiveTime.ToBinary()}\t{true}\t{DateTime.Now.ToBinary()}");
                            continue;
                        }
                        if (chance < clientToServerDelay)
                        {
                            await Task.Delay((int)(chance * int.Parse(ConfigurationManager.AppSettings["clientToServerMaxDelayTimeMs"] ?? "1000")));
                        }
                        forwardClient.Send(data);
                        Console.WriteLine($"{clientEP}\t{data.Length}\t{receiveTime.ToBinary()}\t{false}\t{DateTime.Now.ToBinary()}");
                    }
                });

                var s2cTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        IPEndPoint? serverEP = null;
                        var data = forwardClient.Receive(ref serverEP);
                        var receiveTime = DateTime.Now;
                        var chance = random.NextDouble();
                        if (chance < serverToClientDrop)
                        {
                            Console.WriteLine($"{serverEP}\t{data.Length}\t{receiveTime.ToBinary()}\t{true}\t{DateTime.Now.ToBinary()}");
                            continue;
                        }
                        if (chance < serverToClientDelay)
                        {
                            await Task.Delay((int)(chance * int.Parse(ConfigurationManager.AppSettings["serverToClientMaxDelayTimeMs"] ?? "1000")));
                        }
                        listenClient.Send(data, clientEP);
                        Console.WriteLine($"{serverEP}\t{data.Length}\t{receiveTime.ToBinary()}\t{false}\t{DateTime.Now.ToBinary()}");
                    }
                });

                Task.WaitAll([c2sTask, s2cTask]);
            }, listenHostOption, listenPortOption, connectHostOption, connectPortOption, clientToServerDelayOption, serverToClientDelayOption, clientToServerDropOption, serverToClientDropOption);
            rootCommand.Invoke(args);
        }
    }
}
