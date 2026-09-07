namespace AdhocSystem.Api.Services.Audio;

public interface ISpeechToTextService
{
    Task<(string text, string language, double duration)> TranscribeAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);
}
