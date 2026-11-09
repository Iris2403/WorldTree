using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Minimal allocation-free tween runner for the graybox (architecture §7.2): eased moves with an
    /// optional arc, and a scale "pop". Tweens live in a fixed struct array; starting a tween on a
    /// transform replaces any running tween of the same kind on it.
    /// </summary>
    public sealed class Tweener : MonoBehaviour
    {
        private enum Kind : byte { Move, Pop }

        private struct Tween
        {
            public bool Active;
            public Kind Kind;
            public Transform Target;
            public Vector3 From;
            public Vector3 To;
            public float Arc;
            public float Duration;
            public float Elapsed;
            public float BaseScale;
        }

        private readonly Tween[] _tweens = new Tween[128];

        /// <summary>Accessibility (GDD §10): when true, tweens jump to their end state instead of animating.</summary>
        public static bool ReducedMotion { get; set; }

        public void Move(Transform target, Vector3 to, float duration, float arcHeight = 0f)
        {
            Add(new Tween { Kind = Kind.Move, Target = target, From = target.position, To = to, Arc = arcHeight, Duration = duration });
        }

        public void Pop(Transform target, float baseScale, float duration = 0.22f)
        {
            Add(new Tween { Kind = Kind.Pop, Target = target, Duration = duration, BaseScale = baseScale });
        }

        /// <summary>Stops tweens on a transform (for example when it returns to a pool).</summary>
        public void Stop(Transform target)
        {
            for (int i = 0; i < _tweens.Length; i++)
                if (_tweens[i].Active && _tweens[i].Target == target) _tweens[i].Active = false;
        }

        // Not named Start: Unity treats any MonoBehaviour method called Start as the lifecycle message.
        private void Add(Tween tween)
        {
            if (ReducedMotion)
            {
                Apply(ref tween, 1f);
                return;
            }
            tween.Active = true;
            int free = -1;
            for (int i = 0; i < _tweens.Length; i++)
            {
                if (_tweens[i].Active && _tweens[i].Target == tween.Target && _tweens[i].Kind == tween.Kind)
                {
                    _tweens[i] = tween;
                    return;
                }
                if (!_tweens[i].Active && free < 0) free = i;
            }
            if (free >= 0) _tweens[free] = tween;
            else Apply(ref tween, 1f); // pool exhausted: jump to the end state rather than drop the change
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _tweens.Length; i++)
            {
                if (!_tweens[i].Active) continue;
                if (_tweens[i].Target == null)
                {
                    _tweens[i].Active = false;
                    continue;
                }
                _tweens[i].Elapsed += dt;
                float t = Mathf.Clamp01(_tweens[i].Elapsed / _tweens[i].Duration);
                Apply(ref _tweens[i], t);
                if (t >= 1f) _tweens[i].Active = false;
            }
        }

        private static void Apply(ref Tween tween, float t)
        {
            switch (tween.Kind)
            {
                case Kind.Move:
                    float e = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubic
                    var p = Vector3.LerpUnclamped(tween.From, tween.To, e);
                    p.y += tween.Arc * 4f * t * (1f - t);
                    tween.Target.position = p;
                    break;
                case Kind.Pop:
                    // 0 → 1.15 → 1.0 (GDD §3.6)
                    float s = t < 0.6f ? Mathf.Lerp(0f, 1.15f, EaseOut(t / 0.6f)) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
                    tween.Target.localScale = Vector3.one * (tween.BaseScale * s);
                    break;
            }
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    }
}
