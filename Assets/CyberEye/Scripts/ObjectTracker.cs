using System.Collections.Generic;
using UnityEngine;

// M5: greedy per-class IoU tracker giving stable IDs across (sporadic) detections, so a glow/dossier locks
// onto a target and persists for holdSeconds after it was last seen (suits the ~keyframe detection cadence).
public class ObjectTracker
{
    public class Track
    {
        public int id, classId;
        public string label;
        public float conf;
        public Rect box;          // normalized 0..1, top-left origin (camera image space)
        public float lastSeen, firstSeen;
    }

    readonly List<Track> _tracks = new();
    int _next = 1;
    public IReadOnlyList<Track> Tracks => _tracks;
    public float iouMatch = 0.3f;
    public float holdSeconds = 2.5f;

    static float IoU(Rect a, Rect b)
    {
        float x1 = Mathf.Max(a.xMin, b.xMin), y1 = Mathf.Max(a.yMin, b.yMin);
        float x2 = Mathf.Min(a.xMax, b.xMax), y2 = Mathf.Min(a.yMax, b.yMax);
        float iw = Mathf.Max(0, x2 - x1), ih = Mathf.Max(0, y2 - y1);
        float inter = iw * ih, uni = a.width * a.height + b.width * b.height - inter;
        return uni <= 0 ? 0 : inter / uni;
    }

    public void Update(IReadOnlyList<Detector.Detection> dets, float time)
    {
        var matched = new bool[_tracks.Count];
        var used = new bool[dets.Count];
        for (int di = 0; di < dets.Count; di++)
        {
            var d = dets[di];
            var db = new Rect(d.x, d.y, d.w, d.h);
            int best = -1; float bi = iouMatch;
            for (int ti = 0; ti < _tracks.Count; ti++)
            {
                if (matched[ti] || _tracks[ti].classId != d.classId) continue;
                float io = IoU(_tracks[ti].box, db);
                if (io > bi) { bi = io; best = ti; }
            }
            if (best >= 0)
            {
                var t = _tracks[best];
                t.box = new Rect(Mathf.Lerp(t.box.x, db.x, 0.5f), Mathf.Lerp(t.box.y, db.y, 0.5f),
                                 Mathf.Lerp(t.box.width, db.width, 0.5f), Mathf.Lerp(t.box.height, db.height, 0.5f));
                t.conf = d.confidence; t.lastSeen = time; matched[best] = true; used[di] = true;
            }
        }
        for (int di = 0; di < dets.Count; di++)
        {
            if (used[di]) continue;
            var d = dets[di];
            _tracks.Add(new Track { id = _next++, classId = d.classId, label = d.label, conf = d.confidence,
                                    box = new Rect(d.x, d.y, d.w, d.h), lastSeen = time, firstSeen = time });
        }
    }

    public void Age(float time)
    {
        for (int i = _tracks.Count - 1; i >= 0; i--)
            if (time - _tracks[i].lastSeen > holdSeconds) _tracks.RemoveAt(i);
    }
}
