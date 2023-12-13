using MongoDB.Bson.Serialization.Attributes;

namespace UdpFileTransfer.Common
{
    public class AckDgram
    {
        [BsonElement("S")]
        public long DataCompleteToSequence { get; set; }
        [BsonElement("E")]
        public required long[] ExtraDgramSequences { get; set; }
    }
}
