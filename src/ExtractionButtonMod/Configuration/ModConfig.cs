using System;
using BepInEx.Configuration;

namespace ExtractionButtonMod.Configuration
{
    /// <summary>
    /// Strongly-typed configuration container for the Extraction Button Confirm mod.
    /// </summary>
    public sealed class ModConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModConfig"/> class and binds entries to the provided <see cref="ConfigFile"/>.
        /// </summary>
        /// <param name="config">The BepInEx configuration file associated with the plugin.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is <see langword="null"/>.</exception>
        public ModConfig(ConfigFile config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Enabled = config.Bind(
                "General",
                "Enabled",
                true,
                "Activa o desactiva el mod. Cuando está activo, las extracciones no se envían automáticamente al alcanzar la cuota, sino hasta presionar el botón de confirmación.");

            ButtonMode = config.Bind(
                "General",
                "ButtonMode",
                ConfirmationButtonMode.Both,
                "Elige qué botón físico usar para confirmar la extracción en niveles normales:\n" +
                "- Both: Habilita tanto el pedestal de la tienda (shopStation) como el botón de la máquina (Recomendado).\n" +
                "- ShopPedestalButton: Conserva el pedestal con botón de la tienda (shopStation) al lado del punto de extracción.\n" +
                "- ExtractionPointButton: Usa únicamente el botón propio de la máquina de extracción.");
        }

        /// <summary>
        /// Gets the configuration entry indicating whether the mod is enabled.
        /// </summary>
        public ConfigEntry<bool> Enabled { get; }

        /// <summary>
        /// Gets the configuration entry specifying which physical button mode is active.
        /// </summary>
        public ConfigEntry<ConfirmationButtonMode> ButtonMode { get; }

        /// <summary>
        /// Gets a value indicating whether the extraction machine's own button acts as a confirmation button.
        /// </summary>
        public bool UsesExtractionPointButton =>
            ButtonMode.Value == ConfirmationButtonMode.ExtractionPointButton ||
            ButtonMode.Value == ConfirmationButtonMode.Both;

        /// <summary>
        /// Gets a value indicating whether the Shop pedestal (<c>shopStation</c>) should be preserved and enabled in standard levels.
        /// </summary>
        public bool UsesShopPedestalButton =>
            ButtonMode.Value == ConfirmationButtonMode.ShopPedestalButton ||
            ButtonMode.Value == ConfirmationButtonMode.Both;
    }
}

