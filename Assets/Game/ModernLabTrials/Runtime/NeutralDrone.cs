using System;
using FirstPerson;
using UnityEngine;

namespace ModernLabTrials
{
    // Neutral MCU-7A drone: patrols the lab, occasionally drifts toward the player,
    // and can be stood on. When the player boards a lift-capable drone it rises to
    // its lift anchor; jumping off applies an extra launch impulse.
    public sealed class NeutralDrone : MonoBehaviour
    {
        private enum State { Patrol, Approach, Lift, Hold, Return }

        [Header("Route")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Transform home;
        [SerializeField] private Transform liftTop;
        [SerializeField] private float patrolSpeed = 2.2f;
        [SerializeField] private float approachSpeed = 3.2f;
        [SerializeField] private float returnSpeed = 2.5f;
        [SerializeField] private float approachRange = 18f;
        [SerializeField] private float approachCooldown = 6f;
        [SerializeField] private float liftDuration = 2.2f;
        [SerializeField] private float holdSeconds = 2.6f;
        [SerializeField] private float launchBoost = 7.5f;
        [SerializeField] private float hoverAmplitude = .08f;

        [Header("Model")]
        [SerializeField] private BoxCollider deck;
        [SerializeField] private Transform rotorLeft;
        [SerializeField] private Transform rotorRight;
        [SerializeField] private string droneNumber = "MCU-7A / 01";

        private State state;
        private int patrolIndex;
        private float approachUntil;
        private float nextApproach;
        private float hoverPhase;
        private Vector3 lastPosition;
        private bool riderWasOnDeck;
        private bool rideRewarded;
        private FirstPersonMotor rider;
        private FirstPersonInput riderInput;
        private Camera viewCamera;
        private BoxCollider rideDeck;
        private TextMesh label;

        public event Action<NeutralDrone> RideStarted;
        public event Action<NeutralDrone> RideCompleted;
        public bool IsRiding { get; private set; }
        public string DroneNumber => droneNumber;

        public void Configure(Transform homePoint, Transform liftPoint, Transform[] route, string number)
        {
            home = homePoint;
            liftTop = liftPoint;
            patrolPoints = route;
            droneNumber = number;
        }

        public void ConfigureModel(BoxCollider deckCollider, Transform leftRotor, Transform rightRotor)
        {
            deck = deckCollider;
            rotorLeft = leftRotor;
            rotorRight = rightRotor;
        }

        private void Start()
        {
            if (home == null) home = transform;
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                GameObject p = new GameObject("Drone patrol point");
                p.transform.SetParent(transform.parent, false);
                p.transform.position = transform.position + transform.forward * 4f;
                patrolPoints = new[] { p.transform };
            }
            if (deck == null)
            {
                Transform found = transform.Find("Load-bearing collision deck");
                if (found != null) deck = found.GetComponent<BoxCollider>();
            }
            if (rotorLeft == null) rotorLeft = transform.Find("Rotor_Left · upright pivot");
            if (rotorRight == null) rotorRight = transform.Find("Rotor_Right · upright pivot");
            BuildRideDeck();
            rider = FindFirstObjectByType<FirstPersonMotor>();
            riderInput = rider != null ? rider.GetComponent<FirstPersonInput>() : null;
            viewCamera = Camera.main;
            if (viewCamera == null) viewCamera = FindFirstObjectByType<Camera>();
            BuildLabel();
            lastPosition = transform.position;
            state = State.Patrol;
        }

        private void BuildRideDeck()
        {
            GameObject go = new GameObject("Ride Deck");
            go.transform.SetParent(transform, false);
            Vector3 localCenter = new Vector3(0f, .26f, 0f);
            if (deck != null)
            {
                localCenter = transform.InverseTransformPoint(deck.bounds.center);
                localCenter.y = Mathf.Max(localCenter.y, .15f);
            }
            go.transform.localPosition = localCenter;
            rideDeck = go.AddComponent<BoxCollider>();
            rideDeck.size = new Vector3(1.65f, .14f, 1.85f);
        }

