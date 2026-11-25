using System.Globalization;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // The running ranking strip on the right (exercise plan 10-2): two rows above, the player, one row below, the points to the
    // next row. Passing a row plays a short break-through (no text, no screen shake): the player's row pushes up, the passed
    // name plate cracks and scatters, the pieces gather one place lower, and the rank number pops. Several rows passed at
    // once play 0.2 s apart. While a hazard is close the effect waits (up to MaxHold) and the strip stays small.
    internal sealed class RankStripView
    {
        static readonly CultureInfo Num = CultureInfo.InvariantCulture;
        public const float Duration = .5f, Chain = .2f, MaxHold = 3f;
        public static readonly Rect Area = new Rect(1000, 236, 256, 4 * 34);
        const float RowHeight = 34;
        readonly HudSkin skin;
        float effectAt = -10, holdSince = -1;
        RankRace.Row breaking;
        bool effect;
        int rankPopAt;
        public System.Action Cracked;
        public RankStripView(HudSkin hudSkin) { skin = hudSkin; }
        public void Reset() { effect = false; effectAt = -10; holdSince = -1; }
        // Called every frame while running: starts the next queued overtake unless a hazard is close (then waits a little).
        public void Tick(RankRace race, bool hazardNear, float now)
        {
            if (race == null || !race.Loaded) return;
            bool busy = effect && now - effectAt < (race.Pending > 0 ? Chain : Duration);
            if (busy || race.Pending == 0) { if (!busy) effect = false; holdSince = -1; return; }
            if (hazardNear) { if (holdSince < 0) holdSince = now; if (now - holdSince < MaxHold) return; }
            if (race.TakeOvertake(out breaking)) { effect = true; effectAt = now; holdSince = -1; Cracked?.Invoke(); }
        }
        public void Draw(RankRace race, bool hazardNear, float now)
        {
            if (race == null || !race.Loaded || Event.current.type != EventType.Repaint) return;
            float alpha = hazardNear ? .55f : .85f;
            if (race.First)
            {
                var badge = new Rect(Area.xMax - 120, Area.y, 120, 44);
                skin.Card(badge, Palette.A(Palette.Gold, alpha));
                GUI.color = Palette.Outline; GUI.Label(badge, "1위", skin.Centered(26, FontStyle.Bold)); GUI.color = Color.white;
                DrawEffect(now);
                return;
            }
            var rows = race.Window();
            float t = effect ? Mathf.Clamp01((now - effectAt) / Duration) : 1f;
            // The player's row slides up from one place lower while the effect plays; close to the next row it leans upward.
            float lift = effect ? (1 - Ease(t)) * RowHeight : 0;
            float lean = race.Close ? 4 + Mathf.Sin(now * 9) * 2 : 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int index = rows[i];
                var r = new Rect(Area.x, Area.y + i * RowHeight, Area.width, RowHeight - 4);
                bool me = index < 0;
                if (me) r.y += lift - lean;
                // The row just above the player trembles when the player is close.
                bool next = !me && i + 1 < rows.Count && rows[i + 1] < 0;
                if (next && race.Close) r.x += Mathf.Sin(now * 60) * 1.5f;
                skin.Card(r, me ? Palette.A(Palette.Gold, alpha) : Palette.A(Palette.Ground, alpha * .85f));
                int rank = me ? race.Rank : index + (index < race.Rank - 1 ? 1 : 2);
                string name = me ? "나" : race.Others[index].Name;
                int score = me ? race.Score : race.Others[index].Score;
                GUI.color = me ? Palette.Outline : Palette.Cream;
                float pop = me && effect ? 1 + .45f * Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI) : 1;
                GUI.Label(new Rect(r.x + 8, r.y, 42, r.height), rank.ToString(Num), skin.Style(Mathf.RoundToInt(18 * pop), FontStyle.Bold));
                GUI.Label(new Rect(r.x + 50, r.y, 120, r.height), (me ? "▶ " : "") + name, skin.Style(17, me ? FontStyle.Bold : FontStyle.Normal));
                GUI.Label(new Rect(r.x + 150, r.y, r.width - 158, r.height), score.ToString("N0", Num), Right(17));
                if (next)
                {
                    GUI.color = Palette.Gold;
                    GUI.Label(new Rect(r.x - 96, r.y, 92, r.height), "+" + race.ToNext.ToString("N0", Num), Right(15));
                }
                GUI.color = Color.white;
            }
            DrawEffect(now);
        }
        // The passed name plate: cracks, scatters sideways, then the pieces gather one row below the player.
        void DrawEffect(float now)
        {
            if (!effect) return;
            float t = Mathf.Clamp01((now - effectAt) / Duration);
            var home = new Rect(Area.x, Area.y + RowHeight, Area.width, RowHeight - 4);
            var below = new Rect(Area.x, Area.y + RowHeight * 3, Area.width, RowHeight - 4);
            const int pieces = 8;
            for (int p = 0; p < pieces; p++)
            {
                float u = p / (float)(pieces - 1), seed = Mathf.Sin(p * 12.9898f) * 43758.5f; seed -= Mathf.Floor(seed);
                float scatter = t < .5f ? Ease(t * 2) : 1 - Ease((t - .5f) * 2);
                Vector2 from = new Vector2(home.x + u * home.width, home.center.y), to = new Vector2(below.x + u * below.width, below.center.y);
                Vector2 basePos = Vector2.Lerp(from, to, Ease(Mathf.Clamp01((t - .35f) / .65f)));
                Vector2 offset = new Vector2((u - .5f) * 90 + (seed - .5f) * 40, (seed - .5f) * 50) * scatter;
                float size = Mathf.Lerp(18, 10, scatter);
                GUI.color = new Color(1f, .45f + seed * .3f, .2f, 1 - t * .6f);
                GUI.DrawTexture(new Rect(basePos.x + offset.x - size / 2, basePos.y + offset.y - size / 2, size, size * .7f), Texture2D.whiteTexture);
            }
            // The crack line across the plate in the first instant.
            if (t < .25f) { GUI.color = new Color(1, 1, 1, 1 - t * 4); GUI.DrawTexture(new Rect(home.x + 20, home.center.y - 1, home.width - 40, 3), Texture2D.whiteTexture); }
            GUI.color = Color.white;
        }
        readonly System.Collections.Generic.Dictionary<int, GUIStyle> right = new System.Collections.Generic.Dictionary<int, GUIStyle>();
        GUIStyle Right(int size) { if (!right.TryGetValue(size, out var s)) right[size] = s = new GUIStyle(skin.Style(size, FontStyle.Bold)) { alignment = TextAnchor.MiddleRight }; return s; }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
