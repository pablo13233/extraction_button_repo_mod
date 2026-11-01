using System;
using System.Collections.Generic;
using BepInEx.Logging;
using ExtractionButtonMod.Configuration;
using ExtractionButtonMod.Internal;
using Photon.Pun;
using UnityEngine;

namespace ExtractionButtonMod.Services
{
    /// <summary>
    /// Coordinates manual confirmation of extractions on <see cref="ExtractionPoint"/> instances
    /// in standard gameplay levels, replicating the Shop's confirmation button workflow.
    /// </summary>
    public sealed class ExtractionConfirmationService
    {
        private readonly ModConfig _config;
        private readonly ManualLogSource _logger;
        private readonly ShopPedestalPlacementService _placementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractionConfirmationService"/> class.
        /// </summary>
        /// <param name="config">The active mod configuration.</param>
        /// <param name="logger">The BepInEx logger instance.</param>
        public ExtractionConfirmationService(ModConfig config, ManualLogSource logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _placementService = new ShopPedestalPlacementService(_logger);
        }

        /// <summary>
        /// Handles <c>ExtractionPoint.ShopSetup()</c>. When the Shop pedestal button mode is enabled in
        /// standard levels, prevents <c>shopStation</c> from being destroyed and initializes smart placement.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        /// <returns><see langword="true"/> to execute original <c>ShopSetup</c>; <see langword="false"/> to skip it.</returns>
        public bool TryOverrideShopSetup(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || !_config.UsesShopPedestalButton)
            {
                return true;
            }

            if (SemiFunc.RunIsShop())
            {
                return true;
            }

            ExtractionPointAccess.IsShopRef(instance) = false;
            if (instance.shopStation != null)
            {
                instance.shopStation.gameObject.SetActive(true);
                _placementService.InitializePlacement(instance);
            }

            return false;
        }

        /// <summary>
        /// Prevents <c>ExtractionPoint.HaulChecker()</c> from automatically transitioning to
        /// <see cref="ExtractionPoint.State.Success"/> when the haul quota is reached in standard levels.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void PreventAutomaticExtraction(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || ExtractionPointAccess.IsShopRef(instance))
            {
                return;
            }

