using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.IO.Hashing;
using System.Net;
using System.Net.Sockets;

namespace UdpFileTransfer.Common
{
    public class SendStream : Stream
    {
        public SendStream(IPEndPoint remoteEP)
        {
            client = new UdpClient(remoteEP.AddressFamily);
            client.Connect(remoteEP);
            Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested || unconfirmedDgrams.Any())
                {
                    if (!unconfirmedDgrams.Any())
                    {
                        await Task.Delay(100);
                    }
                    var buffer = (await client.ReceiveAsync(cancellationTokenSource.Token)).Buffer;
                    var ack = BsonSerializer.Deserialize<AckDgram>(buffer);
                    var dgrams = from dgram in unconfirmedDgrams where dgram.Key.Sequence <= ack.DataCompleteToSequence || ack.ExtraDgramSequences.Contains(dgram.Key.Sequence) select dgram.Key;
                    foreach (var dgram in dgrams.ToArray())
                    {
                        unconfirmedDgrams.Remove(dgram);
                    }
                }
            });
            Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested || unconfirmedDgrams.Any())
                {
                    if (!unconfirmedDgrams.Any())
                    {
                        await Task.Delay(100);
                    }
                    Flush(TimeSpan.FromSeconds(0.5));
                }
            });
        }

        private Dictionary<DataDgram, DateTime> unconfirmedDgrams = new Dictionary<DataDgram, DateTime>();

        private long sequence = 0;

        private UdpClient client;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => position; set => throw new NotImplementedException(); }

        private long position = 0;

        public bool SendComplete()
        {
            return !unconfirmedDgrams.Any();
        }

        public override void Flush()
        {
            Flush(TimeSpan.Zero);
        }

        public void Flush(TimeSpan timeout)
        {
            var dgrams = from dgram in unconfirmedDgrams where DateTime.Now - dgram.Value >= timeout select dgram.Key;
            foreach (var dgram in dgrams.ToArray())
            {
                client.Send(dgram.ToBson());
                unconfirmedDgrams[dgram] = DateTime.Now;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            while (unconfirmedDgrams.Count > 100)
            {
                Flush();
                Task.Delay(100).Wait();
            }
            var newBuffer = buffer.Skip(offset).Take(count).ToArray();
            if (newBuffer.Length > 1024)
            {
                var chunks = newBuffer.Chunk(1024);
                foreach (var chunk in chunks)
                {
                    Write(chunk);
                }
                return;
            }
            var dataDgram = new DataDgram { Data = newBuffer, Sequence = ++sequence, Checksum = Crc32.HashToUInt32(newBuffer) };
            unconfirmedDgrams.Add(dataDgram, DateTime.Now);
            position += newBuffer.Length;
            client.Send(dataDgram.ToBson());
        }

        protected override void Dispose(bool disposing)
        {
            cancellationTokenSource.Cancel();
            base.Dispose(disposing);
        }
    }
}
