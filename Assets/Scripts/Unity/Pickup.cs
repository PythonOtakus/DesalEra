using CrazyAquarium.Game;
using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// A floating salvage pickup. Walks toward the raft when the player is close,
    /// which removes the fiddly part of gathering without removing the trip.
    /// </summary>
    public sealed class Pickup : MonoBehaviour
    {
        [SerializeField] private float collectRadius = 1.9f;
        [SerializeField] private float driftSpeed = 0.35f;
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobRate = 1.4f;

        public ResourceKind Kind { get; private set; }
        public int Amount { get; private set; } = 2;
        public Vector3 Origin { get; private set; }

        private GameBootstrap _world;
        private Transform _player;
        private float _respawnAt = -1f;
        private float _bobPhase;
        private Vector3 _basePosition;

        public void Initialise(GameBootstrap world, ResourceKind kind, Vector3 origin)
        {
            _world = world;
            Kind = kind;
            Origin = origin;
            _basePosition = origin;
            transform.localPosition = origin;
            _bobPhase = Random.value * Mathf.PI * 2f;

            // Water and food are rarer on the water than building stock, so the trip
            // out is worth making for them.
            Amount = kind == ResourceKind.Water || kind == ResourceKind.Food ? 3 : 2;
        }

        public void RegisterPlayer(Transform player) => _player = player;

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_respawnAt > 0f)
            {
                if (Time.time >= _respawnAt)
                {
                    _respawnAt = -1f;
                    gameObject.SetActive(true);
                    _basePosition = Origin;
                }
                return;
            }

            // Idle bob, so pickups read as floating rather than glued to the water.
            _bobPhase += Time.deltaTime * bobRate;
            Vector3 position = _basePosition;
            position.y += Mathf.Sin(_bobPhase) * bobAmplitude;

            if (_player != null)
            {
                Vector3 toPlayer = _player.position - position;
                toPlayer.y = 0f;
                float distance = toPlayer.magnitude;

                if (distance < 12f && distance > 0.01f)
                {
                    // Drift toward the player so collecting is "walk into it", not
                    // "line up the crosshair with a bobbing target".
                    position += toPlayer.normalized * (driftSpeed * Time.deltaTime * Mathf.Clamp01(12f - distance));
                }

                if (distance < collectRadius && _world != null)
                {
                    if (_world.Collect(this))
                    {
                        gameObject.SetActive(false);
                    }
                }
            }

            transform.localPosition = position;
        }

        public void Collect()
        {
            // The world reads Kind and Amount before calling this; hiding the object
            // is the only effect.
        }

        public void ScheduleRespawn(float seconds)
        {
            if (seconds <= 0f) return;
            _respawnAt = Time.time + seconds;
            gameObject.SetActive(false);
        }
    }
}
