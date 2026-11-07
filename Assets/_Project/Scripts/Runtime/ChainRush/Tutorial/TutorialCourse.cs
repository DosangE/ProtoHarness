using System;
using System.Collections.Generic;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.ChainRush.Tutorial
{
    // The tutorial (user 2026-11-06): a mission map, not a race, so it has no start line, laps or finish. The map is a
    // wide round floor with one swing pole in the middle, and each stage is a list of goals given one at a time. A stage
    // starts from the spawn, so any of them can be played or restarted alone.
    // The world is laid on a ring centerline round the pole (TrackDefinition.Ring): the runner spawns on the ring at the
    // origin facing +z, so the pole stands RingRadius to its right. Everything that asks the track still works on the
    // floor: a pole's angle is taken from the ring's direction, which is square to the pole everywhere, and a wall's
    // normal runs across it (measured 2026-11-06, DECISIONS "튜토리얼"). RaceAssembler extends the motor's pole reach
    // by FloorRadius, so the pole hooks from anywhere on the floor; distance sets the swing's initial rope length.
    public static class TutorialCourse
    {
        public const string Title = "TUTORIAL";
        public const string TrackName = "Tutorial";
        public const float RingRadius = 30f;
        // The guarded island the pole stands on; the floor has a hole there, as a road has an inner edge.
        public const float IslandRadius = 3f;
        public const float FloorRadius = 150f;
        // Slower than a race's default: the turn radius does not scale, so the same corner is easier.
        public const SpeedClass Speed = SpeedClass.Normal;
        // Below this a runner has stopped (Stop goal), in m/s.
        public const float StoppedSpeed = 1f;
        // Beyond this it is moving backward (Reverse goal), in m/s: a U-turn into the zone does not count.
        public const float BackingSpeed = 0.5f;

        public static readonly Vector3 Pole = new Vector3(RingRadius, 0f, 0f);

        public enum GoalKind
        {
            // In the zone.
            Reach,
            // In the zone and stopped.
            Stop,
            // In the zone and moving backward.
            Reverse,
            // Swung Amount degrees round the pole on one hook.
            Swing,
            // Taken Amount metres of rope in on one hook.
            Rope,
            // Let a swing go.
            Release,
            // Holding Amount gauge slots.
            Gauge,
            // A booster is running.
            Boost,
        }

        // One thing to do. A zone goal has a Point and a Radius; the others have neither.
        public readonly struct Goal
        {
            public Goal(GoalKind kind, string text, Vector3 point, float radius, float amount)
            {
                Kind = kind;
                Text = text;
                Point = point;
                Radius = radius;
                Amount = amount;
            }

            public GoalKind Kind { get; }
            public string Text { get; }
            public Vector3 Point { get; }
            public float Radius { get; }
            public float Amount { get; }
            public bool HasZone => Radius > 0f;
        }

        // One briefing page, drawn from code so the illustrations share the live HUD's symbols.
        public readonly struct Page
        {
            public Page(string title, string body, TutorialFigures.Kind figure)
            {
                Title = title;
                Body = body;
                Figure = figure;
            }

            public string Title { get; }
            public string Body { get; }
            public TutorialFigures.Kind Figure { get; }
        }

        // A stage: its name, the lobby's description, briefing pages and goals in order. Gauge is earned by swinging.
        public readonly struct Stage
        {
            public Stage(string name, string summary, Page[] pages, params Goal[] goals)
            {
                Name = name;
                Summary = summary;
                Pages = pages;
                Goals = goals;
            }

            public string Name { get; }
            public string Summary { get; }
            public IReadOnlyList<Page> Pages { get; }
            public IReadOnlyList<Goal> Goals { get; }
        }

        // What a goal is judged on, read from the race once a tick (TutorialDirector).
        public struct Reading
        {
            public Vector3 Position;
            public float Speed;
            public float ForwardSpeed;
            public float SwungDegrees;
            public float RopeTaken;
            public bool Released;
            public float Gauge;
            public bool Boosting;
        }

        private static Goal Zone(GoalKind kind, float x, float z, float radius, string text) =>
            new Goal(kind, text, new Vector3(x, 0f, z), radius, 0f);

        private static Goal Action(GoalKind kind, float amount, string text) =>
            new Goal(kind, text, Vector3.zero, 0f, amount);

        // Zones are at least two turn radii apart (the circuit's 25 m), and a stage's first one is reachable from the
        // spawn without backing up, except the reverse stage's.
        private static readonly Stage[] All =
        {
            new Stage("전진 · 정지", "↑ 로 달리고 ↓ 로 멈춥니다.\n목표 지점 안에 정확히 멈춰 보세요.", new[]
                {
                    new Page("빛기둥이 목표입니다", "금색 빛기둥과 사각형 표시가 목표 지점을 알려줍니다.\n화면 밖에 있으면 화살표가 그 방향을 가리킵니다.", TutorialFigures.Kind.Goal),
                    new Page("달리고, 멈추기", "↑ 를 누르고 있으면 앞으로 달립니다.\n↑ 를 떼고 ↓ 로 브레이크를 걸어 목표 원 안에 멈추세요.", TutorialFigures.Kind.Drive),
                },
                Zone(GoalKind.Reach, 0f, 60f, 10f, "↑ 를 누르고 있으면 달립니다.\n앞에 보이는 빛기둥까지 달리세요."),
                Zone(GoalKind.Reach, 0f, 100f, 10f, "좋습니다. 다음 빛기둥도 그대로 지나가세요."),
                Zone(GoalKind.Stop, 0f, 125f, 10f, "↑ 를 떼고 ↓ 를 누르면 브레이크입니다.\n빛기둥의 원 안에 멈추세요.")),
            new Stage("좌회전 · 우회전", "← / → 로 방향을 틉니다.\n빠를수록 크게 돌아가니 미리 꺾으세요.", new[]
                {
                    new Page("좌우로 방향 틀기", "↑ 로 달리면서 ← / → 로 방향을 틉니다.\n빛기둥을 향해 왼쪽과 오른쪽으로 돌아보세요.", TutorialFigures.Kind.Turn),
                    new Page("빠를수록 미리 꺾으세요", "속도가 빠를수록 크게 돌아갑니다.\n목표를 지나치지 않도록 미리 방향을 꺾으세요.", TutorialFigures.Kind.Turn),
                },
                Zone(GoalKind.Reach, -45f, 50f, 10f, "← 를 눌러 왼쪽으로 꺾으세요.\n왼쪽 빛기둥까지 가면 됩니다."),
                Zone(GoalKind.Reach, -20f, 105f, 10f, "이번에는 → 를 눌러 오른쪽으로 꺾으세요."),
                Zone(GoalKind.Reach, 50f, 95f, 10f, "→ 로 계속 돌아 다음 빛기둥으로."),
                Zone(GoalKind.Reach, 75f, 45f, 10f, "크게 돌아 아래쪽 빛기둥으로 가세요.\n미리 꺾어야 지나치지 않습니다."),
                Zone(GoalKind.Reach, 125f, 20f, 10f, "마지막입니다. ← 로 왼쪽 빛기둥까지.")),
            new Stage("후진", "↓ 를 유지하면 뒤로 갑니다.\n후진 중에는 조향이 반대로 듭니다.", new[]
                {
                    new Page("↓ 를 유지하면 후진", "달리는 중 ↓ 를 누르면 먼저 속도를 줄입니다.\n멈춘 뒤에도 계속 누르고 있으면 뒤로 갑니다.", TutorialFigures.Kind.Reverse),
                    new Page("후진 중에는 조향이 반대", "후진할 때는 조향이 반대로 듭니다.\n↓ 를 유지하며 ← / → 로 뒤쪽 목표를 향해 가세요.", TutorialFigures.Kind.Reverse),
                },
                Zone(GoalKind.Reverse, 0f, -22f, 6f, "↓ 를 계속 누르면 뒤로 갑니다.\n돌지 말고 그대로 뒤쪽 빛기둥까지."),
                Zone(GoalKind.Reverse, -14f, -50f, 7f, "↓ 를 유지한 채 ← 를 누르면\n뒤가 왼쪽으로 갑니다. 왼쪽 뒤 빛기둥으로.")),
            new Stage("봉 스윙", "가운데 봉에 줄을 걸고 돕니다.\n코너를 도는 이 게임의 핵심 기술입니다.", new[]
                {
                    new Page("바닥 어디서나 봉에 줄 걸기", "사각형 표시와 화살표가 봉의 방향을 알려줍니다.\n바닥 어디서나 ↑ 로 달리면서 SHIFT 를 누르고 유지하세요.\n멀리서 걸면 크게, 가까이서 걸면 작게 돕니다.", TutorialFigures.Kind.Pole),
                    new Page("줄 길이를 조절하고 놓기", "스윙 중 ← / → 는 줄 길이를 조절합니다.\n봉 쪽으로 감고 반대쪽으로 늘립니다.\nSHIFT 를 놓으면 그 속도로 곧장 나갑니다.", TutorialFigures.Kind.Rope),
                },
                Action(GoalKind.Swing, 180f, "바닥 어디서나 왼쪽 SHIFT 를 누르고 유지하면\n줄이 걸립니다. ↑ 를 누른 채 반 바퀴 도세요."),
                Action(GoalKind.Rope, 5f, "스윙 중에는 ← / → 가 줄 길이입니다.\n봉 쪽 방향키로 줄을 5 m 감으세요."),
                Action(GoalKind.Release, 0f, "SHIFT 를 떼면 그 속도로 곧장 나갑니다.\n놓아 보세요.")),
            new Stage("부스터", "게이지는 스윙으로 채웁니다.\n한 칸을 모아 부스터로 가속하세요.", new[]
                {
                    new Page("게이지는 스윙으로 찹니다", "먼저 SHIFT 로 봉에 줄을 걸고 돌아 게이지를 채우세요.\n한 칸이 차면 옆의 ▶▶ 칸이 켜집니다.", TutorialFigures.Kind.Gauge),
                    new Page("모은 게이지로 부스터", "SHIFT 를 놓은 뒤 ↑ 를 누른 채 CTRL 로 사용합니다.\n부스터는 게이지 한 칸을 쓰며, ↑ 를 떼면 끊깁니다.", TutorialFigures.Kind.Boost),
                },
                Action(GoalKind.Gauge, 1f, "SHIFT 로 봉에 줄을 걸어 도세요.\n스윙으로 게이지 한 칸을 채우세요."),
                Action(GoalKind.Release, 0f, "SHIFT 를 떼서 스윙을 끝내세요."),
                Action(GoalKind.Boost, 0f, "↑ 를 누른 채 왼쪽 CTRL 로 부스터를 쓰세요."),
                Zone(GoalKind.Reach, 0f, 130f, 10f, "↑ 를 떼면 부스터가 끊깁니다.\n누른 채로 빛기둥까지 달리세요.")),
            new Stage("종합", "바깥 목표를 지나 그대로 스윙하고\n부스터로 달려 마지막 목표 안에 멈춥니다.", new[]
                {
                    new Page("지금까지 배운 것을 이어서", "먼저 봉에서 떨어진 바깥 빛기둥으로 달립니다.\n그 자리에서 줄을 걸고 스윙으로 게이지 한 칸을 채우세요.", TutorialFigures.Kind.Goal),
                    new Page("놓고, 가속하고, 멈추기", "줄을 놓고 ↑ 와 CTRL 로 부스터를 사용하세요.\n마지막 목표 원 안에서 ↓ 로 멈추면 모든 과제 완료입니다.", TutorialFigures.Kind.Boost),
                },
                Zone(GoalKind.Reach, 0f, 65f, 10f, "먼저 앞쪽 바깥 빛기둥까지 달리세요.\n그다음 그 자리에서 줄을 걸어 스윙합니다."),
                Action(GoalKind.Gauge, 1f, "봉에 SHIFT 로 줄을 걸어 돌면 게이지가 찹니다.\n한 칸을 채우세요."),
                Action(GoalKind.Release, 0f, "SHIFT 를 떼서 스윙을 끝내세요."),
                Action(GoalKind.Boost, 0f, "↑ 를 누른 채 CTRL 로 부스터를 쓰세요."),
                Zone(GoalKind.Stop, 30f, -110f, 12f, "마지막 빛기둥의 원 안에 멈추세요.\n↑ 를 떼고 ↓ 로 브레이크를 걸면 됩니다.")),
        };

        public static IReadOnlyList<Stage> Stages => All;

        // Guidance only: spending a slot before the Boost goal must leave a visible way to earn another.
        private static bool NeedsGauge(in Goal goal, float gauge, bool boosting) =>
            goal.Kind == GoalKind.Boost && gauge < 1f && !boosting;

        public static string GoalText(in Goal goal, float gauge, bool boosting) => NeedsGauge(goal, gauge, boosting)
            ? "게이지가 없습니다.\n봉에서 스윙해 다시 채우세요." : goal.Text;

        public static bool IsMet(in Goal goal, in Reading now)
        {
            switch (goal.Kind)
            {
                case GoalKind.Reach: return InZone(goal, now.Position);
                case GoalKind.Stop: return InZone(goal, now.Position) && now.Speed < StoppedSpeed;
                case GoalKind.Reverse: return InZone(goal, now.Position) && now.ForwardSpeed < -BackingSpeed;
                case GoalKind.Swing: return now.SwungDegrees >= goal.Amount;
                case GoalKind.Rope: return now.RopeTaken >= goal.Amount;
                case GoalKind.Release: return now.Released;
                case GoalKind.Gauge: return now.Gauge >= goal.Amount;
                case GoalKind.Boost: return now.Boosting;
                default: throw new ArgumentOutOfRangeException(nameof(goal), goal.Kind, "TutorialCourse: unknown goal.");
            }
        }

        // Measured level, so a jump over a zone still counts.
        private static bool InZone(in Goal goal, Vector3 position)
        {
            Vector3 away = position - goal.Point;
            away.y = 0f;
            return away.sqrMagnitude <= goal.Radius * goal.Radius;
        }

        // The tutorial from the given stage on. The ring is made here, not kept as an asset: nothing else races it and it
        // needs no saved values. The race scene destroys it with the race (RaceAssembler).
        public static RaceSetup Setup(RunnerProfile runner, TrackLook look, int firstStage)
        {
            if (firstStage < 0 || firstStage >= All.Length)
                throw new ArgumentOutOfRangeException(nameof(firstStage), firstStage, $"TutorialCourse: stages run 0 to {All.Length - 1}.");
            return new RaceSetup(TrackDefinition.Ring(TrackName, RingRadius), Title, "", RaceMode.Tutorial, Speed, runner, look, firstStage);
        }
    }
}
