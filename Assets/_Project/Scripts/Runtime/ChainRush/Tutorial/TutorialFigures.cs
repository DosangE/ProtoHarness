using System;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;

namespace ProtoHarness.ChainRush.Tutorial
{
    // Briefing illustrations in a fixed local canvas. Only value types and cached caller styles are used per draw;
    // the target, off-screen arrow and gauge are the live HUD's own drawing functions.
    public static class TutorialFigures
    {
        public enum Kind { Goal, Drive, Turn, Reverse, Pole, Rope, Gauge, Boost }

        public static void Draw(Kind kind, Rect area, GUIStyle keys, GUIStyle small, GUIStyle item, Color goalColor)
        {
            NightUi.Block(area, NightUi.Background);
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(area.width / 1000f, area.height / 260f);
            GUI.matrix = previous * Matrix4x4.TRS(
                new Vector3(area.center.x - 500f * scale, area.center.y - 130f * scale, 0f),
                Quaternion.identity, new Vector3(scale, scale, 1f));
            switch (kind)
            {
                case Kind.Goal:
                    NightUi.Block(new Rect(258, 45, 12, 155), goalColor);
                    Ellipse(new Vector2(264, 204), 80, 20, 0, 360, goalColor);
                    ChainRushHud.DrawTarget(264, 112, 1.5f, goalColor, 0f);
                    ChainRushHud.DrawTarget(704, 128, 1f, goalColor, 0f);
                    ChainRushHud.DrawArrow(new Vector2(754, 128), Vector2.right, goalColor);
                    GUI.Label(new Rect(170, 226, 280, 28), "빛기둥 · 목표 표시", small);
                    GUI.Label(new Rect(600, 200, 330, 28), "화면 밖 목표의 방향", small);
                    break;
                case Kind.Drive:
                    Key(220, 100, 72, "↑", keys);
                    ChainRushHud.DrawArrow(new Vector2(348, 136), Vector2.right, NightUi.Accent);
                    Runner(new Vector2(450, 136));
                    Key(640, 100, 72, "↓", keys);
                    NightUi.Block(new Rect(770, 104, 8, 64), goalColor);
                    NightUi.Block(new Rect(794, 104, 8, 64), goalColor);
                    GUI.Label(new Rect(220, 200, 250, 28), "누르고 달리기", small);
                    GUI.Label(new Rect(640, 200, 250, 28), "속도 줄이기", small);
                    break;
                case Kind.Turn:
                    Key(130, 110, 72, "←", keys);
                    Key(800, 110, 72, "→", keys);
                    Ellipse(new Vector2(500, 210), 180, 150, 190, 270, NightUi.Accent);
                    Ellipse(new Vector2(500, 210), 180, 150, 270, 350, NightUi.Accent);
                    ChainRushHud.DrawArrow(new Vector2(322, 175), Vector2.down, NightUi.Accent);
                    ChainRushHud.DrawArrow(new Vector2(678, 175), Vector2.down, NightUi.Accent);
                    Runner(new Vector2(500, 202));
                    Line(new Vector2(500, 180), new Vector2(500, 70), NightUi.Border);
                    GUI.Label(new Rect(365, 230, 420, 28), "위에서 본 회전 경로", small);
                    break;
                case Kind.Reverse:
                    Key(150, 110, 72, "↓", keys);
                    Runner(new Vector2(470, 70));
                    Line(new Vector2(470, 95), new Vector2(470, 150), NightUi.Accent);
                    Ellipse(new Vector2(580, 150), 110, 80, 90, 180, NightUi.Accent);
                    ChainRushHud.DrawArrow(new Vector2(580, 230), Vector2.right, NightUi.Accent);
                    Key(750, 90, 72, "←", keys);
                    Key(840, 90, 72, "→", keys);
                    GUI.Label(new Rect(730, 190, 250, 28), "후진 중 조향 반대", small);
                    break;
                case Kind.Pole:
                    NightUi.Block(new Rect(328, 34, 14, 180), NightUi.Muted);
                    Ellipse(new Vector2(335, 214), 54, 14, 0, 360, NightUi.Muted);
                    ChainRushHud.DrawTarget(335, 118, 2f, NightUi.Accent, 0.08f);
                    Key(610, 90, 150, "SHIFT", keys);
                    GUI.Label(new Rect(590, 190, 320, 28), "누르고 유지하면 줄 연결", small);
                    break;
                case Kind.Rope:
                    Swing(new Vector2(330, 130));
                    Key(560, 34, 150, "SHIFT", keys);
                    Key(560, 136, 72, "←", keys);
                    Key(650, 136, 72, "→", keys);
                    ChainRushHud.DrawArrow(new Vector2(455, 130), Vector2.right, NightUi.Accent);
                    GUI.Label(new Rect(760, 120, 230, 64), "놓으면\n곧장 나가기", small);
                    GUI.Label(new Rect(560, 224, 360, 28), "← / → 줄 길이 조절", small);
                    break;
                case Kind.Gauge:
                    Swing(new Vector2(205, 130));
                    Key(325, 190, 150, "SHIFT", keys);
                    ChainRushHud.DrawArrow(new Vector2(380, 130), Vector2.right, NightUi.Accent);
                    ChainRushHud.DrawBoostGauge(510, 62, 0.65f, 0f, -1, small, item);
                    ChainRushHud.DrawBoostGauge(510, 154, 1f, 0f, -1, small, item);
                    GUI.Label(new Rect(510, 222, 430, 28), "스윙 → 게이지 → ▶▶ 한 칸", small);
                    break;
                case Kind.Boost:
                    Key(100, 80, 72, "↑", keys);
                    GUI.Label(new Rect(196, 100, 36, 40), "+", keys);
                    Key(252, 80, 130, "CTRL", keys);
                    ChainRushHud.DrawArrow(new Vector2(435, 116), Vector2.right, NightUi.Accent);
                    Runner(new Vector2(630, 100));
                    for (int i = 0; i < 4; i++)
                        NightUi.Block(new Rect(540, 70 + i * 20, 65, 3), NightUi.Accent);
                    ChainRushHud.DrawBoostGauge(510, 180, 1f, 0f, -1, small, item);
                    GUI.Label(new Rect(100, 194, 370, 28), "↑ 를 떼면 부스터 중단", small);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "TutorialFigures: unknown figure.");
            }
            GUI.matrix = previous;
        }

