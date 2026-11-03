using System;

namespace Lexer.Kf2.XpBar;

// This animation takes native progression samples but has no game dependencies.
internal sealed class XpAnimation
{
    public const double Duration = 0.45;
    public const byte HighlightRed = 248, HighlightGreen = 216, HighlightBlue = 128;
    bool _seeded;
    uint _xp;
    int _level;
    double _start, _target, _startedAt;

    public void Reset() => _seeded = false;

    public (double Fill, double EarnedEnd) Sample(uint xp, int level, double progress, double now)
    {
        double target = level + Math.Clamp(progress, 0, 1);
        if (!_seeded || xp < _xp || level < _level)
        {
            _seeded = true;
            _start = _target = target;
        }
        else if (target != _target)
        {
            double displayed = Position(now);
            if (target > displayed && (xp > _xp || displayed < _target))
            {
                _start = displayed;
                _startedAt = now;
                _target = target;
            }
            else
            {
                _start = _target = target;
            }
        }
        _xp = xp;
        _level = level;
        double position = Position(now);
        double lap = Math.Min(Math.Floor(position), level);
        return (position - lap, Math.Min(_target - lap, 1));
    }

    double Position(double now)
    {
        double remaining = 1 - Math.Clamp((now - _startedAt) / Duration, 0, 1);
        return _target - (_target - _start) * remaining * remaining * remaining;
    }
}
