using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;

namespace ExtractionButtonMod.Services
{
    /// <summary>
    /// Evaluates the surrounding environment of an <see cref="ExtractionPoint"/> and repositions
    /// its <c>shopStation</c> confirmation pedestal when procedural level generation spawns walls
    /// or props overlapping the default location.
    /// </summary>
    public sealed class ShopPedestalPlacementService
    {
        // With bottom center at 0.54m and radius 0.24m, the lowest point of the check volume is 0.30m
        // above the base floor, preventing false-positive hits against floor meshes, rugs, or trims.
        private const float CapsuleBottomHeightOffset = 0.54f;
        private const float CapsuleTopHeightOffset = 1.30f;
        private const float OverlapRadius = 0.24f;
        private const float ClearanceCheckRadius = 0.40f;
        private const float FloorIgnoreHeightThreshold = 0.25f;

        private readonly ManualLogSource _logger;
        private readonly ConditionalWeakTable<ExtractionPoint, PlacementContext> _contexts =
            new ConditionalWeakTable<ExtractionPoint, PlacementContext>();
        private readonly Collider[] _overlapBuffer = new Collider[32];

        /// <summary>
        /// Initializes a new instance of the <see cref="ShopPedestalPlacementService"/> class.
        /// </summary>
        /// <param name="logger">The BepInEx logger instance.</param>
        public ShopPedestalPlacementService(ManualLogSource logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Records the initial prefab transform of <c>shopStation</c> and performs an initial placement evaluation.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void InitializePlacement(ExtractionPoint instance)
        {
            if (instance == null || instance.shopStation == null)
            {
                return;
            }

            PlacementContext context = GetOrCreateContext(instance);
            EvaluateAndApplyBestPlacement(instance, context, isPostLevelGenPass: SemiFunc.LevelGenDone());
        }

        /// <summary>
        /// Ensures that placement is re-evaluated once procedural level generation (<see cref="SemiFunc.LevelGenDone"/>)
        /// has completed and all surrounding walls and room props are spawned.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void EnsurePlacementResolved(ExtractionPoint instance)
        {
            if (instance == null || instance.shopStation == null)
            {
                return;
            }

            PlacementContext context = GetOrCreateContext(instance);
            if (context.ResolvedAfterLevelGen)
            {
                return;
            }

            if (!SemiFunc.LevelGenDone())
            {
                return;
            }

            EvaluateAndApplyBestPlacement(instance, context, isPostLevelGenPass: true);
        }

        /// <summary>
        /// Re-evaluates the pedestal placement when the extraction point is activated, in case dynamic room props
        /// spawned or settled onto the pedestal spot prior to activation.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void RefreshPlacementOnActivation(ExtractionPoint instance)
        {
            if (instance == null || instance.shopStation == null)
            {
                return;
            }

            PlacementContext context = GetOrCreateContext(instance);
            if (context.ResolvedOnActivation)
            {
                return;
            }

            context.ResolvedOnActivation = true;
            EvaluateAndApplyBestPlacement(instance, context, isPostLevelGenPass: true);
        }

        private PlacementContext GetOrCreateContext(ExtractionPoint instance)
        {
            if (_contexts.TryGetValue(instance, out PlacementContext existing))
            {
                return existing;
            }

            Transform pointTransform = instance.transform;
            Transform stationTransform = instance.shopStation;

            Vector3 originalLocalPos = pointTransform.InverseTransformPoint(stationTransform.position);
            Quaternion originalLocalRot = Quaternion.Inverse(pointTransform.rotation) * stationTransform.rotation;

            Vector3 buttonOffsetInStation = instance.shopButton != null
                ? stationTransform.InverseTransformPoint(instance.shopButton.position)
                : new Vector3(0f, 1.0f, 0f);

            HashSet<Collider> ignoredSelfColliders = CollectSelfColliders(instance);

            var created = new PlacementContext(
                originalLocalPos,
                originalLocalRot,
                buttonOffsetInStation,
                ignoredSelfColliders);

            _contexts.Add(instance, created);
            return created;
        }

        private void EvaluateAndApplyBestPlacement(
            ExtractionPoint instance,
            PlacementContext context,
            bool isPostLevelGenPass)
        {
            if (instance.shopStation == null)
            {
                return;
            }

            if (isPostLevelGenPass)
            {
                context.ResolvedAfterLevelGen = true;
            }

            Physics.SyncTransforms();

            CandidateSpot[] candidates = BuildCandidates(instance, context);
            int bestIndex = 0;
            float bestPenalty = float.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                float penalty = EvaluateCandidatePenalty(instance, context, candidates[i]);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestIndex = i;
                }

                // If the candidate is completely clear of overlaps and wall proximity, select it immediately.
                if (penalty <= 0.001f)
                {
                    break;
                }
            }

