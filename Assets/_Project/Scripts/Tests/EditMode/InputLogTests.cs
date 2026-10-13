using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Control;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class InputLogTests
    {
        // A scripted source that remembers how it was called.
        private sealed class ScriptedSource : IInputSource
        {
            private readonly Queue<TickInput> script = new Queue<TickInput>();
            public int Polls;
            public int Consumes;
            public int Clears;

            public ScriptedSource(params TickInput[] inputs)
            {
                foreach (TickInput input in inputs) script.Enqueue(input);
            }

            public void Poll() => Polls++;

            public TickInput Consume()
            {
                Consumes++;
                return script.Count > 0 ? script.Dequeue() : default;
            }

            public void Clear() => Clears++;
        }

        private static readonly TickInput A = new TickInput(0.25f, true, false, false, false, false);
        private static readonly TickInput B = new TickInput(-1f, false, true, false, true, false);
        private static readonly TickInput C = new TickInput(0f, false, false, true, false, true);

        private static void AssertSame(TickInput expected, TickInput actual, string where)
        {
            Assert.That(actual.Steer, Is.EqualTo(expected.Steer), where + " steer");
            Assert.That(actual.PrimaryPressed, Is.EqualTo(expected.PrimaryPressed), where + " primary");
            Assert.That(actual.ReleasePressed, Is.EqualTo(expected.ReleasePressed), where + " release");
            Assert.That(actual.AttackPressed, Is.EqualTo(expected.AttackPressed), where + " attack");
            Assert.That(actual.Drift, Is.EqualTo(expected.Drift), where + " drift");
            Assert.That(actual.ChainActionPressed, Is.EqualTo(expected.ChainActionPressed), where + " chain action");
        }

        // ---- InputLog ------------------------------------------------------------------------------

        [Test]
        public void Log_AddAndIndex_KeepsOrderAndAllSixFields()
        {
            var log = new InputLog();
            log.Add(A);
            log.Add(B);
            log.Add(C);
            Assert.That(log.Count, Is.EqualTo(3));
            AssertSame(A, log[0], "0");
            AssertSame(B, log[1], "1");
            AssertSame(C, log[2], "2");
        }

        [Test]
        public void Log_IndexOutOfRange_Throws()
        {
            var log = new InputLog();
            log.Add(A);
            Assert.Throws<ArgumentOutOfRangeException>(() => { TickInput x = log[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { TickInput x = log[-1]; });
        }

        [Test]
        public void Log_Clear_EmptiesIt()
        {
            var log = new InputLog();
            log.Add(A);
            log.Clear();
            Assert.That(log.Count, Is.Zero);
        }

        [Test]
        public void Log_CopyWith_ReplacesOneEntryAndLeavesTheOriginal()
        {
            var log = new InputLog();
            log.Add(A);
            log.Add(B);
            log.Add(C);
            InputLog changed = log.CopyWith(1, A);
            Assert.That(changed.Count, Is.EqualTo(3));
            AssertSame(A, changed[0], "0");
            AssertSame(A, changed[1], "1 replaced");
            AssertSame(C, changed[2], "2");
            AssertSame(B, log[1], "the original keeps its entry");
            Assert.Throws<ArgumentOutOfRangeException>(() => log.CopyWith(3, A));
        }

        // ---- InputRecorder -------------------------------------------------------------------------

        [Test]
        public void Recorder_Consume_ReturnsTheInnerValueAndRecordsItInOrder()
        {
            var inner = new ScriptedSource(A, B, C);
            var log = new InputLog();
            var recorder = new InputRecorder(inner, log);
            AssertSame(A, recorder.Consume(), "first");
            AssertSame(B, recorder.Consume(), "second");
            AssertSame(C, recorder.Consume(), "third");
            Assert.That(log.Count, Is.EqualTo(3));
            AssertSame(A, log[0], "log 0");
            AssertSame(B, log[1], "log 1");
            AssertSame(C, log[2], "log 2");
            Assert.That(inner.Consumes, Is.EqualTo(3));
        }

        [Test]
        public void Recorder_Poll_ForwardsToTheInnerSourceWithoutRecording()
        {
            var inner = new ScriptedSource(A);
            var log = new InputLog();
            var recorder = new InputRecorder(inner, log);
            recorder.Poll();
            recorder.Poll();
            Assert.That(inner.Polls, Is.EqualTo(2));
            Assert.That(log.Count, Is.Zero);
        }

        [Test]
        public void Recorder_Clear_ClearsTheInnerSourceAndStartsANewLog()
        {
            var inner = new ScriptedSource(A, B, C);
            var log = new InputLog();
            var recorder = new InputRecorder(inner, log);
            recorder.Consume();
            recorder.Consume();
            recorder.Clear();
            Assert.That(inner.Clears, Is.EqualTo(1));
            Assert.That(log.Count, Is.Zero);
            AssertSame(C, recorder.Consume(), "the inner script continues");
            Assert.That(log.Count, Is.EqualTo(1));
        }

        [Test]
        public void Recorder_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new InputRecorder(null, new InputLog()));
            Assert.Throws<ArgumentNullException>(() => new InputRecorder(new ScriptedSource(), null));
        }

        // ---- InputReplay ---------------------------------------------------------------------------

        [Test]
        public void Replay_Consume_ReturnsTheLogOneTickAtATimeThenIdleAndFinished()
        {
            var log = new InputLog();
            log.Add(A);
            log.Add(B);
            var replay = new InputReplay(log);
            Assert.That(replay.Finished, Is.False);
            AssertSame(A, replay.Consume(), "first");
            Assert.That(replay.Finished, Is.False);
            AssertSame(B, replay.Consume(), "second");
            Assert.That(replay.Finished, Is.True, "finished right after the last recorded tick");
            Assert.That(replay.Position, Is.EqualTo(2));
            AssertSame(default, replay.Consume(), "past the end");
            Assert.That(replay.Position, Is.EqualTo(2));
        }

        [Test]
        public void Replay_Clear_RewindsToTheFirstTick()
        {
            var log = new InputLog();
            log.Add(A);
            log.Add(B);
            var replay = new InputReplay(log);
            replay.Consume();
            replay.Consume();
            replay.Clear();
            Assert.That(replay.Finished, Is.False);
            Assert.That(replay.Position, Is.Zero);
            AssertSame(A, replay.Consume(), "starts again");
        }

        [Test]
        public void Replay_EmptyLog_IsFinishedAndIdle()
        {
            var replay = new InputReplay(new InputLog());
            Assert.That(replay.Finished, Is.True);
            AssertSame(default, replay.Consume(), "idle");
        }

        [Test]
        public void Replay_Poll_DoesNothing()
        {
            var log = new InputLog();
            log.Add(A);
            var replay = new InputReplay(log);
            Assert.DoesNotThrow(() => replay.Poll());
            Assert.That(replay.Position, Is.Zero);
        }

        [Test]
        public void Replay_NullLog_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new InputReplay(null));
        }

        // ---- record then replay --------------------------------------------------------------------

        [Test]
        public void RecordThenReplay_GivesTheSameInputsTickForTick()
        {
            var inner = new ScriptedSource(A, B, C, B, A);
            var log = new InputLog();
            var recorder = new InputRecorder(inner, log);
            var played = new List<TickInput>();
            for (int i = 0; i < 5; i++) played.Add(recorder.Consume());
            var replay = new InputReplay(log);
            for (int i = 0; i < 5; i++) AssertSame(played[i], replay.Consume(), "tick " + (i + 1));
            Assert.That(replay.Finished, Is.True);
        }
    }
}
