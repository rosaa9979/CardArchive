using UnityEngine;
using TcgEngine.Client;

namespace TcgEngine.FX
{
    /// <summary>Presentation only. The existing ability resolves damage on the server.</summary>
    public sealed class NonomiBarrageFX : MonoBehaviour
    {
        [Header("Fan barrage")]
        [SerializeField, Range(1, 12)] private int wave_count = 7;
        [SerializeField, Range(3, 40)] private int bullets_per_wave = 21;
        [SerializeField, Range(30f, 150f)] private float spread_angle = 112f;
        [SerializeField, Min(0.02f)] private float wave_interval = 0.052f;
        [SerializeField, Min(0.1f)] private float flight_duration = 0.38f;
        [SerializeField, Range(0f, 0.3f)] private float source_height = 0.035f;
        [SerializeField] private Color tracer_color = new Color(1f, 0.76f, 0.22f, 1f);

        [Header("Prefab particles")]
        [SerializeField] private ParticleSystem tracers;
        [SerializeField] private ParticleSystem muzzle;
        [SerializeField] private ParticleSystem sparks;

        private float elapsed;
        private int next_wave;
        private float view_scale = 1f;
        private bool playing;

        public float Duration => (wave_count - 1) * wave_interval + flight_duration + 0.18f;

        private void OnEnable()
        {
            if (Application.isPlaying)
                Play();
        }

        public void Play(Camera view = null)
        {
            if (!Prepare(view))
                return;
            playing = true;
            EmitThrough(0f);
        }

        private bool Prepare(Camera view)
        {
            if (tracers == null || muzzle == null || sparks == null)
                return false;
            if (view == null)
                view = GameCamera.GetCamera();
            if (view == null)
                view = Camera.main;
            if (view == null)
                return false;

            // A camera-relative plane keeps the origin at the bottom centre at any aspect ratio.
            float depth = Mathf.Clamp(10f, view.nearClipPlane + 0.1f, view.farClipPlane - 0.1f);
            Vector3 bottom = view.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth));
            Vector3 top = view.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));
            view_scale = Vector3.Distance(bottom, top) / 10.8f;
            transform.SetPositionAndRotation(view.ViewportToWorldPoint(new Vector3(0.5f, source_height, depth)), view.transform.rotation);
            transform.localScale = Vector3.one;
            elapsed = 0f;
            next_wave = 0;
            ResetSystem(tracers);
            ResetSystem(muzzle);
            ResetSystem(sparks);
            return true;
        }

        private static void ResetSystem(ParticleSystem system)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(true);
        }

        private void Update()
        {
            if (!playing)
                return;
            elapsed += Time.deltaTime;
            EmitThrough(elapsed);
            if (elapsed >= Duration)
            {
                playing = false;
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            playing = false;
            if (tracers != null) tracers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (muzzle != null) muzzle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void EmitThrough(float time)
        {
            while (next_wave < wave_count && time >= next_wave * wave_interval)
            {
                // Account for a late frame instead of piling overdue rounds up at the muzzle.
                EmitWave(next_wave, time - next_wave * wave_interval);
                next_wave++;
            }
        }

        private void EmitWave(int wave, float lateness)
        {
            for (int i = 0; i < bullets_per_wave; i++)
            {
                float variation = Noise(wave * 71 + i * 13);
                float angle = Mathf.Lerp(-spread_angle * 0.5f, spread_angle * 0.5f,
                    (i + 0.5f) / bullets_per_wave) + (variation - 0.5f) * 3.5f;
                Vector3 direction = Quaternion.Euler(0f, 0f, -angle) * Vector3.up;
                float speed = Mathf.Lerp(30f, 39f, Noise(i * 29 + wave * 31)) * view_scale;
                float lifetime = flight_duration * Mathf.Lerp(0.88f, 1.12f, variation);
                if (lateness >= lifetime)
                    continue;
                var round = new ParticleSystem.EmitParams
                {
                    position = direction * (0.12f * view_scale + speed * lateness),
                    velocity = direction * speed,
                    startLifetime = lifetime - lateness,
                    startSize3D = new Vector3(Mathf.Lerp(0.07f, 0.115f, variation), Mathf.Lerp(0.8f, 1.55f, variation), 1f) * view_scale,
                    rotation3D = new Vector3(0f, 0f, -angle),
                    startColor = Color.Lerp(tracer_color, new Color(1f, 0.95f, 0.65f, 1f), variation * 0.65f),
                    randomSeed = (uint)(wave * bullets_per_wave + i + 1)
                };
                tracers.Emit(round, 1);
            }

            if (lateness > 0.12f)
                return;
            EmitFlash(muzzle, Vector3.zero, Vector3.zero, new Vector3(1.15f, 0.65f, 1f), 0.11f, 0f,
                new Color(1f, 0.58f, 0.09f, 0.6f));
            for (int i = 0; i < 3; i++)
                EmitFlash(muzzle, Vector3.up * 0.18f, Vector3.zero, new Vector3(0.14f, 1.0f, 1f), 0.085f,
                    (i - 1) * 48f + wave * 9f, new Color(1f, 0.91f, 0.58f, 1f));

            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(-78f, 78f, Noise(wave * 43 + i * 17));
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                EmitFlash(sparks, direction * 0.12f, direction * Mathf.Lerp(3f, 9f, Noise(i * 47 + wave)),
                    new Vector3(0.045f, 0.22f, 1f), 0.23f, angle, new Color(1f, 0.68f, 0.12f, 0.85f));
            }
        }

        private void EmitFlash(ParticleSystem system, Vector3 position, Vector3 velocity, Vector3 size,
            float lifetime, float rotation, Color tint)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = position * view_scale,
                velocity = velocity * view_scale,
                startSize3D = size * view_scale,
                startLifetime = lifetime,
                rotation3D = new Vector3(0f, 0f, rotation),
                startColor = tint
            };
            system.Emit(particle, 1);
        }

        // Local deterministic variation must not consume the game's random sequence.
        private static float Noise(int seed)
        {
            uint n = unchecked((uint)(seed + 1) * 747796405u + 2891336453u);
            n = ((n >> (int)((n >> 28) + 4)) ^ n) * 277803737u;
            return ((n >> 22) ^ n) / (float)uint.MaxValue;
        }

#if UNITY_EDITOR
        // Used by the editor preview and visual verification without running combat.
        public void SamplePreview(float time, Camera view)
        {
            if (!Prepare(view)) return;
            playing = false;
            const float step = 1f / 120f;
            EmitThrough(0f);
            for (float t = 0f; t < time;)
            {
                float dt = Mathf.Min(step, time - t);
                tracers.Simulate(dt, true, false, false);
                muzzle.Simulate(dt, true, false, false);
                sparks.Simulate(dt, true, false, false);
                t += dt;
                EmitThrough(t);
            }
            tracers.Pause(true);
            muzzle.Pause(true);
            sparks.Pause(true);
        }
#endif
    }
}
