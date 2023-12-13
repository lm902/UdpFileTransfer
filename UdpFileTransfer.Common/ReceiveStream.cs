using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.IO.Hashing;
using System.Net;
using System.Net.Sockets;

namespace UdpFileTransfer.Common
{
    public class ReceiveStream : Stream
    {
        private UdpClient client;

        private IPEndPoint? remoteEP;

        public ReceiveStream(IPEndPoint localEP)
        {
            client = new UdpClient(localEP);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        private byte[]? unfinishedData;

        public override int Read(byte[] buffer, int offset, int count)
        {
            byte[] data;
            if (unfinishedData != null)
            {
                data = unfinishedData;
                unfinishedData = null;
            }
            else
            {
                data = ReceiveData();
            }

            if (data.Length == 1 && data[0] == 0)
            {
                SendAcks();
                Task.Delay(100).Wait();
                Close();
                return 0;
            }

            Buffer.BlockCopy(data, 0, buffer, offset, Math.Min(count, data.Length));
            if (count < data.LongLength)
            {
                unfinishedData = data.Skip(offset).ToArray();
                return count;
            }
            return data.Length;
        }

        private long sequence;

        private List<DataDgram> extraData = new List<DataDgram>();

        private byte[] ReceiveData()
        {
            byte[]? data = null;
            while (data == null)
            {
                var filter = from dgram in extraData where dgram.Sequence == sequence + 1 select dgram;
                if (filter.Any())
                {
                    var dgram = filter.First();
                    extraData.Remove(dgram);
                    sequence = dgram.Sequence;
                    data = dgram.Data;
                    continue;
                }
                else
                {
                    try
                    {
                        extraData.RemoveAll(dgram => dgram.Sequence <= sequence);
                        var dgram = BsonSerializer.Deserialize<DataDgram>(client.Receive(ref remoteEP));
                        if (Crc32.HashToUInt32(dgram.Data) != dgram.Checksum)
                        {
                            continue;
                        }

                        if (dgram.Sequence == sequence + 1)
                        {
                            sequence = dgram.Sequence;
                            SendAcks();
                            data = dgram.Data;
                            continue;
                        }
                        else
                        {
                            if (dgram.Sequence > sequence + 1)
                            {
                                extraData.Add(dgram);
                            }
                            SendAcks();
                            continue;
                        }
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode == SocketError.ConnectionReset)
                        {
                            Close();
                            data = new byte[0];
                            continue;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            return data;
        }

        private void SendAcks()
        {
            client.Send(new AckDgram
            {
                DataCompleteToSequence = sequence,
                ExtraDgramSequences = (from dgram in extraData select dgram.Sequence).Take(100).ToArray()
            }.ToBson(), remoteEP);
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
            throw new NotImplementedException();
        }
    }
}
