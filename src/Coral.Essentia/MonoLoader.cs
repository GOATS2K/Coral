using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace Coral.Essentia;

public class MonoLoader : Configurable
{
    private string _filename = string.Empty;
    private float _sampleRate = 16000f;
    private int _resampleQuality = 1;

    public override void DeclareParameters()
    {
        DeclareParameter("filename", string.Empty, "The name of the audio file to load.");
        DeclareParameter("sampleRate", 16000f, "The desired output sampling rate in Hz.");
        DeclareParameter("resampleQuality", 1, "Resampling quality (0=best, 1=medium, 2=fast, 4=linear).");
    }

    public override void Configure()
    {
        _filename = GetParameter<string>("filename");
        _sampleRate = GetParameter<float>("sampleRate");
        _resampleQuality = GetParameter<int>("resampleQuality");
    }

    public unsafe float[] Compute()
    {
        if (string.IsNullOrEmpty(_filename))
            throw new EssentiaException("MonoLoader has not been configured with a filename.");

        AVFormatContext* formatContext = null;
        AVCodecContext* codecContext = null;
        SwrContext* swrContext = null;
        AVPacket* packet = null;
        AVFrame* frame = null;
        IntPtr srcState = IntPtr.Zero;

        var resampledChunks = new List<float[]>();

        try
        {
            if (ffmpeg.avformat_open_input(&formatContext, _filename, null, null) != 0)
                throw new EssentiaException($"Could not open file: {_filename}");

            if (ffmpeg.avformat_find_stream_info(formatContext, null) < 0)
                throw new EssentiaException("Could not find stream information.");

            AVCodec* codec = null;
            int audioStreamIndex =
                ffmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &codec, 0);
            if (audioStreamIndex < 0) throw new EssentiaException("No audio stream found.");

            codecContext = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(codecContext, formatContext->streams[audioStreamIndex]->codecpar);
            if (ffmpeg.avcodec_open2(codecContext, codec, null) < 0)
                throw new EssentiaException("Could not open codec.");

            float originalSampleRate = codecContext->sample_rate;
            if (originalSampleRate <= 0)
                throw new EssentiaException($"Source file '{_filename}' has an invalid sample rate.");

            double srcRatio = _sampleRate / originalSampleRate;
            srcState = Libsamplerate.src_new((Libsamplerate.ConverterType) _resampleQuality, 1, out int error);
            if (srcState == IntPtr.Zero)
                throw new EssentiaException($"Libsamplerate src_new failed: {Libsamplerate.src_strerror(error)}");

            swrContext = ffmpeg.swr_alloc();
            var outputChannelLayout = new AVChannelLayout();
            ffmpeg.av_channel_layout_from_string(&outputChannelLayout, "mono");
            ffmpeg.av_opt_set_chlayout(swrContext, "in_chlayout", &codecContext->ch_layout, 0);
            ffmpeg.av_opt_set_int(swrContext, "in_sample_rate", (int) originalSampleRate, 0);
            ffmpeg.av_opt_set_sample_fmt(swrContext, "in_sample_fmt", codecContext->sample_fmt, 0);
            ffmpeg.av_opt_set_chlayout(swrContext, "out_chlayout", &outputChannelLayout, 0);
            ffmpeg.av_opt_set_int(swrContext, "out_sample_rate", (int) originalSampleRate, 0);
            ffmpeg.av_opt_set_sample_fmt(swrContext, "out_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_FLT, 0);
            ffmpeg.av_channel_layout_uninit(&outputChannelLayout);
            if (ffmpeg.swr_init(swrContext) < 0) throw new EssentiaException("Could not initialize SWR context.");

            packet = ffmpeg.av_packet_alloc();
            frame = ffmpeg.av_frame_alloc();

            while (ffmpeg.av_read_frame(formatContext, packet) >= 0)
            {
                if (packet->stream_index == audioStreamIndex && ffmpeg.avcodec_send_packet(codecContext, packet) >= 0)
                {
                    while (ffmpeg.avcodec_receive_frame(codecContext, frame) >= 0)
                    {
                        ProcessDecodedFrame(frame, swrContext, srcState, srcRatio, resampledChunks);
                    }
                }

                ffmpeg.av_packet_unref(packet);
            }

            // Flush the decoder and resampler
            ffmpeg.avcodec_send_packet(codecContext, null);
            while (ffmpeg.avcodec_receive_frame(codecContext, frame) >= 0)
            {
                ProcessDecodedFrame(frame, swrContext, srcState, srcRatio, resampledChunks);
            }

            // Final flush for libsamplerate's internal buffer
            ProcessDecodedFrame(null, swrContext, srcState, srcRatio, resampledChunks);
        }
        finally
        {
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_packet_free(&packet);
            ffmpeg.swr_free(&swrContext);
            ffmpeg.avcodec_free_context(&codecContext);
            ffmpeg.avformat_close_input(&formatContext);
            if (srcState != IntPtr.Zero)
            {
                Libsamplerate.src_delete(srcState);
            }
        }

        int totalSamples = resampledChunks.Sum(c => c.Length);
        var finalAudio = new float[totalSamples];
        int offset = 0;
        foreach (var chunk in resampledChunks)
        {
            Array.Copy(chunk, 0, finalAudio, offset, chunk.Length);
            offset += chunk.Length;
        }

        return finalAudio;
    }

    // CORRECTED: Logic moved to a dedicated, unsafe helper method.
    private unsafe void ProcessDecodedFrame(AVFrame* currentFrame, SwrContext* swrContext, IntPtr srcState,
        double srcRatio, List<float[]> resampledChunks)
    {
        int decodedSamples = currentFrame == null ? 0 : currentFrame->nb_samples;
        if (decodedSamples <= 0 && currentFrame != null) return;

        float[] decodedAudio = new float[decodedSamples];

        if (decodedSamples > 0)
        {
            fixed (float* pDecodedAudio = decodedAudio)
            {
                var pOut = (byte*) pDecodedAudio;
                ffmpeg.swr_convert(swrContext, &pOut, decodedSamples, (byte**) &currentFrame->data,
                    currentFrame->nb_samples);
            }
        }

        int outputFramesEstimate = (int) (decodedAudio.Length * srcRatio) + 4096; // Ensure buffer is large enough
        float[] resampledAudio = new float[outputFramesEstimate];
        var srcData = new Libsamplerate.SRC_DATA
        {
            input_frames = decodedAudio.Length,
            src_ratio = srcRatio,
            output_frames = resampledAudio.Length,
            end_of_input = currentFrame == null ? 1 : 0 // Set end_of_input for the final flush
        };

        fixed (float* inPtr = decodedAudio, outPtr = resampledAudio)
        {
            srcData.data_in = inPtr;
            srcData.data_out = outPtr;
            int error = Libsamplerate.src_process(srcState, ref srcData);
            if (error != 0)
            {
                throw new EssentiaException($"Libsamplerate src_process failed: {Libsamplerate.src_strerror(error)}");
            }
        }

        if (srcData.output_frames_generated > 0)
        {
            Array.Resize(ref resampledAudio, srcData.output_frames_generated);
            resampledChunks.Add(resampledAudio);
        }
    }
}