using UnityEditor;
using UnityEngine;

// Import settings for zone music in Assets/Resources/Music/: streamed from disk as Vorbis so a
// couple of minutes of music doesn't sit decompressed in memory.
class MusicImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/Music/";

    // Bump when these settings change, so Unity reimports the music with them.
    public override uint GetVersion() => 1;

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ai = (AudioImporter)assetImporter;
        ai.forceToMono = false;
        ai.loadInBackground = true;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
    }
}
