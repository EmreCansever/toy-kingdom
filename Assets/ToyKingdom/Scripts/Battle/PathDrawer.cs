using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Bölük rotası çizme: işaretli bölgede parmağını bas, sürükleyerek rota çiz, bırakınca bölük yola çıkar.
    /// Sadece dokunursan bölük en yakın kapıya düz gider.
    /// </summary>
    public class PathDrawer : MonoBehaviour
    {
        const float PointSpacing = 0.22f;
        const int MaxPoints = 220;

        BattleManager bm;
        LineRenderer line;
        readonly List<Vector3> points = new List<Vector3>();
        bool drawing;
        bool hitCastle;

        void Start()
        {
            bm = BattleManager.Instance;
            var go = new GameObject("DrawLine");
            line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = bm.Field.SpriteMaterial;
            line.widthMultiplier = 0.09f;
            line.numCapVertices = 6;
            line.numCornerVertices = 3;
            line.sortingOrder = 29500;
            line.startColor = new Color(1f, 0.95f, 0.75f, 0.95f);
            line.endColor = new Color(1f, 0.8f, 0.3f, 0.95f);
            line.positionCount = 0;

            // bölge içinde başlayan basış rota çizer, dışarıda başlayan kamerayı kaydırır
            if (bm.Rig != null)
                bm.Rig.blockPress = screen =>
                    bm.State == BattleState.Playing && bm.Field.InDeployZone(PointerInput.ToWorld(bm.Cam, screen)) && bm.CanDeploy(out _);
        }

        void Update()
        {
            if (bm == null || bm.State != BattleState.Playing)
            {
                Cancel();
                return;
            }

            PointerInput.Refresh();
            Vector3 world = PointerInput.ToWorld(bm.Cam, PointerInput.Position);

            // bölge dışına dokunmak: ipucu (sürüklemek kamerayı kaydırır)
            if (bm.Rig != null && bm.Rig.TryConsumeTap(out Vector3 tapWorld) && !bm.Field.InDeployZone(tapWorld))
            {
                // bir bölüğe dokunmak onu seçer (emir vermek için)
                var sq = bm.SquadAt(tapWorld);
                if (sq != null) bm.SelectSquad(sq);
                else if (bm.SelectedSquad != null) bm.SelectSquad(null);
                else Say("Bölükleri kenarlardaki işaretli bölgelerden gönder. Yoldaki bir bölüğe dokunarak emir verebilirsin");
            }

            if (PointerInput.Down && !PointerInput.PressStartedOverUI)
            {
                if (!bm.Field.InDeployZone(world)) return;
                if (!bm.CanDeploy(out string reason))
                {
                    if (!string.IsNullOrEmpty(reason)) Say(reason);
                    return;
                }
                drawing = true;
                hitCastle = false;
                points.Clear();
                points.Add(bm.Field.Constrain(world));
                Redraw();
            }

            if (drawing && PointerInput.Held)
            {
                EdgeScroll();
                if (!hitCastle && points.Count < MaxPoints)
                {
                    Vector3 last = points[points.Count - 1];
                    if ((world - last).magnitude >= PointSpacing)
                    {
                        if (bm.Field.IsBlocked(world))
                        {
                            points.Add(bm.Field.Constrain(world));
                            hitCastle = true;
                        }
                        else points.Add(bm.Field.Constrain(world));
                        Redraw();
                    }
                }
            }

            if (drawing && (PointerInput.Up || !PointerInput.Held))
            {
                drawing = false;
                if (TotalLength() < 0.6f)
                {
                    // dokunuş: en yakın kapıya düz yürü
                    var gate = bm.Field.NearestGate(points[0]);
                    if (gate != null)
                    {
                        Vector3 start = points[0];
                        points.Clear();
                        points.Add(start);
                        points.Add(gate.FrontPoint);
                    }
                }
                if (points.Count >= 2) bm.Deploy(new List<Vector3>(points));
                points.Clear();
                Redraw();
            }
        }

        /// <summary>Rota çizerken parmak ekran kenarına gelirse kamera o yöne kayar.</summary>
        void EdgeScroll()
        {
            if (bm.Rig == null) return;
            Vector2 p = PointerInput.Position;
            float mx = Screen.width * 0.07f, my = Screen.height * 0.09f;
            Vector3 dir = Vector3.zero;
            if (p.x < mx) dir.x = -1f; else if (p.x > Screen.width - mx) dir.x = 1f;
            if (p.y < my + Screen.height * 0.16f) dir.y = -1f; else if (p.y > Screen.height - my) dir.y = 1f;
            if (dir != Vector3.zero) bm.Rig.Pan(dir * Time.deltaTime * bm.Cam.orthographicSize * 0.9f);
        }

        float TotalLength()
        {
            float len = 0f;
            for (int i = 1; i < points.Count; i++) len += Vector3.Distance(points[i - 1], points[i]);
            return len;
        }

        void Redraw()
        {
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i] + Vector3.up * 0.03f);
        }

        void Cancel()
        {
            if (!drawing) return;
            drawing = false;
            points.Clear();
            if (line != null) line.positionCount = 0;
        }

        float lastSay;
        void Say(string msg)
        {
            if (Time.time - lastSay < 0.5f) return;
            lastSay = Time.time;
            var hud = GetComponent<BattleHUD>();
            if (hud != null) hud.ShowHint(msg);
        }
    }
}