            CandidateSpot chosen = candidates[bestIndex];
            ApplyCandidate(instance, chosen);

            if (bestIndex > 0 && isPostLevelGenPass)
            {
                _logger.LogInfo(
                    $"Pedestal de confirmación reposicionado en '{instance.name}' -> [{chosen.Label}] " +
                    $"(penalización: {bestPenalty:F1}) para evitar superposición con objetos del nivel.");
            }
        }

        private static CandidateSpot[] BuildCandidates(ExtractionPoint instance, PlacementContext context)
        {
            Vector3 origPos = context.OriginalPointLocalPosition;
            Quaternion origRot = context.OriginalPointLocalRotation;

            float signX = Mathf.Abs(origPos.x) >= 0.05f ? Mathf.Sign(origPos.x) : 1f;
            float absX = Mathf.Max(Mathf.Abs(origPos.x), 1.25f);
            float frontSignZ = DetermineFrontDirectionSign(instance, origPos);
            float frontBaseZ = DetermineFrontEdgeZ(instance, origPos, frontSignZ);

            Quaternion mirroredSideRot = MirrorRotationAcrossX(origRot);

            // Corner positions sit in front of the extraction point's left/right front corners
            // so the pedestal clears side walls/props while keeping the center extraction ramp open.
            float cornerX = signX * Mathf.Clamp(absX * 0.86f, 1.05f, 1.48f);
            float frontCornerZ = frontBaseZ + (frontSignZ * 0.35f);

            float outerCornerX = signX * Mathf.Clamp(absX * 0.72f, 0.90f, 1.25f);
            float outerFrontCornerZ = frontBaseZ + (frontSignZ * 0.72f);

            // Slightly angle corner pedestals inward/forward so the button remains comfortable to press.
            Quaternion frontCornerOriginalRot = origRot * Quaternion.Euler(0f, -signX * frontSignZ * 25f, 0f);
            Quaternion frontCornerOppositeRot = mirroredSideRot * Quaternion.Euler(0f, signX * frontSignZ * 25f, 0f);

            return new[]
            {
                new CandidateSpot(
                    "Lado original",
                    origPos,
                    origRot),
                new CandidateSpot(
                    "Lado opuesto",
                    new Vector3(-origPos.x, origPos.y, origPos.z),
                    mirroredSideRot),
                new CandidateSpot(
                    "Esquina frontal (lado original)",
                    new Vector3(cornerX, origPos.y, frontCornerZ),
                    frontCornerOriginalRot),
                new CandidateSpot(
                    "Esquina frontal (lado opuesto)",
                    new Vector3(-cornerX, origPos.y, frontCornerZ),
                    frontCornerOppositeRot),
                new CandidateSpot(
                    "Frente de esquina exterior (lado original)",
                    new Vector3(outerCornerX, origPos.y, outerFrontCornerZ),
                    frontCornerOriginalRot),
                new CandidateSpot(
                    "Frente de esquina exterior (lado opuesto)",
                    new Vector3(-outerCornerX, origPos.y, outerFrontCornerZ),
                    frontCornerOppositeRot),
            };
        }

