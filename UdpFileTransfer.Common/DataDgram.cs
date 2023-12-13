using MongoDB.Bson.Serialization.Attributes;

namespace UdpFileTransfer.Common
{
    public class DataDgram
    {
        [BsonElement("D")]
        public required byte[] Data { get; set; }
        [BsonElement("S")]
        public long Sequence { get; set; }
        [BsonElement("C")]
        public long Checksum { get; set; }
    }
}
