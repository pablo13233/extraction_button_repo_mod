using BepInEx;
using BepInEx.Bootstrap;
using ExtractionButtonMod.Configuration;
using ExtractionButtonMod.Patches;
using ExtractionButtonMod.Services;
using HarmonyLib;

namespace ExtractionButtonMod
{
    /// <summary>
    /// BepInEx plugin entry point for the Extraction Button Confirm mod.
    /// </summary>
    [BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
    public sealed class ExtractionButtonPlugin : BaseUnityPlugin
    {
        private Harmony? _harmony;

        /// <summary>
        /// Gets the active singleton instance of the plugin.
        /// </summary>
        public static ExtractionButtonPlugin? Instance { get; private set; }

        /// <summary>
        /// Gets the strongly-typed mod configuration.
        /// </summary>
        public ModConfig? ModConfig { get; private set; }

        private void Awake()
        {
            Instance = this;

            ModConfig = new ModConfig(Config);
            var confirmationService = new ExtractionConfirmationService(ModConfig, Logger);
            ExtractionPointPatches.Initialize(confirmationService);

            _harmony = new Harmony(PluginInfo.PluginGuid);
            _harmony.PatchAll(typeof(ExtractionPointPatches));

            CheckForConflictingPlugins();

            Logger.LogInfo($"{PluginInfo.PluginName} v{PluginInfo.PluginVersion} cargado correctamente (Modo: {ModConfig.ButtonMode.Value}).");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void CheckForConflictingPlugins()
        {
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                if (plugin?.Metadata == null || plugin.Metadata.GUID == PluginInfo.PluginGuid)
                {
                    continue;
                }

                if (plugin.Metadata.GUID.IndexOf("ExtractionDelayConfirm", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    plugin.Metadata.Name.IndexOf("ExtractionDelayConfirm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Logger.LogWarning(
                        $"Se detectó otro mod de confirmación de extracción instalado ({plugin.Metadata.Name} [{plugin.Metadata.GUID}]). " +
                        "Se recomienda desactivarlo en Thunderstore Mod Manager para evitar conflictos en ExtractionPoint.");
                }
            }
        }
    }
}

