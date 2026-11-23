using System;
using NUnit.Framework;

namespace Overtime.Tests
{
    public sealed class TemporalTimelineTests
    {
        [Test]
        public void RingKeepsOnlyFourSecondsAndClampsAtOldest()
        {
            var timeline = new TemporalTimeline(4f);
            for (var tick = 1; tick <= 720; tick++)
            {
                timeline.RecordNext();
            }

            Assert.That(timeline.Count, Is.EqualTo(481));
            Assert.That(timeline.Duration, Is.EqualTo(4d));
            Assert.That(timeline.Oldest, Is.EqualTo(240));
            timeline.BeginReverse();
            Assert.That(timeline.MoveBack(8d, 2f), Is.True);
            Assert.That(timeline.Cursor, Is.EqualTo(240));
            Assert.That(timeline.Depth, Is.EqualTo(4d));
            Assert.That(timeline.AtStart, Is.True);
            Assert.That(timeline.MoveBack(1d, 2f), Is.False);
        }

        [Test]
        public void RepeatedBranchesOverwriteTheInvalidatedFuture()
        {
            var timeline = new TemporalTimeline(4f);
            var samples = new int[timeline.Capacity];
            for (var tick = 1; tick <= 480; tick++)
            {
                samples[timeline.RecordNext()] = tick;
            }

            for (var branch = 1; branch <= 3; branch++)
            {
                timeline.BeginReverse();
                var distance = branch == 1 ? 240 : 75;
                timeline.MoveBack(distance * TemporalTimeline.TickDuration / 2d, 2f);
                var split = timeline.Cursor;
                Assert.That(samples[timeline.Slot(split)], Is.EqualTo(branch == 1 ? 240 : (int)split + (branch - 1) * 1000));
                timeline.CommitBranch();
                Assert.That(timeline.Count, Is.EqualTo(split + 1));

                for (var offset = 1; offset <= 120; offset++)
                {
                    samples[timeline.RecordNext()] = (int)split + offset + branch * 1000;
                }
            }

            timeline.BeginReverse();
            timeline.MoveBack(0.1d, 2f);
            Assert.That(samples[timeline.Slot(timeline.Cursor)], Is.EqualTo(3000 + timeline.Cursor));
        }

        [Test]
        public void TimelinesCanAdvanceInOppositeDirectionsWithIndependentCursors()
        {
            var player = new TemporalTimeline(2f);
            var world = new TemporalTimeline(4f);
            for (var index = 1; index <= 200; index++)
            {
                player.RecordNext();
                world.RecordNext();
            }

            world.BeginReverse();
            player.AccumulateForward(0.25d);
            world.AccumulateForward(0.25d);
            world.MoveBack(0.25d, 2f);
            Assert.That(world.StepsDue, Is.Zero);
            Assert.That(player.StepsDue, Is.EqualTo(30));
            while (player.StepsDue > 0)
            {
                player.RecordNext();
            }

            Assert.That(player.Cursor, Is.EqualTo(230));
            Assert.That(world.Cursor, Is.EqualTo(140));
            world.CommitBranch();
            Assert.That(world.RecordNext(), Is.EqualTo(141));
            Assert.That(player.Cursor, Is.EqualTo(230));
        }

        [TestCase(30)]
        [TestCase(59)]
        [TestCase(144)]
        [TestCase(240)]
        public void ForwardAndReverseCadenceIsIndependentOfRenderRate(int frameRate)
        {
            var timeline = new TemporalTimeline(4f);
            var frame = (double)(1f / frameRate);
            for (var index = 0; index < frameRate * 3; index++)
            {
                timeline.AccumulateForward(frame);
                while (timeline.StepsDue > 0)
                {
                    timeline.RecordNext();
                }
            }

            Assert.That(timeline.Cursor, Is.EqualTo(360));
            timeline.BeginReverse();
            for (var index = 0; index < frameRate; index++)
            {
                timeline.MoveBack(frame, 2f);
            }

            Assert.That(timeline.Cursor, Is.EqualTo(120));
            Assert.That(timeline.Depth, Is.EqualTo(2d).Within(0.00001d));
        }

        [Test]
        public void FractionalPreviewCommitsTheLastCompleteSnapshot()
        {
            var timeline = new TemporalTimeline(1f);
            for (var index = 0; index < 4; index++)
            {
                timeline.RecordNext();
            }

            timeline.BeginReverse();
            timeline.MoveBack(TemporalTimeline.TickDuration * 0.75d, 2f);
            Assert.That(timeline.Cursor, Is.EqualTo(3));
            Assert.That(timeline.PreviewFraction, Is.EqualTo(0.5f));
            timeline.CommitBranch();
            Assert.That(timeline.Count, Is.EqualTo(4));
            Assert.That(timeline.PreviewFraction, Is.Zero);
        }

        [Test]
        public void RingDoesNotAllocateAcrossLongRunningBranches()
        {
            var timeline = new TemporalTimeline(4f);
            Exercise(timeline, 2400);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Exercise(timeline, 240000);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            Assert.That(timeline.Count, Is.LessThanOrEqualTo(481));
        }

        private void Exercise(TemporalTimeline timeline, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                timeline.RecordNext();
                if (tick % 500 == 0)
                {
                    timeline.BeginReverse();
                    timeline.MoveBack(0.25d, 2f);
                    timeline.CommitBranch();
                }
            }
        }
    }
}
