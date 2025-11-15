using System.Threading.Channels;
using Coral.Database.Models;

namespace Coral.Services.ChannelWrappers;

public record EmbeddingJob(Track Track, string? RequestId);

public interface IEmbeddingChannel
{
    ChannelWriter<EmbeddingJob> GetWriter();
    ChannelReader<EmbeddingJob> GetReader();
}

public class EmbeddingChannel : IEmbeddingChannel
{
    private readonly Channel<EmbeddingJob> _channel;

    public EmbeddingChannel()
    {
        _channel = Channel.CreateUnbounded<EmbeddingJob>();
    }

    public ChannelWriter<EmbeddingJob> GetWriter() => _channel.Writer;
    public ChannelReader<EmbeddingJob> GetReader() => _channel.Reader;
}