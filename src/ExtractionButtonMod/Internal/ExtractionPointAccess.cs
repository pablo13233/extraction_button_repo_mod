using System;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace ExtractionButtonMod.Internal
{
    /// <summary>
    /// Provides cached, zero-overhead <see cref="AccessTools"/> delegates for accessing non-public
    /// fields and methods on <see cref="ExtractionPoint"/> and related game classes.
    /// </summary>
    internal static class ExtractionPointAccess
    {
        public static readonly AccessTools.FieldRef<ExtractionPoint, bool> IsShopRef =
            AccessTools.FieldRefAccess<ExtractionPoint, bool>("isShop");

        public static readonly AccessTools.FieldRef<ExtractionPoint, int> HaulCurrentRef =
            AccessTools.FieldRefAccess<ExtractionPoint, int>("haulCurrent");

        public static readonly AccessTools.FieldRef<ExtractionPoint, bool> HaulGoalFetchedRef =
            AccessTools.FieldRefAccess<ExtractionPoint, bool>("haulGoalFetched");

        public static readonly AccessTools.FieldRef<ExtractionPoint, float> SuccessDelayRef =
            AccessTools.FieldRefAccess<ExtractionPoint, float>("successDelay");

        public static readonly AccessTools.FieldRef<ExtractionPoint, float> TubeSlamDownEvalRef =
            AccessTools.FieldRefAccess<ExtractionPoint, float>("tubeSlamDownEval");

        public static readonly AccessTools.FieldRef<ExtractionPoint, Material> ButtonOriginalMaterialRef =
            AccessTools.FieldRefAccess<ExtractionPoint, Material>("buttonOriginalMaterial");

        public static readonly AccessTools.FieldRef<ExtractionPoint, bool> ButtonActiveRef =
            AccessTools.FieldRefAccess<ExtractionPoint, bool>("buttonActive");

        public static readonly AccessTools.FieldRef<ExtractionPoint, float> ButtonDenyCooldownRef =
            AccessTools.FieldRefAccess<ExtractionPoint, float>("buttonDenyCooldown");

        public static readonly AccessTools.FieldRef<ExtractionPoint, bool> ButtonPressedRef =
            AccessTools.FieldRefAccess<ExtractionPoint, bool>("buttonPressed");

        public static readonly AccessTools.FieldRef<ExtractionPoint, float> ButtonPressEvalRef =
            AccessTools.FieldRefAccess<ExtractionPoint, float>("buttonPressEval");

        public static readonly AccessTools.FieldRef<ExtractionPoint, Vector3> ButtonOriginalPositionRef =
            AccessTools.FieldRefAccess<ExtractionPoint, Vector3>("buttonOriginalPosition");

        public static readonly AccessTools.FieldRef<ExtractionPoint, bool> ShopButtonAnimationRef =
            AccessTools.FieldRefAccess<ExtractionPoint, bool>("shopButtonAnimation");

        public static readonly AccessTools.FieldRef<ExtractionPoint, float> ShopButtonAnimationEvalRef =
            AccessTools.FieldRefAccess<ExtractionPoint, float>("shopButtonAnimationEval");

        public static readonly AccessTools.FieldRef<ExtractionPoint, ExtractionHalloweenTower?> HalloweenTowerRef =
            AccessTools.FieldRefAccess<ExtractionPoint, ExtractionHalloweenTower?>("halloweenTower");

        public static readonly AccessTools.FieldRef<StaticGrabObject, PhotonView?> StaticGrabPhotonViewRef =
            AccessTools.FieldRefAccess<StaticGrabObject, PhotonView?>("photonView");

        private static readonly Action<ExtractionPoint> HaulInternalStatsUpdateDelegate =
            AccessTools.MethodDelegate<Action<ExtractionPoint>>(
                AccessTools.Method(typeof(ExtractionPoint), "HaulInternalStatsUpdate"));

        private static readonly Action<ExtractionPoint, ExtractionPoint.State> StateSetDelegate =
            AccessTools.MethodDelegate<Action<ExtractionPoint, ExtractionPoint.State>>(
                AccessTools.Method(typeof(ExtractionPoint), "StateSet", new[] { typeof(ExtractionPoint.State) }));

        private static readonly Func<ExtractionPoint, ExtractionPoint.State, bool> StateIsDelegate =
            AccessTools.MethodDelegate<Func<ExtractionPoint, ExtractionPoint.State, bool>>(
                AccessTools.Method(typeof(ExtractionPoint), "StateIs", new[] { typeof(ExtractionPoint.State) }));

        private static readonly Action<ExtractionPoint, bool> ButtonToggleDelegate =
            AccessTools.MethodDelegate<Action<ExtractionPoint, bool>>(
                AccessTools.Method(typeof(ExtractionPoint), "ButtonToggle", new[] { typeof(bool) }));

        private static readonly Func<ExtractionHalloweenTower, bool> HalloweenTowerExtractionWaitDelegate =
            AccessTools.MethodDelegate<Func<ExtractionHalloweenTower, bool>>(
                AccessTools.Method(typeof(ExtractionHalloweenTower), "ExtractionWait"));

        /// <summary>
        /// Invokes <c>ExtractionPoint.HaulInternalStatsUpdate()</c> to refresh <c>haulCurrent</c>.
        /// </summary>
        public static void HaulInternalStatsUpdate(ExtractionPoint instance) =>
            HaulInternalStatsUpdateDelegate(instance);

        /// <summary>
        /// Invokes <c>ExtractionPoint.StateSet(state)</c>, synchronizing state via RPC in multiplayer.
        /// </summary>
        public static void StateSet(ExtractionPoint instance, ExtractionPoint.State state) =>
            StateSetDelegate(instance, state);

        /// <summary>
        /// Invokes <c>ExtractionPoint.StateIs(state)</c> to check the current state of the extraction point.
        /// </summary>
        public static bool StateIs(ExtractionPoint instance, ExtractionPoint.State state) =>
            StateIsDelegate(instance, state);

        /// <summary>
        /// Invokes <c>ExtractionPoint.ButtonToggle(active)</c>.
        /// </summary>
        public static void ButtonToggle(ExtractionPoint instance, bool active) =>
            ButtonToggleDelegate(instance, active);

        /// <summary>
        /// Invokes <c>ExtractionHalloweenTower.ExtractionWait()</c>.
        /// </summary>
        public static bool IsHalloweenTowerWaiting(ExtractionHalloweenTower tower) =>
            HalloweenTowerExtractionWaitDelegate(tower);
    }
}