            // Keeping successDelay above 0 prevents HaulChecker from auto-calling StateSet(State.Success)
            // while preserving all haul calculation, sound effects, and UI text updates.
            ExtractionPointAccess.SuccessDelayRef(instance) = 10f;
        }

        /// <summary>
        /// Ensures the first extraction point's automatic activation when leaving the truck only triggers
        /// when the extraction point is still in <see cref="ExtractionPoint.State.Idle"/>.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        /// <returns><see langword="true"/> if the extraction point is idle; otherwise <see langword="false"/>.</returns>
        public bool ShouldAllowAutoActivateFromTruck(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value)
            {
                return true;
            }

            return ExtractionPointAccess.StateIs(instance, ExtractionPoint.State.Idle);
        }

        /// <summary>
        /// Re-enables and illuminates the main button on the <see cref="ExtractionPoint"/> once the tube
        /// has fully raised in <see cref="ExtractionPoint.State.Active"/> so players can press it to confirm extraction,
        /// and verifies pedestal placement on activation.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void UpdateMainButtonInActiveState(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || ExtractionPointAccess.IsShopRef(instance))
            {
                return;
            }

            if (!ExtractionPointAccess.StateIs(instance, ExtractionPoint.State.Active))
            {
                return;
            }

            if (_config.UsesShopPedestalButton)
            {
                _placementService.RefreshPlacementOnActivation(instance);
            }

            if (!_config.UsesExtractionPointButton)
            {
                return;
            }

            // Wait until the extraction tube has finished raising to the ceiling (tubeSlamDownEval >= 1f).
            if (ExtractionPointAccess.TubeSlamDownEvalRef(instance) < 1f)
            {
                return;
            }

            if (ExtractionPointAccess.ButtonDenyCooldownRef(instance) > 0f)
            {
                return;
            }

            if (!ExtractionPointAccess.ButtonActiveRef(instance))
            {
                ExtractionPointAccess.ButtonPressedRef(instance) = false;
                ExtractionPointAccess.ButtonPressEvalRef(instance) = 0f;

                if (instance.button != null)
                {
                    instance.button.transform.localPosition = ExtractionPointAccess.ButtonOriginalPositionRef(instance);
                    Material originalMaterial = ExtractionPointAccess.ButtonOriginalMaterialRef(instance);
                    if (originalMaterial != null)
                    {
                        instance.button.material = originalMaterial;
                    }
                }

                if (instance.buttonLight != null)
                {
                    instance.buttonLight.enabled = true;
                }

                if (instance.buttonGrabObject != null)
                {
                    instance.buttonGrabObject.enabled = true;
                }

                ExtractionPointAccess.ButtonActiveRef(instance) = true;
            }
        }

        /// <summary>
        /// Handles pressing the main button (<c>OnClick</c>) on the <see cref="ExtractionPoint"/>.
        /// When in <see cref="ExtractionPoint.State.Idle"/>, delegates to vanilla activation logic.
        /// When in <see cref="ExtractionPoint.State.Active"/>, validates the haul quota and confirms or rejects the extraction.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        /// <returns><see langword="true"/> to run vanilla <c>OnClick</c>; <see langword="false"/> if handled.</returns>
        public bool TryHandleMainButtonClick(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || ExtractionPointAccess.IsShopRef(instance) || !_config.UsesExtractionPointButton)
            {
                return true;
            }

            if (instance.isLocked || ExtractionPointAccess.StateIs(instance, ExtractionPoint.State.Idle))
            {
                return true;
            }

            if (!ExtractionPointAccess.StateIs(instance, ExtractionPoint.State.Active))
            {
                return false;
            }

            ReleaseStaticGrabObjectGrabbers(instance.buttonGrabObject);

            if (ExtractionPointAccess.TubeSlamDownEvalRef(instance) < 1f)
            {
                return false;
            }

            EvaluateAndTriggerConfirmation(instance);
            return false;
        }

        /// <summary>
        /// Handles pressing the Shop pedestal button (<c>OnShopClick</c>) when enabled in standard levels.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        /// <returns><see langword="true"/> to run vanilla <c>OnShopClick</c>; <see langword="false"/> if handled.</returns>
        public bool TryHandleShopButtonClick(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || ExtractionPointAccess.IsShopRef(instance))
            {
                return true;
            }

            if (instance.shopStation != null)
            {
                StaticGrabObject? shopGrabObject = instance.shopStation.GetComponentInChildren<StaticGrabObject>();
                ReleaseStaticGrabObjectGrabbers(shopGrabObject);
            }

            if (!ExtractionPointAccess.StateIs(instance, ExtractionPoint.State.Active) ||
                ExtractionPointAccess.TubeSlamDownEvalRef(instance) < 1f)
            {
                return false;
            }

            EvaluateAndTriggerConfirmation(instance);
            return false;
        }

        /// <summary>
        /// Synchronizes button push visuals and audio across all clients when <see cref="ExtractionPoint.State.Success"/>
        /// or <see cref="ExtractionPoint.State.Cancel"/> starts.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void TriggerButtonPushVisuals(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || ExtractionPointAccess.IsShopRef(instance))
            {
                return;
            }

            if (_config.UsesExtractionPointButton && ExtractionPointAccess.ButtonActiveRef(instance))
            {
                ReleaseStaticGrabObjectGrabbers(instance.buttonGrabObject);
                ExtractionPointAccess.ButtonToggle(instance, false);

                ExtractionPointAccess.ButtonPressedRef(instance) = true;
                ExtractionPointAccess.ButtonPressEvalRef(instance) = 0f;

                if (instance.soundButton != null && instance.button != null)
                {
                    instance.soundButton.Play(instance.button.transform.position, 1f, 1f, 1f, 1f);
                }
            }

            if (_config.UsesShopPedestalButton &&
                instance.shopButton != null &&
                !ExtractionPointAccess.ShopButtonAnimationRef(instance))
            {
                instance.shopButton.localScale = new Vector3(1f, 0.1f, 1f);
                if (instance.soundButton != null)
                {
                    instance.soundButton.Play(instance.shopButton.position, 1f, 1f, 1f, 1f);
                }

                ExtractionPointAccess.ShopButtonAnimationEvalRef(instance) = 0f;
                ExtractionPointAccess.ShopButtonAnimationRef(instance) = true;
            }
        }

        /// <summary>
        /// Animates the physical extraction machine button press during <see cref="ExtractionPoint.State.Success"/>
        /// and <see cref="ExtractionPoint.State.Cancel"/> (where <c>StateActive</c>'s animation loop is inactive),
        /// and resolves pedestal placement once procedural level generation finishes.
        /// </summary>
        /// <param name="instance">The <see cref="ExtractionPoint"/> instance.</param>
        public void UpdateConfirmationButtonAnimation(ExtractionPoint instance)
        {
            if (!_config.Enabled.Value || ExtractionPointAccess.IsShopRef(instance))
            {
                return;
            }

            if (_config.UsesShopPedestalButton)
            {
                _placementService.EnsurePlacementResolved(instance);
            }

            if (ExtractionPointAccess.StateIs(instance, ExtractionPoint.State.Active))
            {
                return;
            }

            if (!ExtractionPointAccess.ButtonPressedRef(instance) || instance.button == null)
            {
                return;
            }

            ref float pressEval = ref ExtractionPointAccess.ButtonPressEvalRef(instance);
            if (pressEval >= 1f)
            {
                ExtractionPointAccess.ButtonPressedRef(instance) = false;
                instance.button.transform.localPosition = ExtractionPointAccess.ButtonOriginalPositionRef(instance);
                return;
            }

            pressEval = Mathf.Min(1f, pressEval + (8f * Time.deltaTime));
            float curveOffset = instance.buttonPressAnimationCurve != null
                ? instance.buttonPressAnimationCurve.Evaluate(pressEval)
                : 0f;

            Vector3 originalPos = ExtractionPointAccess.ButtonOriginalPositionRef(instance);
            instance.button.transform.localPosition = new Vector3(
                originalPos.x,
                originalPos.y,
                originalPos.z - (0.1f * curveOffset));
        }

        private void EvaluateAndTriggerConfirmation(ExtractionPoint instance)
        {
            ExtractionPointAccess.HaulInternalStatsUpdate(instance);

            if (SemiFunc.GameEventIsHalloween())
            {
                ExtractionHalloweenTower? halloweenTower = ExtractionPointAccess.HalloweenTowerRef(instance);
                if (halloweenTower != null && ExtractionPointAccess.IsHalloweenTowerWaiting(halloweenTower))
                {
                    ExtractionPointAccess.StateSet(instance, ExtractionPoint.State.Cancel);
                    return;
                }
            }

            int currentHaul = ExtractionPointAccess.HaulCurrentRef(instance);
            int goalHaul = instance.haulGoal;
            bool goalFetched = ExtractionPointAccess.HaulGoalFetchedRef(instance);

            if (goalFetched && currentHaul >= goalHaul)
            {
                _logger.LogInfo($"Extraction confirmed via button! Haul: ${currentHaul} / ${goalHaul}.");
                ExtractionPointAccess.StateSet(instance, ExtractionPoint.State.Success);
            }
            else
            {
                _logger.LogInfo($"Extraction rejected (insufficient haul): ${currentHaul} / ${goalHaul}.");
                ExtractionPointAccess.StateSet(instance, ExtractionPoint.State.Cancel);
            }
        }

        private static void ReleaseStaticGrabObjectGrabbers(StaticGrabObject? grabObject)
        {
            if (grabObject == null || grabObject.playerGrabbing == null || grabObject.playerGrabbing.Count == 0)
            {
                return;
            }

            PhotonView? grabPhotonView = ExtractionPointAccess.StaticGrabPhotonViewRef(grabObject);
            if (grabPhotonView == null)
            {
                return;
            }

            int viewId = grabPhotonView.ViewID;
            List<PhysGrabber> activeGrabbers = new List<PhysGrabber>(grabObject.playerGrabbing);

            for (int i = 0; i < activeGrabbers.Count; i++)
            {
                PhysGrabber grabber = activeGrabbers[i];
                if (grabber == null)
                {
                    continue;
                }

                if (!SemiFunc.IsMultiplayer())
                {
                    grabber.ReleaseObject(viewId, 0.1f);
                }
                else if (SemiFunc.IsMasterClient() && grabber.photonView != null && grabber.photonView.Owner != null)
                {
                    grabber.OverrideGrabRelease(viewId, 0.25f);
                }
            }
        }
    }
}

