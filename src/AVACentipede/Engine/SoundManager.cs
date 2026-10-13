using AVACentipede.Audio;

namespace AVACentipede.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The
/// title tune and the wave tunes play on the music channel, the centipede's beat on its own loop. In a
/// virtual machine a larger mixing buffer keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.45f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune: _engine.PlayMusic(Path.Combine(_folder, "TitleTune.wav"), loop: true); return;
            case Sfx.WaveStart: _engine.PlayMusic(Path.Combine(_folder, "WaveStart.wav"), loop: false); return;
            case Sfx.GameOver: _engine.PlayMusic(Path.Combine(_folder, "GameOver.wav"), loop: false); return;
            // The centipede's march loops on its own channel while it's on the field.
            case Sfx.BeatStart: _engine.PlayAmbience(Path.Combine(_folder, "Beat.wav")); return;
            case Sfx.BeatStop: _engine.StopAmbience(); return;
        }
        string file = sfx switch
        {
            Sfx.Fire => "Fire.wav",
            Sfx.MushroomHit => "MushroomHit.wav",
            Sfx.MushroomGone => "MushroomGone.wav",
            Sfx.SegmentHit => "SegmentHit.wav",
            Sfx.HeadHit => "HeadHit.wav",
            Sfx.Flea => "Flea.wav",
            Sfx.FleaHit => "FleaHit.wav",
            Sfx.Spider => "Spider.wav",
            Sfx.SpiderDie => "SpiderDie.wav",
            Sfx.Scorpion => "Scorpion.wav",
            Sfx.ScorpionDie => "ScorpionDie.wav",
            Sfx.PlayerDie => "PlayerDie.wav",
            Sfx.Restore => "Restore.wav",
            _ => "ExtraLife.wav",
        };
        _engine.PlayEffect(Path.Combine(_folder, file), sfx is Sfx.Fire ? 0.4f : sfx is Sfx.MushroomHit ? 0.5f : 1f);
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
