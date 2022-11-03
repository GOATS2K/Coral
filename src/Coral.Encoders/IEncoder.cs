using Coral.Encoders.EncodingModels;

namespace Coral.Encoders;

public interface IEncoder
{
    bool EnsureEncoderExists();
    IArgumentBuilder Configure();
    TranscodingJob ConfigureTranscodingJob(TranscodingJobRequest request);
}