        private void BuildLabel()
        {
            GameObject go = new GameObject("Drone Number");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            label = go.AddComponent<TextMesh>();
            label.text = droneNumber;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = .045f;
            label.color = new Color(.45f, .95f, 1f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                label.font = font;
                MeshRenderer mesh = go.GetComponent<MeshRenderer>();
                if (mesh != null) mesh.sharedMaterial = font.material;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            RotateRotors();
            if (viewCamera != null && label != null)
                label.transform.rotation = viewCamera.transform.rotation;

            bool onDeck = IsRiderOnDeck();
            if (onDeck && !riderWasOnDeck)
            {
                riderWasOnDeck = true;
                rideRewarded = false;
                RideStarted?.Invoke(this);
                IsRiding = true;
                if (liftTop != null) StartCoroutine(LiftRoutine());
                else state = State.Patrol;
            }
            else if (!onDeck && riderWasOnDeck)
            {
                riderWasOnDeck = false;
                IsRiding = false;
                if (liftTop == null && !rideRewarded)
                {
                    rideRewarded = true;
                    RideCompleted?.Invoke(this);
                }
                state = State.Return;
            }

            if (state == State.Lift || state == State.Hold) return;
            switch (state)
            {
                case State.Patrol: Patrol(); break;
                case State.Approach: Approach(); break;
                case State.Return: ReturnHome(); break;
            }
        }

        private void Patrol()
        {
            if (rider != null && !riderWasOnDeck && Time.time >= nextApproach &&
                Vector3.Distance(transform.position, rider.transform.position) < approachRange)
            {
                nextApproach = Time.time + Mathf.Max(2f, approachCooldown);
                state = State.Approach;
                approachUntil = Time.time + 5f;
                return;
            }
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            Transform point = patrolPoints[patrolIndex % patrolPoints.Length];
            if (point == null) { patrolIndex++; return; }
            Vector3 target = point.position + Vector3.up * (Mathf.Sin(Time.time * 1.4f + hoverPhase) * hoverAmplitude);
            if (Vector3.Distance(transform.position, target) < .25f) { patrolIndex++; return; }
            MoveTowards(target, patrolSpeed);
        }

        private void Approach()
        {
            if (rider == null || Time.time > approachUntil || riderWasOnDeck)
            {
                state = State.Patrol;
                return;
            }
            Vector3 forward = rider.transform.forward;
            Vector3 target = rider.transform.position + forward * 1.4f + Vector3.up * .85f;
            MoveTowards(target, approachSpeed);
            if (Vector3.Distance(transform.position, target) < .45f)
            {
                // Hover close enough to board; wait for the player or time out.
                transform.position = Vector3.Lerp(transform.position, target, .04f);
            }
        }

        private void ReturnHome()
        {
            Vector3 target = home != null ? home.position : transform.position;
            if (Vector3.Distance(transform.position, target) < .2f)
            {
                state = State.Patrol;
                return;
            }
            MoveTowards(target, returnSpeed);
        }

        private void MoveTowards(Vector3 target, float speed)
        {
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude < .0001f) return;
            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            Vector3 delta = transform.position - before;
            if (delta.sqrMagnitude > .00001f)
            {
                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > .00001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(flat.normalized, Vector3.up), 4f * Time.deltaTime);
            }
        }

        private System.Collections.IEnumerator LiftRoutine()
        {
            state = State.Lift;
            Vector3 start = transform.position;
            Vector3 end = liftTop.position;
            float time = 0f;
            while (time < liftDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / liftDuration));
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }
            transform.position = end;
            state = State.Hold;
            if (!rideRewarded)
            {
                rideRewarded = true;
                RideCompleted?.Invoke(this);
            }
            yield return new WaitForSeconds(holdSeconds);
            state = State.Return;
        }

        private bool IsRiderOnDeck()
        {
            if (rider == null || rideDeck == null) return false;
            Bounds b = rideDeck.bounds;
            Vector3 p = rider.transform.position;
            return p.y >= b.min.y - .35f && p.y <= b.max.y + .35f &&
                   p.x >= b.min.x - .15f && p.x <= b.max.x + .15f &&
                   p.z >= b.min.z - .15f && p.z <= b.max.z + .15f;
        }

        private void LateUpdate()
        {
            if (rider == null || rideDeck == null) return;
            if (IsRiderOnDeck())
            {
                Vector3 delta = transform.position - lastPosition;
                if (delta.sqrMagnitude > .0000001f) rider.Body.Move(delta);
                if (riderInput != null && riderInput.JumpPressed)
                {
                    Vector3 v = rider.Velocity;
                    v.y = Mathf.Max(v.y, 4.5f) + launchBoost;
                    rider.Launch(v, .35f);
                }
            }
            lastPosition = transform.position;
        }

        private void RotateRotors()
        {
            if (rotorLeft != null) rotorLeft.Rotate(Vector3.up, 900f * Time.deltaTime, Space.Self);
            if (rotorRight != null) rotorRight.Rotate(Vector3.up, -900f * Time.deltaTime, Space.Self);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, .9f, 1f);
            if (patrolPoints != null)
                foreach (Transform p in patrolPoints)
                    if (p != null) Gizmos.DrawWireSphere(p.position, .25f);
            if (liftTop != null)
            {
                Gizmos.color = new Color(1f, .8f, .2f);
                Gizmos.DrawWireCube(liftTop.position, new Vector3(1.7f, .15f, 1.9f));
                Gizmos.DrawLine(transform.position, liftTop.position);
            }
        }
    }
}
