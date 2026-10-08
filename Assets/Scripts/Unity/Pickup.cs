using DesalEra.Game;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// A floating salvage pickup. Walks toward the raft when the player is close,
    /// which removes the fiddly part of gathering without removing the trip.
    /// </summary>
    public sealed class Pickup : MonoBehaviour
    {
        [SerializeField] private float collectRadius = 1.9f;
        [SerializeField] private float driftSpeed = 0.35f;
        [SerializeField] private float sinkM = 0.08f;

        public ResourceKind Kind { get; private set; }
        public int Amount { get; private set; } = 2;
        public Vector3 Origin { get; private set; }

        private GameBootstrap _world;
        private Transform _player;
        private float _respawnAt = -1f;
        private Vector3 _basePosition;

        public void Initialise(GameBootstrap world, ResourceKind kind, Vector3 origin)
        {
            _world = world;
            Kind = kind;
            Origin = origin;
            _basePosition = origin;
            transform.localPosition = origin;

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

            Vector3 position = _basePosition;
            if (_player != null)
            {
                Vector3 toPlayer = _player.position - position;
                toPlayer.y = 0f;
                float distance = toPlayer.magnitude;

                if (distance < 12f && distance > 0.01f)
                {
                    position += toPlayer.normalized * (driftSpeed * Time.deltaTime * Mathf.Clamp01(12f - distance));
                    _basePosition.x = position.x;
                    _basePosition.z = position.z;
                }

                if (distance < collectRadius && _world != null)
                {
                    if (_world.Collect(this))
                    {
                        gameObject.SetActive(false);
                    }
                }
            }

            _basePosition = position;
        }

        private void LateUpdate()
        {
            if (!gameObject.activeInHierarchy || _respawnAt > 0f) return;

            Vector3 position = _basePosition;
            Vector3 world = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(position.x, 0f, position.z))
                : new Vector3(position.x, 0f, position.z);
            float y = RaftState.WaterLevelY + SeaWave.HeightAt(world) - sinkM;
            if (transform.parent != null)
                position.y = transform.parent.InverseTransformPoint(new Vector3(world.x, y, world.z)).y;
            else
                position.y = y;

            transform.localPosition = position;

            Vector3 n = SeaWave.NormalAt(world);
            n = new Vector3(n.x * 3.2f, Mathf.Max(n.y, 0.35f), n.z * 3.2f).normalized;
            Quaternion target = Quaternion.FromToRotation(Vector3.up, n);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
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
