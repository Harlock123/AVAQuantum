namespace AVAQuantum.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

public enum Phase
{
    /// <summary>A wave's start: its number called, the particles coming out of the nucleus.</summary>
    Ready,
    /// <summary>The wave, until everything is caught.</summary>
    Play,
    /// <summary>The probe hit: breaking up, before the life is taken.</summary>
    Down,
    /// <summary>The wave cleared: its bonus, before the next.</summary>
    Clear,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Catch, MultiCatch, Loop, Split, AtomBreak, AtomForm, Zap, Mew, Charge, Emerge, ExtraLife,
    WaveStart, WaveClear, GameOver, TitleTune, MusicStop,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it runs in fixed
/// 60 Hz steps, so it plays the same at any frame rate.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 14;
    public const double AttractSeconds = 50;
    public const double HighScoreSeconds = 9;
    public const double ReadySeconds = 2.2, ClearSeconds = 3.2, DownSeconds = 2.2;

    private double _accumulator, _idle;
    private bool _startPrev, _tunePending = true;
    private readonly int _seed;
    private int _games;

    public GameEngine(HighScoreTable? table = null, int seed = 1)
    {
        HighScores = table ?? HighScoreTable.Load();
        _seed = seed;
        NewGame();
        State = GameState.Title;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Ready;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public bool Silent { get; set; }
    public World World { get; private set; } = new();
    public Pilot Pilot { get; } = new();
    public int Score => World.Score;
    public string? Message { get; private set; }
    public string? SubMessage { get; private set; }
    public double MessageTime { get; private set; }
    /// <summary>What the last wave cleared was worth.</summary>
    public int LastBonus { get; private set; }

    private void Play(Sfx s)
    {
        // The demonstration plays in silence, the way the arcade machines' attract modes mostly did.
        if (!Silent && State != GameState.Attract) Sound?.Invoke(s);
    }

    private void Say(string text, string? sub = null, double seconds = 2.5)
    {
        Message = text;
        SubMessage = sub;
        MessageTime = seconds;
    }

    public void Update(double dt, Controls c, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(c, start);
        }
    }

    public void Tick(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        if (MessageTime > 0) { MessageTime -= dt; if (MessageTime <= 0) Message = SubMessage = null; }
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTune); }
                _idle += dt;
                if (startPressed) StartGame();
                else if (_idle > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(Pilot.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(c);
                break;
            case GameState.GameOver:
                World.Step(default, live: false);
                World.Heard.Clear();
                if (StateTime > 4)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, World.Wave + 1);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    private void NewGame()
    {
        World = new World();
        World.Start(_seed * 1000 + _games++);
        SetPhase(Phase.Ready);
        Message = SubMessage = null;
    }

    public void StartGame()
    {
        NewGame();
        State = GameState.Playing;
        StateTime = 0;
        NewEntryIndex = -1;
        Play(Sfx.MusicStop);
        Callout();
    }

    private void StartAttract()
    {
        Play(Sfx.MusicStop);
        NewGame();
        State = GameState.Attract;
        StateTime = 0;
        Pilot.Reset();
        Callout();
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
        _tunePending = true;
        Play(Sfx.MusicStop);
        Message = SubMessage = null;
        NewGame();
    }

    private void ShowHighScores(int place)
    {
        Play(Sfx.MusicStop);
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
    }

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }

    /// <summary>A wave's number called at its start, with a word on anything new in it.</summary>
    private void Callout()
    {
        int wave = World.Wave;
        string? sub = wave switch
        {
            0 => "LOOP YOUR TRAIL ROUND THE PARTICLES TO CATCH THEM",
            1 => "SPLITTERS SPLIT IN TWO WHEN CAUGHT",
            2 => "PULSARS ARE DEADLY WHILE CHARGED.  KITTENS EAT YOUR TRAIL",
            3 => "CATCH ALL FOUR OF A FOURSQUARE IN ONE LOOP FOR A BONUS",
            4 => "BUTTERFLIES FLUTTER.  LOOP THE WHOLE ATOM TO BREAK IT",
            _ => null,
        };
        Say($"WAVE {wave + 1}", sub, ReadySeconds + 0.6);
        Play(Sfx.WaveStart);
    }

    private void Simulate(Controls c)
    {
        double dt = Field.Step;
        PhaseTime += dt;
        var w = World;
        switch (Phase)
        {
            case Phase.Ready or Phase.Play:
                w.Step(c);
                if (Phase == Phase.Ready && PhaseTime > ReadySeconds) SetPhase(Phase.Play);
                if (w.Probe.Down) SetPhase(Phase.Down);
                else if (w.Cleared) WaveClear();
                break;
            case Phase.Down:
                w.Step(default, live: false);
                if (PhaseTime > DownSeconds)
                {
                    w.Lives--;
                    if (w.Lives <= 0) { GameOver(); break; }
                    w.Revive();
                    if (w.Cleared) WaveClear();
                    else SetPhase(Phase.Play);
                }
                break;
            case Phase.Clear:
                w.Step(c);
                if (PhaseTime > ClearSeconds) NextWave();
                break;
        }
        foreach (var s in w.Heard) Play(s);
        w.Heard.Clear();
    }

    private void WaveClear()
    {
        var w = World;
        SetPhase(Phase.Clear);
        LastBonus = WaveBook.ClearBonus(w.Wave);
        Say($"WAVE {w.Wave + 1} CLEAR", $"BONUS {LastBonus}", ClearSeconds);
        w.AddScore(LastBonus);
        Play(Sfx.WaveClear);
    }

    private void NextWave()
    {
        World.Begin(World.Wave + 1);
        SetPhase(Phase.Ready);
        Callout();
    }

    private void GameOver()
    {
        Play(Sfx.GameOver);
        if (State == GameState.Attract) { ShowHighScores(-1); return; }
        State = GameState.GameOver;
        StateTime = 0;
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Tick(c);
    }

    public void Idle(int steps) => Run(steps);

    /// <summary>Straight into a wave, past its callout.</summary>
    public void Go(int wave = 0)
    {
        NewGame();
        State = GameState.Playing;
        if (wave > 0) World.Begin(wave);
        SetPhase(Phase.Play);
        Message = null;
    }
}
