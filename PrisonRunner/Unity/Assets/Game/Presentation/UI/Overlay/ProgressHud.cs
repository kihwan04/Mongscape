using System.Globalization;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Reuse one content object instead of retaining every score/distance string in the skin's dictionary.
    internal sealed class ProgressHud
    {
        private readonly OverlaySkin skin;
        private readonly GUIContent content = new GUIContent();
        private readonly GUIContent distanceContent = new GUIContent();
        private readonly GUIContent bestContent = new GUIContent();
        private int shownScore = -1, shownMeters = -1, shownBest = -1;
        public ProgressHud(OverlaySkin overlaySkin) { skin = overlaySkin; }
        // savedBest: the member's best score for this play style, -1 for a guest (no best shown).
        public void Draw(RunSession session, int savedBest)
        {
            // The live score uses the same RunScore formula as the result and the ranking.
            int score = session.Score.Score;
            if (score != shownScore)
            {
                shownScore = score;
                content.text = score.ToString("N0", CultureInfo.InvariantCulture) + "점";
            }
            int meters = Mathf.FloorToInt(session.Score.Distance);
            int best = savedBest < 0 ? -1 : Mathf.Max(score, savedBest);
            if (meters != shownMeters || best != shownBest)
            {
                shownMeters = meters; shownBest = best;
                distanceContent.text = meters.ToString("N0", CultureInfo.InvariantCulture) + " m";
                bestContent.text = best < 0 ? "" : "최고 " + best.ToString("N0", CultureInfo.InvariantCulture) + "점";
            }
            // Keep the score/distance group together beside the pause control, away from the road.
            skin.Box(new Rect(870, 24, 300, 88), Palette.A(Palette.Ground, .82f));
            GUI.Label(new Rect(886, 28, 268, 48), content, skin.Style(36, true, TextAnchor.MiddleRight));
            // Distance on the left, the best to beat on the right of the same row.
            GUI.Label(new Rect(886, 77, 268, 26), distanceContent, skin.Style(16, true, best < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft));
            if (best >= 0) GUI.Label(new Rect(886, 77, 268, 26), bestContent, skin.Style(16, true, TextAnchor.MiddleRight));
        }
    }
}