        private static void Key(float x, float y, float width, string text, GUIStyle style)
        {
            NightUi.Block(new Rect(x, y + 5, width, 60), NightUi.Border);
            NightUi.Block(new Rect(x, y, width, 60), NightUi.Selected);
            NightUi.Block(new Rect(x, y + 57, width, 3), NightUi.Accent);
            GUI.Label(new Rect(x, y, width, 60), text, style);
        }

        private static void Runner(Vector2 at)
        {
            NightUi.Block(new Rect(at.x - 10, at.y - 15, 20, 30), NightUi.Accent);
            ChainRushHud.DrawArrow(new Vector2(at.x, at.y - 25), Vector2.up, NightUi.Accent);
        }

        private static void Swing(Vector2 center)
        {
            Ellipse(center, 90, 90, 0, 360, NightUi.Border);
            Ellipse(center, 60, 60, 0, 270, NightUi.Accent);
            NightUi.Block(new Rect(center.x - 7, center.y - 7, 14, 14), NightUi.Muted);
            Line(center, center + new Vector2(90, 0), NightUi.Accent);
            Runner(center + new Vector2(90, 0));
            ChainRushHud.DrawTarget(center.x, center.y, 0.6f, NightUi.Accent, 0f);
            ChainRushHud.DrawArrow(center + new Vector2(0, -60), Vector2.right, NightUi.Accent);
        }

        private static void Ellipse(Vector2 center, float xRadius, float yRadius, float start, float end, Color color)
        {
            const int Segments = 48;
            Vector2 last = center + new Vector2(Mathf.Cos(start * Mathf.Deg2Rad) * xRadius, Mathf.Sin(start * Mathf.Deg2Rad) * yRadius);
            for (int i = 1; i <= Segments; i++)
            {
                float angle = Mathf.Lerp(start, end, (float)i / Segments) * Mathf.Deg2Rad;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * xRadius, Mathf.Sin(angle) * yRadius);
                Line(last, next, color);
                last = next;
            }
        }

        private static void Line(Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(from.x, from.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            NightUi.Block(new Rect(0, -1.5f, delta.magnitude, 3), color);
            GUI.matrix = previous;
        }
    }
}
