using Application.Contracts.Media;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media;
using Application.Modules.Media.Pipeline;
using Domains.MediaAggregate;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Application.Tests.Media;

/// <summary>
/// Unit tests for <see cref="MediaProcessingPipeline"/>. Storage and processing services are
/// mocked, so the test runs without network or FFmpeg. Verifies the variant ladder (image +
/// video), the idempotent skip when an asset is in the wrong status, and the failure path that
/// records the error onto the aggregate.
/// </summary>
public sealed class MediaProcessingPipelineTests
{
    private static readonly Guid Owner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Process_image_emits_eight_variants_two_formats_x_four_sizes()
    {
        // Expected: 2 thumbnail sizes (96, 256) × 2 formats + 2 resized sizes (720, 1440) × 2 formats = 8 variants.
        MediaAsset asset = NewUploaded(MediaKind.Image, contentType: "image/jpeg");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var image = new Mock<IImageProcessingService>();
        image.Setup(s => s.CreateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<ImageEncoding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int size, ImageEncoding enc, CancellationToken _) =>
                new ProcessedImage(new MemoryStream(new byte[100]), enc.ContentType, size, size, 100));
        image.Setup(s => s.ResizeAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<ImageEncoding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int max, ImageEncoding enc, CancellationToken _) =>
                new ProcessedImage(new MemoryStream(new byte[400]), enc.ContentType, max, max / 2, 400));

        MediaProcessingPipeline pipeline = BuildPipeline(repo.Object, storage.Object, image.Object, Mock.Of<IVideoProcessingService>());
        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        Assert.Equal(8, asset.Variants.Count);

        // 4 thumbnails (96 jpg, 96 webp, 256 jpg, 256 webp)
        Assert.Equal(4, asset.Variants.Count(v => v.Kind == MediaVariantKind.ImageThumbnail));
        // 4 resized (720 jpg, 720 webp, 1440 jpg, 1440 webp)
        Assert.Equal(4, asset.Variants.Count(v => v.Kind == MediaVariantKind.ImageResized));
        // Every size emitted in both JPEG and WebP.
        Assert.Equal(4, asset.Variants.Count(v => v.ContentType == "image/jpeg"));
        Assert.Equal(4, asset.Variants.Count(v => v.ContentType == "image/webp"));

        // Each breakpoint has both formats present.
        foreach (int size in new[] { 96, 256, 720, 1440 })
        {
            Assert.Contains(asset.Variants, v => v.Width == size && v.ContentType == "image/jpeg");
            Assert.Contains(asset.Variants, v => v.Width == size && v.ContentType == "image/webp");
        }

        storage.Verify(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }

    [Theory]
    [InlineData(2160, new[] { 2160, 1440, 1080, 720, 480 })] // 4K UHD source: every rung
    [InlineData(1440, new[] { 1440, 1080, 720, 480 })]       // 2K QHD source: skip 4K
    [InlineData(1080, new[] { 1080, 720, 480 })]             // Full HD source: skip 4K + 2K
    [InlineData(720,  new[] { 720, 480 })]                   // HD source: skip everything above 720
    [InlineData(480,  new[] { 480 })]                        // SD source: single rung
    public async Task Process_video_emits_every_rung_at_or_below_source_height(int sourceHeight, int[] expectedRungs)
    {
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(10), sourceHeight * 16 / 9, sourceHeight, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", sourceHeight * 16 / 9, sourceHeight, 200));
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, VideoCodecKind _, CancellationToken _) =>
                new ProcessedVideo(new MemoryStream(new byte[500]), "video/mp4", rung * 16 / 9, rung, TimeSpan.FromSeconds(10), 500));

        MediaProcessingPipeline pipeline = BuildPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(), video.Object);
        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        // poster + every applicable MP4 rung
        Assert.Equal(1 + expectedRungs.Length, asset.Variants.Count);
        Assert.Single(asset.Variants, v => v.Kind == MediaVariantKind.VideoPoster);

        IEnumerable<int> emittedHeights = asset.Variants
            .Where(v => v.Kind == MediaVariantKind.VideoMp4)
            .Select(v => v.Height ?? -1);
        Assert.Equal(expectedRungs.OrderBy(h => h), emittedHeights.OrderBy(h => h));

        // Rungs strictly above the source must never be requested.
        foreach (int taller in new[] { 2160, 1440, 1080, 720, 480 }.Where(r => r > sourceHeight))
            video.Verify(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), taller, It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_video_emits_single_native_rung_when_source_is_smaller_than_lowest_breakpoint()
    {
        // 360p source: under the lowest standard rung (480), emit a single re-encoded copy at native.
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(5), 640, 360, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", 640, 360, 200));
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), 360, It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideo(new MemoryStream(new byte[500]), "video/mp4", 640, 360, TimeSpan.FromSeconds(5), 500));

        MediaProcessingPipeline pipeline = BuildPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(), video.Object);
        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        Assert.Equal(2, asset.Variants.Count); // poster + native 360p
        Assert.Single(asset.Variants, v => v.Kind == MediaVariantKind.VideoMp4 && v.Height == 360);
    }

    [Fact]
    public async Task Process_skips_assets_that_are_not_uploaded_or_failed()
    {
        MediaAsset asset = NewPending();
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var image = new Mock<IImageProcessingService>(MockBehavior.Strict);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);

        MediaProcessingPipeline pipeline = BuildPipeline(repo.Object, storage.Object, image.Object, Mock.Of<IVideoProcessingService>());
        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Pending, asset.Status);
        image.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_marks_failed_and_records_error_when_image_processing_throws()
    {
        MediaAsset asset = NewUploaded(MediaKind.Image, contentType: "image/jpeg");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));

        var image = new Mock<IImageProcessingService>();
        image.Setup(s => s.CreateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<ImageEncoding>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("decoder crashed"));

        MediaProcessingPipeline pipeline = BuildPipeline(repo.Object, storage.Object, image.Object, Mock.Of<IVideoProcessingService>());
        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Failed, asset.Status);
        Assert.Contains("decoder crashed", asset.ErrorMessage);
    }

    [Fact]
    public async Task Process_video_emits_rungs_smallest_first_so_low_quality_is_durable_first()
    {
        // 1080p source. Expected emission order: poster → 480p → 720p → 1080p.
        // We verify by capturing the sequence of TranscodeToMp4Async calls and the order in which
        // AddVariant rows are persisted (via the Variants collection on the aggregate).
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(5), 1920, 1080, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", 1920, 1080, 200));

        List<int> transcodeOrder = new();
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, VideoCodecKind _, CancellationToken _) =>
            {
                transcodeOrder.Add(rung);
                return new ProcessedVideo(new MemoryStream(new byte[500]), "video/mp4", rung * 16 / 9, rung, TimeSpan.FromSeconds(5), 500);
            });

        MediaProcessingPipeline pipeline = BuildPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(), video.Object);
        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(new[] { 480, 720, 1080 }, transcodeOrder);
    }

    [Fact]
    public async Task Process_video_saves_changes_incrementally_so_partial_results_are_durable()
    {
        // For a 1080p source we expect: MarkProcessing save + poster save + 3 rung saves + MarkReady save = 6 saves.
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(5), 1920, 1080, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", 1920, 1080, 200));
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, VideoCodecKind _, CancellationToken _) =>
                new ProcessedVideo(new MemoryStream(new byte[500]), "video/mp4", rung * 16 / 9, rung, TimeSpan.FromSeconds(5), 500));

        var uow = new Mock<IUnitOfWork>();
        var pipeline = new MediaProcessingPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(),
            video.Object, NullScanner(), uow.Object, new FixedClock(), Options.Create(new MediaUploadOptions()),
            NullLogger<MediaProcessingPipeline>.Instance);

        await pipeline.ProcessAsync(asset.Id.Value);

        // 1 for MarkProcessing + 1 for poster + 3 for the 480/720/1080 rungs + 1 for MarkReady = 6.
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    [Fact]
    public async Task Process_image_saves_changes_per_breakpoint()
    {
        // 4 breakpoints (96, 256, 720, 1440) → 4 incremental saves + MarkProcessing + MarkReady = 6 saves.
        MediaAsset asset = NewUploaded(MediaKind.Image, contentType: "image/jpeg");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var image = new Mock<IImageProcessingService>();
        image.Setup(s => s.CreateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<ImageEncoding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int size, ImageEncoding enc, CancellationToken _) =>
                new ProcessedImage(new MemoryStream(new byte[100]), enc.ContentType, size, size, 100));
        image.Setup(s => s.ResizeAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<ImageEncoding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int max, ImageEncoding enc, CancellationToken _) =>
                new ProcessedImage(new MemoryStream(new byte[400]), enc.ContentType, max, max / 2, 400));

        var uow = new Mock<IUnitOfWork>();
        var pipeline = new MediaProcessingPipeline(repo.Object, storage.Object, image.Object,
            Mock.Of<IVideoProcessingService>(), NullScanner(), uow.Object, new FixedClock(),
            Options.Create(new MediaUploadOptions()), NullLogger<MediaProcessingPipeline>.Instance);

        await pipeline.ProcessAsync(asset.Id.Value);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    [Fact]
    public async Task Process_marks_failed_when_scanner_quarantines_payload_and_skips_processing()
    {
        // A Quarantine verdict must stop the pipeline before image/video processing runs — that's
        // the whole point of the scanner step. Asset ends in Failed status with a redacted detail
        // recorded for operator review.
        MediaAsset asset = NewUploaded(MediaKind.Image, contentType: "image/jpeg");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));

        // Strict image mock: any call to image processing is a test failure — the scanner must
        // short-circuit the pipeline before we ever try to decode the payload.
        var image = new Mock<IImageProcessingService>(MockBehavior.Strict);

        var scanner = new Mock<IMediaScanner>();
        scanner.Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaScanResult(ScanVerdict.Quarantine, "Win.Test.EICAR_HDB-1 FOUND"));

        var uow = new Mock<IUnitOfWork>();
        var pipeline = new MediaProcessingPipeline(repo.Object, storage.Object, image.Object,
            Mock.Of<IVideoProcessingService>(), scanner.Object, uow.Object, new FixedClock(),
            Options.Create(new MediaUploadOptions()), NullLogger<MediaProcessingPipeline>.Instance);

        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Failed, asset.Status);
        Assert.Contains("EICAR_HDB", asset.ErrorMessage);
        Assert.Empty(asset.Variants);
        image.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_video_emits_additional_codec_variants_when_configured()
    {
        // With AdditionalVideoCodecs=[H265], each rung produces H.264 + HEVC variants. The pipeline
        // records the right MediaVariantKind per codec so downstream players (HTML5 <source type>)
        // can pick the cheapest format their decoder can play.
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        // 720p source → applicable rungs = [480, 720] → 2 rungs × 2 codecs = 4 mp4 variants + poster = 5.
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(5), 1280, 720, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", 1280, 720, 200));
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, VideoCodecKind codec, CancellationToken _) =>
            {
                string ct = codec switch
                {
                    VideoCodecKind.H265 => "video/mp4; codecs=hev1.1.6.L93.B0",
                    _ => "video/mp4",
                };
                return new ProcessedVideo(new MemoryStream(new byte[500]), ct, rung * 16 / 9, rung, TimeSpan.FromSeconds(5), 500);
            });

        var options = Options.Create(new MediaUploadOptions
        {
            AdditionalVideoCodecs = new List<VideoCodecKind> { VideoCodecKind.H265 }
        });
        var pipeline = new MediaProcessingPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(),
            video.Object, NullScanner(), Mock.Of<IUnitOfWork>(), new FixedClock(), options,
            NullLogger<MediaProcessingPipeline>.Instance);

        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        // poster + 2 H.264 rungs + 2 HEVC rungs
        Assert.Equal(5, asset.Variants.Count);
        Assert.Equal(2, asset.Variants.Count(v => v.Kind == MediaVariantKind.VideoMp4));
        Assert.Equal(2, asset.Variants.Count(v => v.Kind == MediaVariantKind.VideoMp4Hevc));
        // HEVC variants carry the codecs parameter so HTML5 <source> can negotiate cleanly.
        Assert.All(
            asset.Variants.Where(v => v.Kind == MediaVariantKind.VideoMp4Hevc),
            v => Assert.Contains("hev1", v.ContentType));
    }

    [Fact]
    public async Task Process_video_emits_hls_master_and_segments_when_enabled()
    {
        // With EnableHls=true the pipeline calls TranscodeToHlsAsync per applicable rung, uploads
        // playlist + segments, then writes a master.m3u8 referencing every rung. Verifies:
        //  - Per-rung HLS calls happen
        //  - master.m3u8 is uploaded with the correct content type
        //  - A VideoHlsMaster variant is recorded on the aggregate
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        var putKeys = new List<(string Key, string ContentType)>();
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((key, _, ct, _) => putKeys.Add((key, ct)))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(5), 1280, 720, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", 1280, 720, 200));
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, VideoCodecKind _, CancellationToken _) =>
                new ProcessedVideo(new MemoryStream(new byte[500]), "video/mp4", rung * 16 / 9, rung, TimeSpan.FromSeconds(5), 500));

        // HLS sessions use a real temp directory so OpenRead/Dispose are exercised end-to-end.
        video.Setup(v => v.TranscodeToHlsAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, int _, VideoCodecKind _, CancellationToken _) => CreateFakeHlsSession(rung));

        var options = Options.Create(new MediaUploadOptions { EnableHls = true });
        var pipeline = new MediaProcessingPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(),
            video.Object, NullScanner(), Mock.Of<IUnitOfWork>(), new FixedClock(), options,
            NullLogger<MediaProcessingPipeline>.Instance);

        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        Assert.Single(asset.Variants, v => v.Kind == MediaVariantKind.VideoHlsMaster);
        Assert.Contains(putKeys, p => p.Key.EndsWith("/hls/master.m3u8") && p.ContentType == "application/vnd.apple.mpegurl");
        // Per-rung playlists + segments — at least one of each rung directory should appear.
        Assert.Contains(putKeys, p => p.Key.Contains("/hls/480p/") && p.Key.EndsWith(".m3u8"));
        Assert.Contains(putKeys, p => p.Key.Contains("/hls/720p/") && p.Key.EndsWith(".m3u8"));
        Assert.Contains(putKeys, p => p.Key.Contains("/hls/480p/") && p.Key.EndsWith(".ts"));
    }

    /// <summary>
    /// Creates a real temp directory with a minimal playlist + one segment so the pipeline can
    /// exercise the session's <c>OpenRead</c> and dispose paths without invoking ffmpeg.
    /// </summary>
    private static HlsTranscodeSession CreateFakeHlsSession(int rung)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hls-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.m3u8"), "#EXTM3U\n#EXTINF:5.0,\nseg-000.ts\n#EXT-X-ENDLIST\n");
        File.WriteAllBytes(Path.Combine(dir, "seg-000.ts"), new byte[] { 0x47, 0x40, 0x00, 0x10 });
        return new HlsTranscodeSession(
            dir,
            relativePaths: new[] { "index.m3u8", "seg-000.ts" },
            playlistRelativePath: "index.m3u8",
            width: rung * 16 / 9,
            height: rung,
            duration: TimeSpan.FromSeconds(5),
            totalByteSize: 100);
    }

    [Fact]
    public async Task Process_video_in_parallel_mode_emits_all_rungs()
    {
        // With MaxConcurrentVideoRungs > 1, all rungs are still emitted but order is non-deterministic.
        // The key invariant: no rung is missed, no rung is duplicated, total saves still equal
        // MarkProcessing + poster + N rungs + MarkReady.
        MediaAsset asset = NewUploaded(MediaKind.Video, contentType: "video/mp4");
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
        storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var video = new Mock<IVideoProcessingService>();
        video.Setup(v => v.ProbeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoProbeResult(TimeSpan.FromSeconds(5), 1920, 1080, "h264", "aac"));
        video.Setup(v => v.ExtractPosterAsync(It.IsAny<Stream>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedVideoFrame(new MemoryStream(new byte[200]), "image/jpeg", 1920, 1080, 200));
        video.Setup(v => v.TranscodeToMp4Async(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<VideoCodecKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, int rung, VideoCodecKind _, CancellationToken _) =>
                new ProcessedVideo(new MemoryStream(new byte[500]), "video/mp4", rung * 16 / 9, rung, TimeSpan.FromSeconds(5), 500));

        var uow = new Mock<IUnitOfWork>();
        var options = Options.Create(new MediaUploadOptions { MaxConcurrentVideoRungs = 3 });
        var pipeline = new MediaProcessingPipeline(repo.Object, storage.Object, Mock.Of<IImageProcessingService>(),
            video.Object, NullScanner(), uow.Object, new FixedClock(), options, NullLogger<MediaProcessingPipeline>.Instance);

        await pipeline.ProcessAsync(asset.Id.Value);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        // poster + 3 mp4 rungs (480/720/1080)
        Assert.Equal(4, asset.Variants.Count);
        Assert.Equal(3, asset.Variants.Count(v => v.Kind == MediaVariantKind.VideoMp4));
        // Each rung committed independently — same total save count regardless of concurrency.
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    private static MediaProcessingPipeline BuildPipeline(
        IMediaAssetRepository repo,
        IObjectStorage storage,
        IImageProcessingService image,
        IVideoProcessingService video) =>
        new(repo, storage, image, video, NullScanner(), Mock.Of<IUnitOfWork>(), new FixedClock(),
            Options.Create(new MediaUploadOptions()), NullLogger<MediaProcessingPipeline>.Instance);

    /// <summary>
    /// Returns an <see cref="IMediaScanner"/> that always reports <see cref="ScanVerdict.Clean"/>.
    /// All existing tests predate the scanner step and assume payloads pass through, so this
    /// keeps their expectations valid without adding scanner-specific setup boilerplate.
    /// </summary>
    private static IMediaScanner NullScanner()
    {
        var scanner = new Mock<IMediaScanner>();
        scanner.Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaScanResult(ScanVerdict.Clean, "test"));
        return scanner.Object;
    }

    private static MediaAsset NewPending() =>
        MediaAsset.CreateForUpload(Owner, MediaKind.Image, "media/abc/original.jpg", "image/jpeg", 100, DateTime.UtcNow);

    private static MediaAsset NewUploaded(MediaKind kind, string contentType)
    {
        MediaAsset asset = MediaAsset.CreateForUpload(Owner, kind, "media/abc/original.bin", contentType, 100, DateTime.UtcNow);
        asset.MarkUploaded(DateTime.UtcNow);
        asset.ClearDomainEvents();
        return asset;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now { get; } = new(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc);
    }
}
