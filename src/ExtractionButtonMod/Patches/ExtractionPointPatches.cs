using ExtractionButtonMod.Services;
using HarmonyLib;

namespace ExtractionButtonMod.Patches
{
    /// <summary>
    /// Harmony patches for <see cref="ExtractionPoint"/> to replace automatic quota extraction
    /// with a manual confirmation button press.
    /// </summary>
    [HarmonyPatch(typeof(ExtractionPoint))]
    internal static class ExtractionPointPatches
    {
        private static ExtractionConfirmationService? s_service;

        /// <summary>
        /// Initializes the patch class with the active <see cref="ExtractionConfirmationService"/>.
        /// </summary>
        /// <param name="service">The confirmation service instance.</param>
        internal static void Initialize(ExtractionConfirmationService service)
        {
            s_service = service;
        }

        [HarmonyPatch("ShopSetup")]
        [HarmonyPrefix]
        private static bool ShopSetupPrefix(ExtractionPoint __instance)
        {
            return s_service == null || s_service.TryOverrideShopSetup(__instance);
        }

        [HarmonyPatch("HaulChecker")]
        [HarmonyPrefix]
        private static void HaulCheckerPrefix(ExtractionPoint __instance)
        {
            s_service?.PreventAutomaticExtraction(__instance);
        }

        [HarmonyPatch("ActivateTheFirstExtractionPointAutomaticallyWhenAPlayerLeaveTruck")]
        [HarmonyPrefix]
        private static bool AutoActivateFirstPointPrefix(ExtractionPoint __instance)
        {
            return s_service == null || s_service.ShouldAllowAutoActivateFromTruck(__instance);
        }

        [HarmonyPatch("StateActive")]
        [HarmonyPostfix]
        private static void StateActivePostfix(ExtractionPoint __instance)
        {
            s_service?.UpdateMainButtonInActiveState(__instance);
        }

        [HarmonyPatch("OnClick")]
        [HarmonyPrefix]
        private static bool OnClickPrefix(ExtractionPoint __instance)
        {
            return s_service == null || s_service.TryHandleMainButtonClick(__instance);
        }

        [HarmonyPatch("OnShopClick")]
        [HarmonyPrefix]
        private static bool OnShopClickPrefix(ExtractionPoint __instance)
        {
            return s_service == null || s_service.TryHandleShopButtonClick(__instance);
        }

        [HarmonyPatch("ShopButtonPushVisualsStart")]
        [HarmonyPostfix]
        private static void ShopButtonPushVisualsStartPostfix(ExtractionPoint __instance)
        {
            s_service?.TriggerButtonPushVisuals(__instance);
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void UpdatePostfix(ExtractionPoint __instance)
        {
            s_service?.UpdateConfirmationButtonAnimation(__instance);
        }

        [HarmonyPatch("StateComplete")]
        [HarmonyPrefix]
        private static void StateCompletePrefix(ExtractionPoint __instance, out UnityEngine.Transform? __state)
        {
            __state = null;
            if (s_service != null && !Internal.ExtractionPointAccess.IsShopRef(__instance) && __instance.shopStation != null)
            {
                __state = __instance.shopStation;
                __instance.shopStation = null!;
            }
        }

        [HarmonyPatch("StateComplete")]
        [HarmonyPostfix]
        private static void StateCompletePostfix(ExtractionPoint __instance, UnityEngine.Transform? __state)
        {
            if (__state != null)
            {
                __instance.shopStation = __state;
            }
        }
    }
}