        private float EvaluateCandidatePenalty(
            ExtractionPoint instance,
            PlacementContext context,
            CandidateSpot candidate)
        {
            Transform pointTransform = instance.transform;
            Vector3 worldBasePos = pointTransform.TransformPoint(candidate.PointLocalPosition);
            Quaternion worldRot = pointTransform.rotation * candidate.PointLocalRotation;
            Vector3 up = pointTransform.up;

            Vector3 capsuleBottom = worldBasePos + (up * CapsuleBottomHeightOffset);
            Vector3 capsuleTop = worldBasePos + (up * CapsuleTopHeightOffset);
            Vector3 buttonWorldPos = worldBasePos + (worldRot * context.ButtonOffsetInStation);

            float floorMaxY = worldBasePos.y + FloorIgnoreHeightThreshold;
            float penalty = 0f;

            // 1. Direct overlap check on the pedestal body volume.
            int directHits = Physics.OverlapCapsuleNonAlloc(
                capsuleBottom,
                capsuleTop,
                OverlapRadius,
                _overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < directHits; i++)
            {
                Collider col = _overlapBuffer[i];
                if (!IsRelevantObstruction(col, context.IgnoredSelfColliders, capsuleBottom, floorMaxY))
                {
                    continue;
                }

                penalty += 100f;
            }

            // 2. Direct overlap check around the interactive button top.
            int buttonHits = Physics.OverlapSphereNonAlloc(
                buttonWorldPos,
                OverlapRadius * 0.95f,
                _overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < buttonHits; i++)
            {
                Collider col = _overlapBuffer[i];
                if (!IsRelevantObstruction(col, context.IgnoredSelfColliders, buttonWorldPos, floorMaxY))
                {
                    continue;
                }

                penalty += 120f;
            }

            // 3. Line-of-sight / wall-clipping check from the front-center of the extraction point to the button.
            float frontSignZ = DetermineFrontDirectionSign(instance, context.OriginalPointLocalPosition);
            float frontEdgeZ = DetermineFrontEdgeZ(instance, context.OriginalPointLocalPosition, frontSignZ);
            Vector3 frontReferenceLocal = new Vector3(
                0f,
                context.OriginalPointLocalPosition.y + 0.90f,
                frontEdgeZ + (frontSignZ * 0.25f));
            Vector3 frontReferenceWorld = pointTransform.TransformPoint(frontReferenceLocal);

            Vector3 toButton = buttonWorldPos - frontReferenceWorld;
            float distanceToButton = toButton.magnitude;
            if (distanceToButton > 0.15f)
            {
                RaycastHit[] hits = Physics.RaycastAll(
                    frontReferenceWorld,
                    toButton / distanceToButton,
                    distanceToButton,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i].collider;
                    if (!IsRelevantObstruction(col, context.IgnoredSelfColliders, buttonWorldPos, floorMaxY))
                    {
                        continue;
                    }

                    // A solid collider blocks access between the front of the extraction point and the button.
                    penalty += 250f;
                }
            }

            // 4. Soft clearance check so we prefer spots with breathing room over spots grazing a wall.
            int clearanceHits = Physics.OverlapCapsuleNonAlloc(
                capsuleBottom,
                capsuleTop,
                ClearanceCheckRadius,
                _overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < clearanceHits; i++)
            {
                Collider col = _overlapBuffer[i];
                if (!IsRelevantObstruction(col, context.IgnoredSelfColliders, capsuleBottom, floorMaxY))
                {
                    continue;
                }

                penalty += 15f;
            }

