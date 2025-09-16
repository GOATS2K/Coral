using System.Threading.Channels;
using Coral.Database.Models;

namespace Coral.Services.ChannelWrappers;

public interface IEmbeddingChannel
{
    ChannelWriter<Track> GetWriter();
    ChannelReader<Track> GetReader();
}

public class EmbeddingChannel : IEmbeddingChannel
{
    private readonly Channel<Track> _channel;

    public EmbeddingChannel()
    {
        _channel = Channel.CreateUnbounded<Track>();
    }

    public ChannelWriter<Track> GetWriter() => _channel.Writer;
    public ChannelReader<Track> GetReader() => _channel.Reader;
}