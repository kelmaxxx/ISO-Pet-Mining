using Godot;

// Placeholder audio: every clip is synthesized at startup so the game has
// sound before real SFX/music files exist. Replace the Build* calls in _Ready
// with GD.Load<AudioStream>("res://assets/audio/...") when the real audio lands.
public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	private const int Rate = 22050;
	private const int PoolSize = 6;
	private const float MinHitGapMsec = 60f;

	private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[PoolSize];
	private int _nextPlayer;
	private ulong _lastHitMsec;

	private AudioStreamWav _hit;
	private AudioStreamWav _coin;
	private AudioStreamWav _hatch;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_hit = BuildSweep(220f, 70f, 0.09f, square: true);
		_coin = BuildSweep(880f, 1500f, 0.10f);
		_hatch = BuildArpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f);

		for (int i = 0; i < PoolSize; i++)
		{
			_players[i] = new AudioStreamPlayer { VolumeDb = -8f };
			AddChild(_players[i]);
		}

		var music = new AudioStreamPlayer { Stream = BuildMusic(), VolumeDb = -16f };
		AddChild(music);
		music.Play();
	}

	public void PlayHit()
	{
		// Pets and the player hit several times a second; don't turn that into noise.
		ulong now = Time.GetTicksMsec();
		if (now - _lastHitMsec < MinHitGapMsec) return;
		_lastHitMsec = now;
		Play(_hit);
	}

	public void PlayCoin() => Play(_coin);

	public void PlayHatch() => Play(_hatch);

	private void Play(AudioStream stream)
	{
		var player = _players[_nextPlayer];
		_nextPlayer = (_nextPlayer + 1) % PoolSize;
		player.Stream = stream;
		player.Play();
	}

	private static AudioStreamWav BuildSweep(float fromHz, float toHz, float seconds, bool square = false)
	{
		int count = (int)(Rate * seconds);
		var data = new byte[count * 2];
		double phase = 0;
		for (int i = 0; i < count; i++)
		{
			float t = i / (float)count;
			phase += Mathf.Lerp(fromHz, toHz, t) / Rate;
			float wave = square
				? (Mathf.Sin((float)phase * Mathf.Tau) >= 0f ? 1f : -1f)
				: Mathf.Sin((float)phase * Mathf.Tau);
			WriteSample(data, i, wave * (1f - t) * 0.35f);
		}
		return Wav(data, count, loop: false);
	}

	private static AudioStreamWav BuildArpeggio(float[] notes, float noteSeconds)
	{
		int perNote = (int)(Rate * noteSeconds);
		int count = perNote * notes.Length;
		var data = new byte[count * 2];
		double phase = 0;
		for (int i = 0; i < count; i++)
		{
			float t = (i % perNote) / (float)perNote;
			phase += notes[i / perNote] / Rate;
			float wave = Mathf.Sin((float)phase * Mathf.Tau);
			WriteSample(data, i, wave * (1f - t) * 0.30f);
		}
		return Wav(data, count, loop: false);
	}

	private static AudioStreamWav BuildMusic()
	{
		float[] melody =
		{
			440.00f, 523.25f, 659.25f, 523.25f,
			392.00f, 493.88f, 587.33f, 493.88f,
			349.23f, 440.00f, 523.25f, 440.00f,
			329.63f, 392.00f, 493.88f, 392.00f,
		};
		float[] bass = { 110f, 0f, 0f, 0f, 98f, 0f, 0f, 0f, 87.31f, 0f, 0f, 0f, 82.41f, 0f, 0f, 0f };
		const float beat = 0.30f;

		int perNote = (int)(Rate * beat);
		int count = perNote * melody.Length;
		var data = new byte[count * 2];
		double phase = 0;
		double bassPhase = 0;
		for (int i = 0; i < count; i++)
		{
			int note = i / perNote;
			float t = (i % perNote) / (float)perNote;
			float env = Mathf.Min(1f, (1f - t) * 4f);

			phase += melody[note] / Rate;
			float lead = (Mathf.Sin((float)phase * Mathf.Tau) >= 0f ? 1f : -1f) * 0.12f * env;

			float low = 0f;
			if (bass[note] > 0f)
			{
				bassPhase += bass[note] / Rate;
				low = Mathf.Sin((float)bassPhase * Mathf.Tau) * 0.16f;
			}
			WriteSample(data, i, lead + low);
		}
		return Wav(data, count, loop: true);
	}

	private static AudioStreamWav Wav(byte[] data, int sampleCount, bool loop)
	{
		return new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = Rate,
			Stereo = false,
			Data = data,
			LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
			LoopBegin = 0,
			LoopEnd = sampleCount,
		};
	}

	private static void WriteSample(byte[] data, int index, float value)
	{
		short sample = (short)(Mathf.Clamp(value, -1f, 1f) * short.MaxValue);
		data[index * 2] = (byte)(sample & 0xFF);
		data[index * 2 + 1] = (byte)((sample >> 8) & 0xFF);
	}
}