            return penalty;
        }

        private static bool IsRelevantObstruction(
            Collider? collider,
            HashSet<Collider> ignoredSelfColliders,
            Vector3 samplePoint,
            float floorMaxY)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                return false;
            }

            if (ignoredSelfColliders.Contains(collider))
            {
                return false;
            }

            // Ignore player characters so a nearby player standing next to the extraction point
            // does not cause false-positive obstacle detection.
            if (collider.GetComponentInParent<PlayerAvatar>() != null ||
                collider.GetComponentInParent<PlayerController>() != null)
            {
                return false;
            }

            // Ignore floor/ground geometry beneath the pedestal base.
            Bounds bounds = collider.bounds;
            if (bounds.max.y <= floorMaxY)
            {
                return false;
            }

            if (SupportsClosestPoint(collider))
            {
                Vector3 closest = collider.ClosestPoint(samplePoint);
                if (closest.y <= floorMaxY && bounds.size.y < 0.35f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SupportsClosestPoint(Collider collider)
        {
            if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            {
                return true;
            }

            if (collider is MeshCollider meshCollider)
            {
                return meshCollider.convex;
            }

            return false;
        }

        private static void ApplyCandidate(ExtractionPoint instance, CandidateSpot candidate)
        {
            if (instance.shopStation == null)
            {
                return;
            }

            Transform pointTransform = instance.transform;
            Transform stationTransform = instance.shopStation;

            stationTransform.position = pointTransform.TransformPoint(candidate.PointLocalPosition);
            stationTransform.rotation = pointTransform.rotation * candidate.PointLocalRotation;
        }

        private static float DetermineFrontDirectionSign(ExtractionPoint instance, Vector3 originalStationLocalPos)
        {
            if (instance.ramp != null)
            {
                Vector3 rampLocal = instance.transform.InverseTransformPoint(instance.ramp.position);
                if (Mathf.Abs(rampLocal.z) > 0.05f)
                {
                    return Mathf.Sign(rampLocal.z);
                }
            }

            if (Mathf.Abs(originalStationLocalPos.z) > 0.05f)
            {
                return Mathf.Sign(originalStationLocalPos.z);
            }

            return 1f;
        }

        private static float DetermineFrontEdgeZ(
            ExtractionPoint instance,
            Vector3 originalStationLocalPos,
            float frontSignZ)
        {
            float candidateFrontAbsZ = Mathf.Abs(originalStationLocalPos.z) + 0.55f;

            if (instance.ramp != null)
            {
                Vector3 rampLocal = instance.transform.InverseTransformPoint(instance.ramp.position);
                candidateFrontAbsZ = Mathf.Max(candidateFrontAbsZ, Mathf.Abs(rampLocal.z));
            }

            return frontSignZ * candidateFrontAbsZ;
        }

        private static Quaternion MirrorRotationAcrossX(Quaternion localRotation)
        {
            Vector3 forward = localRotation * Vector3.forward;
            Vector3 up = localRotation * Vector3.up;

            Vector3 mirroredForward = new Vector3(-forward.x, forward.y, forward.z);
            Vector3 mirroredUp = new Vector3(-up.x, up.y, up.z);

            if (mirroredForward.sqrMagnitude < 0.001f)
            {
                return localRotation;
            }

            return Quaternion.LookRotation(mirroredForward.normalized, mirroredUp.normalized);
        }

        private static HashSet<Collider> CollectSelfColliders(ExtractionPoint instance)
        {
            var result = new HashSet<Collider>();
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    result.Add(colliders[i]);
                }
            }

            return result;
        }

        private sealed class PlacementContext
        {
            public PlacementContext(
                Vector3 originalPointLocalPosition,
                Quaternion originalPointLocalRotation,
                Vector3 buttonOffsetInStation,
                HashSet<Collider> ignoredSelfColliders)
            {
                OriginalPointLocalPosition = originalPointLocalPosition;
                OriginalPointLocalRotation = originalPointLocalRotation;
                ButtonOffsetInStation = buttonOffsetInStation;
                IgnoredSelfColliders = ignoredSelfColliders;
            }

            public Vector3 OriginalPointLocalPosition { get; }

            public Quaternion OriginalPointLocalRotation { get; }

            public Vector3 ButtonOffsetInStation { get; }

            public HashSet<Collider> IgnoredSelfColliders { get; }

            public bool ResolvedAfterLevelGen { get; set; }

            public bool ResolvedOnActivation { get; set; }
        }

        private readonly struct CandidateSpot
        {
            public CandidateSpot(string label, Vector3 pointLocalPosition, Quaternion pointLocalRotation)
            {
                Label = label;
                PointLocalPosition = pointLocalPosition;
                PointLocalRotation = pointLocalRotation;
            }

            public string Label { get; }

            public Vector3 PointLocalPosition { get; }

            public Quaternion PointLocalRotation { get; }
        }
    }
}
