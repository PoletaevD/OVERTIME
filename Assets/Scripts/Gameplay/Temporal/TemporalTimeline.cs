using System;
using UnityEngine;

namespace Overtime
{
    internal enum TemporalDirection
    {
        Forward,
        Reverse
    }

    internal sealed class TemporalTimeline
    {
        internal const int TicksPerSecond = 120;
        internal const double TickDuration = 1d / TicksPerSecond;
        private const double TickTolerance = 0.00001d;

        private long _head;
        private double _fraction;
        private double _forwardRemainder;

        internal int Capacity { get; }
        internal int Count { get; private set; } = 1;
        internal long Cursor { get; private set; }
        internal long Oldest => _head - Count + 1;
        internal bool AtStart => Cursor == Oldest;
        internal TemporalDirection Direction { get; private set; }
        internal double Position => (Cursor - _fraction) * TickDuration;
        internal double Duration => (Count - 1) * TickDuration;
        internal double Depth => (_head - Cursor + _fraction) * TickDuration;
        internal float PreviewFraction => (float)_fraction;

        internal TemporalTimeline(float historySeconds)
        {
            Capacity = Math.Max(2, Mathf.FloorToInt(historySeconds * TicksPerSecond) + 1);
        }

        internal int Slot(long tick)
        {
            return (int)(tick % Capacity);
        }

        internal void AccumulateForward(double seconds)
        {
            if (Direction == TemporalDirection.Forward)
            {
                _forwardRemainder += seconds * TicksPerSecond;
            }
        }

        internal int StepsDue => (int)(_forwardRemainder + TickTolerance);

        internal int RecordNext()
        {
            _forwardRemainder = Math.Max(0d, _forwardRemainder - 1d);
            Cursor = ++_head;
            Count = Math.Min(Count + 1, Capacity);
            return Slot(Cursor);
        }

        internal void BeginReverse()
        {
            Direction = TemporalDirection.Reverse;
            _fraction = 0d;
            _forwardRemainder = 0d;
        }

        internal bool MoveBack(double seconds, float rate)
        {
            if (Direction != TemporalDirection.Reverse)
            {
                return false;
            }

            var from = Cursor;
            var distance = Math.Min(Cursor - Oldest, _fraction + seconds * rate * TicksPerSecond);
            var steps = Math.Min(Cursor - Oldest, (long)(distance + TickTolerance));
            Cursor -= steps;
            _fraction = Math.Max(0d, distance - steps);
            return from != Cursor;
        }

        internal void CommitBranch()
        {
            Count -= (int)(_head - Cursor);
            _head = Cursor;
            _fraction = 0d;
            Direction = TemporalDirection.Forward;
        }

        internal void Clear()
        {
            _head = 0;
            Cursor = 0;
            Count = 1;
            _fraction = 0d;
            _forwardRemainder = 0d;
            Direction = TemporalDirection.Forward;
        }
    }
}
